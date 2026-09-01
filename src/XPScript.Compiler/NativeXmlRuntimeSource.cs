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
            System.Xml.Linq.XDocument document => document.ToString(System.Xml.Linq.SaveOptions.DisableFormatting),
            System.Xml.Linq.XNode node => node.ToString(System.Xml.Linq.SaveOptions.DisableFormatting),
            _ => throw new XPScriptRuntimeException(13, "XmlStringify requires XmlDocument or XmlNode.")
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
    public virtual string Name => Node is System.Xml.Linq.XElement element ? element.Name.LocalName : "";
    public virtual string Value
    {
        get => Node switch
        {
            System.Xml.Linq.XElement element => element.Value,
            System.Xml.Linq.XCData cdata => cdata.Value,
            System.Xml.Linq.XText text => text.Value,
            System.Xml.Linq.XComment comment => comment.Value,
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
                default:
                    throw new XPScriptRuntimeException(5, "XML node value cannot be changed for this node type.");
            }
        }
    }

    public string Stringify() => Node.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
}

internal sealed class XPScriptXmlElement : XPScriptXmlNode
{
    internal XPScriptXmlElement(System.Xml.Linq.XElement element) : base(element) { }
    internal System.Xml.Linq.XElement Element => (System.Xml.Linq.XElement)Node;

    public override string Name => Element.Name.LocalName;
    public int Count => Element.Elements().Count();

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

    public bool HasAttribute(object? nameValue)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        return Element.Attribute(name) is not null;
    }

    public void RemoveAttribute(object? nameValue)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        Element.Attribute(name)?.Remove();
    }

    public XPScriptXmlElement AddElement(object? nameValue, object? value = null)
    {
        var child = new System.Xml.Linq.XElement(XPScriptNativeXml.RequireName(nameValue));
        if (value is not null && !XPScriptNullRuntime.IsNull(value)) child.Value = XPScriptNativeXml.ScalarText(value);
        Element.Add(child);
        return new XPScriptXmlElement(child);
    }

    public void Add(object? value)
    {
        if (value is not XPScriptXmlNode node)
            throw new XPScriptRuntimeException(13, "XmlElement.Add requires XmlNode.");
        Element.Add(CloneNode(node.Node));
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

    private static System.Xml.Linq.XNode CloneNode(System.Xml.Linq.XNode node) => node switch
    {
        System.Xml.Linq.XElement element => new System.Xml.Linq.XElement(element),
        System.Xml.Linq.XCData cdata => new System.Xml.Linq.XCData(cdata.Value),
        System.Xml.Linq.XText text => new System.Xml.Linq.XText(text.Value),
        System.Xml.Linq.XComment comment => new System.Xml.Linq.XComment(comment.Value),
        System.Xml.Linq.XProcessingInstruction instruction => new System.Xml.Linq.XProcessingInstruction(instruction.Target, instruction.Data),
        _ => throw new XPScriptRuntimeException(5, "Unsupported XML node type.")
    };
}

internal sealed class XPScriptXmlDocument
{
    internal XPScriptXmlDocument(System.Xml.Linq.XDocument document) => Document = document;
    internal System.Xml.Linq.XDocument Document { get; }

    public bool Indent { get; set; } = true;
    public bool OmitXmlDeclaration { get; set; } = true;
    public XPScriptXmlElement? Root => Document.Root is null ? null : new XPScriptXmlElement(Document.Root);

    public XPScriptXmlElement CreateElement(object? name) => XPScriptNativeXml.CreateElement(name);

    public void SetRoot(object? value)
    {
        if (value is not XPScriptXmlElement element)
            throw new XPScriptRuntimeException(13, "XmlDocument.SetRoot requires XmlElement.");
        var replacement = new System.Xml.Linq.XElement(element.Element);
        if (Document.Root is null) Document.Add(replacement);
        else Document.Root.ReplaceWith(replacement);
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

internal sealed class XPScriptXmlNodeCollection
{
    private readonly System.Collections.Generic.List<XPScriptXmlNode> _items;
    internal XPScriptXmlNodeCollection(System.Collections.Generic.IEnumerable<XPScriptXmlNode> items) => _items = items.ToList();
    public int Count => _items.Count;
    public XPScriptXmlNode? Get(object? indexValue)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= _items.Count) throw new XPScriptRuntimeException(9, "XML node index out of range.");
        return _items[index];
    }
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

internal sealed class XPScriptXmlValidationErrorCollection
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
