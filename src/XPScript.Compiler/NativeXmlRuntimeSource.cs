namespace XPScript.Compiler;

internal static class NativeXmlRuntimeSource
{
    public const string Code = """
internal static class XPScriptNativeXml
{
    private const int MaxParseBytes = 8 * 1024 * 1024;
    private const int MaxDtdBytes = 1024 * 1024;

    public static XPScriptXmlDocument CreateDocument() => new(new System.Xml.Linq.XDocument());
    public static XPScriptXmlElement CreateElement(object? name) => new(new System.Xml.Linq.XElement(RequireName(name)));

    public static XPScriptXmlDocument Parse(object? value)
    {
        var text = XPScriptRuntime.CStr(value);
        if (System.Text.Encoding.UTF8.GetByteCount(text) > MaxParseBytes)
            throw new XPScriptRuntimeException(5, "XML input exceeds the 8 MiB parse limit.");

        try
        {
            var settings = new System.Xml.XmlReaderSettings
            {
                DtdProcessing = System.Xml.DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = false,
                IgnoreWhitespace = false
            };
            using var stringReader = new System.IO.StringReader(text);
            using var reader = System.Xml.XmlReader.Create(stringReader, settings);
            var document = System.Xml.Linq.XDocument.Load(
                reader,
                System.Xml.Linq.LoadOptions.PreserveWhitespace | System.Xml.Linq.LoadOptions.SetLineInfo);
            return new XPScriptXmlDocument(document);
        }
        catch (XPScriptRuntimeException)
        {
            throw;
        }
        catch (System.Xml.XmlException)
        {
            throw new XPScriptRuntimeException(5, "Invalid XML input.");
        }
    }

    public static string Stringify(object? value)
    {
        return value switch
        {
            XPScriptXmlDocument document => document.Stringify(),
            XPScriptXmlNode node => node.Stringify(),
            XPScriptXmlAttribute attribute => attribute.Stringify(),
            System.Xml.Linq.XDocument document => document.ToString(System.Xml.Linq.SaveOptions.DisableFormatting),
            System.Xml.Linq.XNode node => node.ToString(System.Xml.Linq.SaveOptions.DisableFormatting),
            System.Xml.Linq.XAttribute attribute => attribute.ToString(),
            _ => throw new XPScriptRuntimeException(13, "XmlStringify requires XmlDocument, XmlNode or XmlAttribute.")
        };
    }

    public static string Escape(object? value)
        => new System.Xml.Linq.XText(XPScriptRuntime.CStr(value)).ToString(System.Xml.Linq.SaveOptions.DisableFormatting);

    internal static string RequireName(object? value)
    {
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "XML element name cannot be empty.");
        try
        {
            System.Xml.XmlConvert.VerifyName(name);
            return name;
        }
        catch (System.Xml.XmlException)
        {
            throw new XPScriptRuntimeException(5, "Invalid XML element or attribute name.");
        }
    }

    internal static string ScalarText(object? value)
    {
        if (value is null || XPScriptNullRuntime.IsNull(value)) return "";
        if (value is ILSObjectReference reference)
        {
            if (reference.IsNothing) return "";
            value = reference.ObjectValue;
        }
        if (value is bool boolean) return boolean ? "true" : "false";
        if (value is DateTime dateTime) return dateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        if (value is IFormattable formattable) return formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        return XPScriptRuntime.CStr(value);
    }

    internal static XPScriptXmlValidationResult ValidateDtd(System.Xml.Linq.XDocument document, object? dtdValue)
    {
        if (document.Root is null)
            return XPScriptXmlValidationResult.FromSingleError("XML document has no root element.", 0, 0, "Error");

        var dtd = XPScriptRuntime.CStr(dtdValue);
        if (System.Text.Encoding.UTF8.GetByteCount(dtd) > MaxDtdBytes)
            throw new XPScriptRuntimeException(5, "DTD input exceeds the 1 MiB limit.");
        if (string.IsNullOrWhiteSpace(dtd))
            throw new XPScriptRuntimeException(5, "DTD input cannot be empty.");
        if (System.Text.RegularExpressions.Regex.IsMatch(dtd, @"<!\s*DOCTYPE\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            throw new XPScriptRuntimeException(5, "ValidateDTD expects internal DTD declarations, not a DOCTYPE declaration.");
        if (System.Text.RegularExpressions.Regex.IsMatch(dtd, @"\b(?:SYSTEM|PUBLIC)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            throw new XPScriptRuntimeException(5, "External DTD identifiers and external entities are not supported.");

        var root = document.Root;
        var prefix = root.GetPrefixOfNamespace(root.Name.Namespace);
        var rootName = string.IsNullOrWhiteSpace(prefix) ? root.Name.LocalName : prefix + ":" + root.Name.LocalName;
        var body = root.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
        var header = "<!DOCTYPE " + rootName + " [\n" + dtd + "\n]>\n";
        var validationText = header + body;
        var lineOffset = header.Count(ch => ch == '\n');
        var errors = new System.Collections.Generic.List<XPScriptXmlValidationError>();

        try
        {
            var settings = new System.Xml.XmlReaderSettings
            {
                DtdProcessing = System.Xml.DtdProcessing.Parse,
                ValidationType = System.Xml.ValidationType.DTD,
                XmlResolver = null,
                IgnoreComments = false,
                IgnoreWhitespace = false
            };
            settings.ValidationFlags |= System.Xml.Schema.XmlSchemaValidationFlags.ReportValidationWarnings;
            settings.ValidationEventHandler += (_, args) =>
            {
                var exception = args.Exception;
                var line = exception is null || exception.LineNumber <= lineOffset ? 0 : exception.LineNumber - lineOffset;
                var column = exception?.LinePosition ?? 0;
                errors.Add(new XPScriptXmlValidationError(
                    args.Message,
                    line,
                    column,
                    args.Severity == System.Xml.Schema.XmlSeverityType.Warning ? "Warning" : "Error"));
            };

            using var stringReader = new System.IO.StringReader(validationText);
            using var reader = System.Xml.XmlReader.Create(stringReader, settings);
            while (reader.Read()) { }
        }
        catch (System.Xml.XmlException ex)
        {
            var line = ex.LineNumber <= lineOffset ? 0 : ex.LineNumber - lineOffset;
            errors.Add(new XPScriptXmlValidationError(ex.Message, line, ex.LinePosition, "Error"));
        }

        return new XPScriptXmlValidationResult(errors);
    }
}

internal class XPScriptXmlNode
{
    internal XPScriptXmlNode(System.Xml.Linq.XNode node) => Node = node;
    internal System.Xml.Linq.XNode Node { get; }

    public string NodeType => Node switch
    {
        System.Xml.Linq.XElement => "Element",
        System.Xml.Linq.XCData => "CData",
        System.Xml.Linq.XText => "Text",
        System.Xml.Linq.XComment => "Comment",
        System.Xml.Linq.XProcessingInstruction => "ProcessingInstruction",
        System.Xml.Linq.XDocumentType => "DocumentType",
        _ => "Node"
    };

    public XPScriptXmlElement? Parent => Node.Parent is null ? null : new XPScriptXmlElement(Node.Parent);
    public XPScriptXmlDocument? OwnerDocument => Node.Document is null ? null : new XPScriptXmlDocument(Node.Document);
    public virtual string Name => Node is System.Xml.Linq.XElement element ? element.Name.LocalName : "";
    public virtual string Value
    {
        get => Node switch
        {
            System.Xml.Linq.XElement element => element.Value,
            System.Xml.Linq.XCData cdata => cdata.Value,
            System.Xml.Linq.XText text => text.Value,
            System.Xml.Linq.XComment comment => comment.Value,
            System.Xml.Linq.XProcessingInstruction instruction => instruction.Data,
            _ => ""
        };
        set
        {
            switch (Node)
            {
                case System.Xml.Linq.XElement element:
                    element.Value = XPScriptNativeXml.ScalarText(value);
                    break;
                case System.Xml.Linq.XCData cdata:
                    cdata.Value = XPScriptNativeXml.ScalarText(value);
                    break;
                case System.Xml.Linq.XText text:
                    text.Value = XPScriptNativeXml.ScalarText(value);
                    break;
                case System.Xml.Linq.XComment comment:
                    comment.Value = XPScriptNativeXml.ScalarText(value);
                    break;
                case System.Xml.Linq.XProcessingInstruction instruction:
                    instruction.Data = XPScriptNativeXml.ScalarText(value);
                    break;
                default:
                    throw new XPScriptRuntimeException(5, "XML node value cannot be changed for this node type.");
            }
        }
    }

    public string InnerText
    {
        get => Value;
        set => Value = value;
    }
    public string OuterXml => Stringify();
    public bool HasParent => Node.Parent is not null || Node.Document is not null;
    public bool HasChildNodes => ChildSequence().Any();
    public int ChildCount => ChildSequence().Count();
    public XPScriptXmlNodeCollection ChildNodes => new(ChildSequence().Select(WrapNode));
    public XPScriptXmlNode? FirstChild => ChildSequence().FirstOrDefault() is { } node ? WrapNode(node) : null;
    public XPScriptXmlNode? LastChild => ChildSequence().LastOrDefault() is { } node ? WrapNode(node) : null;
    public XPScriptXmlNode? PreviousSibling => Node.PreviousNode is { } previous ? WrapNode(previous) : null;
    public XPScriptXmlNode? NextSibling => Node.NextNode is { } next ? WrapNode(next) : null;

    public bool Remove()
    {
        if (Node.Parent is null && Node.Document is null) return false;
        Node.Remove();
        return true;
    }

    public bool Delete() => Remove();
    public XPScriptXmlNode Clone() => WrapNode(CloneNode(Node));

    public XPScriptXmlNode InsertBefore(object? value)
    {
        var clone = RequireNodeClone(value, "InsertBefore");
        EnsureAttached("insert a sibling");
        try { Node.AddBeforeSelf(clone); }
        catch (InvalidOperationException) { throw new XPScriptRuntimeException(5, "XML node cannot be inserted at this location."); }
        return WrapNode(clone);
    }

    public XPScriptXmlNode InsertAfter(object? value)
    {
        var clone = RequireNodeClone(value, "InsertAfter");
        EnsureAttached("insert a sibling");
        try { Node.AddAfterSelf(clone); }
        catch (InvalidOperationException) { throw new XPScriptRuntimeException(5, "XML node cannot be inserted at this location."); }
        return WrapNode(clone);
    }

    public XPScriptXmlNode ReplaceWith(object? value)
    {
        var clone = RequireNodeClone(value, "ReplaceWith");
        EnsureAttached("replace it");
        try { Node.ReplaceWith(clone); }
        catch (InvalidOperationException) { throw new XPScriptRuntimeException(5, "XML node cannot be replaced at this location."); }
        return WrapNode(clone);
    }

    public string Stringify() => Node.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);

    private System.Collections.Generic.IEnumerable<System.Xml.Linq.XNode> ChildSequence()
        => Node is System.Xml.Linq.XContainer container
            ? container.Nodes()
            : System.Linq.Enumerable.Empty<System.Xml.Linq.XNode>();

    private void EnsureAttached(string operation)
    {
        if (Node.Parent is null && Node.Document is null)
            throw new XPScriptRuntimeException(5, $"XML node must be attached before attempting to {operation}.");
    }

    internal static XPScriptXmlNode WrapNode(System.Xml.Linq.XNode node) => node switch
    {
        System.Xml.Linq.XElement element => new XPScriptXmlElement(element),
        _ => new XPScriptXmlNode(node)
    };

    internal static System.Xml.Linq.XNode CloneNode(System.Xml.Linq.XNode node) => node switch
    {
        System.Xml.Linq.XElement element => new System.Xml.Linq.XElement(element),
        System.Xml.Linq.XCData cdata => new System.Xml.Linq.XCData(cdata.Value),
        System.Xml.Linq.XText text => new System.Xml.Linq.XText(text.Value),
        System.Xml.Linq.XComment comment => new System.Xml.Linq.XComment(comment.Value),
        System.Xml.Linq.XProcessingInstruction instruction => new System.Xml.Linq.XProcessingInstruction(instruction.Target, instruction.Data),
        System.Xml.Linq.XDocumentType documentType => new System.Xml.Linq.XDocumentType(documentType.Name, documentType.PublicId, documentType.SystemId, documentType.InternalSubset),
        _ => throw new XPScriptRuntimeException(5, "Unsupported XML node type.")
    };

    private static System.Xml.Linq.XNode RequireNodeClone(object? value, string operation)
    {
        if (value is not XPScriptXmlNode node)
            throw new XPScriptRuntimeException(13, $"XmlNode.{operation} requires XmlNode.");
        return CloneNode(node.Node);
    }
}

internal sealed class XPScriptXmlElement : XPScriptXmlNode
{
    internal XPScriptXmlElement(System.Xml.Linq.XElement element) : base(element) { }
    internal System.Xml.Linq.XElement Element => (System.Xml.Linq.XElement)Node;

    public override string Name => Element.Name.LocalName;
    public int Count => Element.Elements().Count();
    public int ElementCount => Count;
    public int AttributeCount => Element.Attributes().Count();
    public XPScriptXmlNodeCollection Elements => new(Element.Elements().Select(item => (XPScriptXmlNode)new XPScriptXmlElement(item)));
    public XPScriptXmlAttributeCollection Attributes => new(Element.Attributes());

    public void Rename(object? nameValue) => Element.Name = XPScriptNativeXml.RequireName(nameValue);

    public void SetAttribute(object? nameValue, object? value)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        Element.SetAttributeValue(name, XPScriptNativeXml.ScalarText(value));
    }

    public string GetAttribute(object? nameValue)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        return Element.Attribute(name)?.Value ?? "";
    }

    public XPScriptXmlAttribute? GetAttributeNode(object? nameValue)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        var attribute = Element.Attribute(name);
        return attribute is null ? null : new XPScriptXmlAttribute(attribute);
    }

    public bool HasAttribute(object? nameValue)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        return Element.Attribute(name) is not null;
    }

    public bool RemoveAttribute(object? nameValue)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        var attribute = Element.Attribute(name);
        if (attribute is null) return false;
        attribute.Remove();
        return true;
    }

    public void RemoveAllAttributes() => Element.RemoveAttributes();

    public XPScriptXmlElement AddElement(object? nameValue, object? value = null)
    {
        var child = new System.Xml.Linq.XElement(XPScriptNativeXml.RequireName(nameValue));
        if (value is not null && !XPScriptNullRuntime.IsNull(value)) child.Value = XPScriptNativeXml.ScalarText(value);
        Element.Add(child);
        return new XPScriptXmlElement(child);
    }

    public XPScriptXmlElement PrependElement(object? nameValue, object? value = null)
    {
        var child = new System.Xml.Linq.XElement(XPScriptNativeXml.RequireName(nameValue));
        if (value is not null && !XPScriptNullRuntime.IsNull(value)) child.Value = XPScriptNativeXml.ScalarText(value);
        Element.AddFirst(child);
        return new XPScriptXmlElement(child);
    }

    public void Add(object? value) => AppendChild(value);

    public XPScriptXmlNode AppendChild(object? value)
    {
        if (value is not XPScriptXmlNode node)
            throw new XPScriptRuntimeException(13, "XmlElement.AppendChild requires XmlNode.");
        var clone = XPScriptXmlNode.CloneNode(node.Node);
        Element.Add(clone);
        return XPScriptXmlNode.WrapNode(clone);
    }

    public XPScriptXmlNode PrependChild(object? value)
    {
        if (value is not XPScriptXmlNode node)
            throw new XPScriptRuntimeException(13, "XmlElement.PrependChild requires XmlNode.");
        var clone = XPScriptXmlNode.CloneNode(node.Node);
        Element.AddFirst(clone);
        return XPScriptXmlNode.WrapNode(clone);
    }

    public void AddText(object? value) => Element.Add(new System.Xml.Linq.XText(XPScriptNativeXml.ScalarText(value)));
    public void AddCData(object? value) => Element.Add(new System.Xml.Linq.XCData(XPScriptNativeXml.ScalarText(value)));
    public void AddComment(object? value) => Element.Add(new System.Xml.Linq.XComment(XPScriptNativeXml.ScalarText(value)));

    public XPScriptXmlElement? GetElement(object? nameValue)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        var child = Element.Elements().FirstOrDefault(item => item.Name.LocalName.Equals(name, StringComparison.Ordinal));
        return child is null ? null : new XPScriptXmlElement(child);
    }

    public XPScriptXmlNodeCollection GetElements(object? nameValue)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        return new XPScriptXmlNodeCollection(Element.Elements()
            .Where(item => item.Name.LocalName.Equals(name, StringComparison.Ordinal))
            .Select(item => (XPScriptXmlNode)new XPScriptXmlElement(item)));
    }

    public XPScriptXmlNodeCollection GetDescendants(object? nameValue)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        return new XPScriptXmlNodeCollection(Element.Descendants()
            .Where(item => item.Name.LocalName.Equals(name, StringComparison.Ordinal))
            .Select(item => (XPScriptXmlNode)new XPScriptXmlElement(item)));
    }

    public bool RemoveElement(object? nameValue)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        var child = Element.Elements().FirstOrDefault(item => item.Name.LocalName.Equals(name, StringComparison.Ordinal));
        if (child is null) return false;
        child.Remove();
        return true;
    }

    public int RemoveElements(object? nameValue)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        var matches = Element.Elements().Where(item => item.Name.LocalName.Equals(name, StringComparison.Ordinal)).ToList();
        foreach (var child in matches) child.Remove();
        return matches.Count;
    }

    public void RemoveChildren() => Element.RemoveNodes();

    public void RemoveAll()
    {
        Element.RemoveAttributes();
        Element.RemoveNodes();
    }

    public XPScriptXmlNode? SelectSingleNode(object? xpathValue)
    {
        var xpath = XPScriptRuntime.CStr(xpathValue);
        try
        {
            var match = System.Xml.XPath.Extensions.XPathSelectElement(Element, xpath);
            return match is null ? null : new XPScriptXmlElement(match);
        }
        catch (System.Xml.XPath.XPathException)
        {
            throw new XPScriptRuntimeException(5, "Invalid XPath expression.");
        }
    }

    public XPScriptXmlNodeCollection SelectNodes(object? xpathValue)
    {
        var xpath = XPScriptRuntime.CStr(xpathValue);
        try
        {
            return new XPScriptXmlNodeCollection(System.Xml.XPath.Extensions.XPathSelectElements(Element, xpath)
                .Select(item => (XPScriptXmlNode)new XPScriptXmlElement(item)));
        }
        catch (System.Xml.XPath.XPathException)
        {
            throw new XPScriptRuntimeException(5, "Invalid XPath expression.");
        }
    }
}

internal sealed class XPScriptXmlAttribute
{
    internal XPScriptXmlAttribute(System.Xml.Linq.XAttribute attribute) => Attribute = attribute;
    internal System.Xml.Linq.XAttribute Attribute { get; }

    public string Name => Attribute.Name.LocalName;
    public string Value
    {
        get => Attribute.Value;
        set => Attribute.Value = XPScriptNativeXml.ScalarText(value);
    }
    public XPScriptXmlElement? Parent => Attribute.Parent is null ? null : new XPScriptXmlElement(Attribute.Parent);
    public XPScriptXmlElement? OwnerElement => Parent;
    public bool IsNamespaceDeclaration => Attribute.IsNamespaceDeclaration;
    public string Stringify() => Attribute.ToString();

    public bool Remove()
    {
        if (Attribute.Parent is null) return false;
        Attribute.Remove();
        return true;
    }

    public bool Delete() => Remove();
}

internal sealed class XPScriptXmlAttributeCollection : System.Collections.IEnumerable
{
    private readonly System.Collections.Generic.List<System.Xml.Linq.XAttribute> _items;
    internal XPScriptXmlAttributeCollection(System.Collections.Generic.IEnumerable<System.Xml.Linq.XAttribute> items) => _items = items.ToList();

    public int Count => _items.Count;
    public XPScriptXmlAttribute? First => _items.Count == 0 ? null : new XPScriptXmlAttribute(_items[0]);
    public XPScriptXmlAttribute? Last => _items.Count == 0 ? null : new XPScriptXmlAttribute(_items[^1]);

    public XPScriptXmlAttribute Get(object? indexOrName)
    {
        if (indexOrName is string nameValue)
        {
            var name = XPScriptNativeXml.RequireName(nameValue);
            var match = _items.FirstOrDefault(item => item.Name.LocalName.Equals(name, StringComparison.Ordinal));
            if (match is null) throw new XPScriptRuntimeException(9, "XML attribute was not found.");
            return new XPScriptXmlAttribute(match);
        }

        var index = XPScriptRuntime.CInt(indexOrName);
        if (index < 0 || index >= _items.Count) throw new XPScriptRuntimeException(9, "XML attribute index out of range.");
        return new XPScriptXmlAttribute(_items[index]);
    }

    public bool Has(object? nameValue)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        return _items.Any(item => item.Name.LocalName.Equals(name, StringComparison.Ordinal));
    }

    public System.Collections.IEnumerator GetEnumerator()
        => _items.Select(item => (object)new XPScriptXmlAttribute(item)).GetEnumerator();
}

internal sealed class XPScriptXmlDocument
{
    internal XPScriptXmlDocument(System.Xml.Linq.XDocument document) => Document = document;
    internal System.Xml.Linq.XDocument Document { get; }

    public bool Indent { get; set; } = true;
    public bool OmitXmlDeclaration { get; set; } = true;
    public XPScriptXmlElement? Root => Document.Root is null ? null : new XPScriptXmlElement(Document.Root);
    public XPScriptXmlElement? DocumentElement => Root;
    public bool HasRoot => Document.Root is not null;
    public XPScriptXmlNodeCollection ChildNodes => new(Document.Nodes().Select(XPScriptXmlNode.WrapNode));

    public XPScriptXmlElement CreateElement(object? name) => XPScriptNativeXml.CreateElement(name);

    public XPScriptXmlElement CreateElement(object? name, object? value)
    {
        var element = XPScriptNativeXml.CreateElement(name);
        element.Value = XPScriptNativeXml.ScalarText(value);
        return element;
    }

    public XPScriptXmlNode CreateTextNode(object? value)
        => new XPScriptXmlNode(new System.Xml.Linq.XText(XPScriptNativeXml.ScalarText(value)));

    public XPScriptXmlNode CreateCData(object? value)
        => new XPScriptXmlNode(new System.Xml.Linq.XCData(XPScriptNativeXml.ScalarText(value)));

    public XPScriptXmlNode CreateComment(object? value)
        => new XPScriptXmlNode(new System.Xml.Linq.XComment(XPScriptNativeXml.ScalarText(value)));

    public XPScriptXmlNode CreateProcessingInstruction(object? target, object? data)
    {
        try
        {
            return new XPScriptXmlNode(new System.Xml.Linq.XProcessingInstruction(
                XPScriptNativeXml.RequireName(target), XPScriptRuntime.CStr(data)));
        }
        catch (ArgumentException)
        {
            throw new XPScriptRuntimeException(5, "Invalid XML processing instruction.");
        }
    }

    public XPScriptXmlElement AddRoot(object? name, object? value = null)
    {
        if (Document.Root is not null) throw new XPScriptRuntimeException(5, "XML document already has a root element.");
        var root = new System.Xml.Linq.XElement(XPScriptNativeXml.RequireName(name));
        if (value is not null && !XPScriptNullRuntime.IsNull(value)) root.Value = XPScriptNativeXml.ScalarText(value);
        Document.Add(root);
        return new XPScriptXmlElement(root);
    }

    public void SetRoot(object? value)
    {
        if (value is not XPScriptXmlElement element)
            throw new XPScriptRuntimeException(13, "XmlDocument.SetRoot requires XmlElement.");
        var replacement = new System.Xml.Linq.XElement(element.Element);
        if (Document.Root is null) Document.Add(replacement);
        else Document.Root.ReplaceWith(replacement);
    }

    public bool RemoveRoot()
    {
        if (Document.Root is null) return false;
        Document.Root.Remove();
        return true;
    }

    public void Clear()
    {
        Document.RemoveNodes();
        Document.Declaration = null;
    }

    public void LoadXml(object? value)
    {
        var parsed = XPScriptNativeXml.Parse(value);
        Document.RemoveNodes();
        Document.Declaration = parsed.Document.Declaration is null
            ? null
            : new System.Xml.Linq.XDeclaration(parsed.Document.Declaration);
        foreach (var node in parsed.Document.Nodes()) Document.Add(XPScriptXmlNode.CloneNode(node));
    }

    public XPScriptXmlNode? SelectSingleNode(object? xpathValue)
    {
        var xpath = XPScriptRuntime.CStr(xpathValue);
        try
        {
            var match = System.Xml.XPath.Extensions.XPathSelectElement(Document, xpath);
            return match is null ? null : new XPScriptXmlElement(match);
        }
        catch (System.Xml.XPath.XPathException)
        {
            throw new XPScriptRuntimeException(5, "Invalid XPath expression.");
        }
    }

    public XPScriptXmlNodeCollection SelectNodes(object? xpathValue)
    {
        var xpath = XPScriptRuntime.CStr(xpathValue);
        try
        {
            return new XPScriptXmlNodeCollection(System.Xml.XPath.Extensions.XPathSelectElements(Document, xpath)
                .Select(item => (XPScriptXmlNode)new XPScriptXmlElement(item)));
        }
        catch (System.Xml.XPath.XPathException)
        {
            throw new XPScriptRuntimeException(5, "Invalid XPath expression.");
        }
    }

    public XPScriptXmlValidationResult ValidateDTD(object? dtd) => XPScriptNativeXml.ValidateDtd(Document, dtd);
    public bool IsValidDTD(object? dtd) => ValidateDTD(dtd).Valid;

    public string Stringify()
    {
        if (Document.Root is null) return "";
        var settings = new System.Xml.XmlWriterSettings
        {
            OmitXmlDeclaration = OmitXmlDeclaration,
            Indent = Indent,
            NewLineChars = "\n",
            NewLineHandling = System.Xml.NewLineHandling.Replace,
            Encoding = new System.Text.UTF8Encoding(false)
        };
        using var stream = new System.IO.MemoryStream();
        using (var writer = System.Xml.XmlWriter.Create(stream, settings)) Document.Save(writer);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}

internal sealed class XPScriptXmlNodeCollection : System.Collections.IEnumerable
{
    private readonly System.Collections.Generic.List<XPScriptXmlNode> _items;
    internal XPScriptXmlNodeCollection(System.Collections.Generic.IEnumerable<XPScriptXmlNode> items) => _items = items.ToList();

    public int Count => _items.Count;
    public XPScriptXmlNode? First => _items.Count == 0 ? null : _items[0];
    public XPScriptXmlNode? Last => _items.Count == 0 ? null : _items[^1];

    public XPScriptXmlNode? Get(object? indexValue)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= _items.Count) throw new XPScriptRuntimeException(9, "XML node index out of range.");
        return _items[index];
    }

    public System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
}

internal sealed class XPScriptXmlValidationResult
{
    internal XPScriptXmlValidationResult(System.Collections.Generic.IEnumerable<XPScriptXmlValidationError> errors)
    {
        Errors = new XPScriptXmlValidationErrorCollection(errors);
    }
    public bool Valid => Errors.Count == 0;
    public XPScriptXmlValidationErrorCollection Errors { get; }

    internal static XPScriptXmlValidationResult FromSingleError(string message, int line, int column, string severity)
        => new(new[] { new XPScriptXmlValidationError(message, line, column, severity) });
}

internal sealed class XPScriptXmlValidationErrorCollection : System.Collections.IEnumerable
{
    private readonly System.Collections.Generic.List<XPScriptXmlValidationError> _items;
    internal XPScriptXmlValidationErrorCollection(System.Collections.Generic.IEnumerable<XPScriptXmlValidationError> items) => _items = items.ToList();
    public int Count => _items.Count;
    public XPScriptXmlValidationError Get(object? indexValue)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= _items.Count) throw new XPScriptRuntimeException(9, "XML validation error index out of range.");
        return _items[index];
    }
    public System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
}

internal sealed class XPScriptXmlValidationError
{
    internal XPScriptXmlValidationError(string message, int line, int column, string severity)
    {
        Message = message;
        Line = line;
        Column = column;
        Severity = severity;
    }
    public string Message { get; }
    public int Line { get; }
    public int Column { get; }
    public string Severity { get; }
}
""";
}
