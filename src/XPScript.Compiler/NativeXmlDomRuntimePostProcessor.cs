namespace XPScript.Compiler;

internal sealed class NativeXmlDomRuntimePostProcessor
{
    private const string Sentinel = "public XPScriptXmlAttributeCollection Attributes";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (!generated.Contains("internal sealed class XPScriptXmlDocument", StringComparison.Ordinal)) return generated;
        if (generated.Contains(Sentinel, StringComparison.Ordinal)) return generated;

        generated = ReplaceRequired(generated,
            """
    public string Stringify() => Node.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
}

internal sealed class XPScriptXmlElement : XPScriptXmlNode
""",
            """
    public bool HasParent => Node.Parent is not null || Node.Document is not null;
    public bool HasChildNodes => Node.Nodes().Any();
    public int ChildCount => Node.Nodes().Count();
    public XPScriptXmlNodeCollection ChildNodes => new(Node.Nodes().Select(WrapNode));
    public XPScriptXmlNode? FirstChild => Node.Nodes().FirstOrDefault() is { } node ? WrapNode(node) : null;
    public XPScriptXmlNode? LastChild => Node.Nodes().LastOrDefault() is { } node ? WrapNode(node) : null;
    public XPScriptXmlNode? PreviousSibling => Node.PreviousNode is { } node ? WrapNode(node) : null;
    public XPScriptXmlNode? NextSibling => Node.NextNode is { } node ? WrapNode(node) : null;
    public XPScriptXmlDocument? OwnerDocument => Node.Document is { } document ? new XPScriptXmlDocument(document) : null;
    public string InnerText
    {
        get => Value;
        set => Value = value;
    }
    public string OuterXml => Stringify();

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
        if (Node.Parent is null && Node.Document is null)
            throw new XPScriptRuntimeException(5, "XML node must be attached before inserting a sibling.");
        Node.AddBeforeSelf(clone);
        return WrapNode(clone);
    }

    public XPScriptXmlNode InsertAfter(object? value)
    {
        var clone = RequireNodeClone(value, "InsertAfter");
        if (Node.Parent is null && Node.Document is null)
            throw new XPScriptRuntimeException(5, "XML node must be attached before inserting a sibling.");
        Node.AddAfterSelf(clone);
        return WrapNode(clone);
    }

    public XPScriptXmlNode ReplaceWith(object? value)
    {
        var clone = RequireNodeClone(value, "ReplaceWith");
        if (Node.Parent is null && Node.Document is null)
            throw new XPScriptRuntimeException(5, "XML node must be attached before it can be replaced.");
        Node.ReplaceWith(clone);
        return WrapNode(clone);
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

    public string Stringify() => Node.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
}

internal sealed class XPScriptXmlElement : XPScriptXmlNode
""", "node-navigation");

        generated = ReplaceRequired(generated,
            """
    public override string Name => Element.Name.LocalName;
    public int Count => Element.Elements().Count();

    public void SetAttribute(object? nameValue, object? value)
""",
            """
    public override string Name => Element.Name.LocalName;
    public int Count => Element.Elements().Count();
    public int ElementCount => Count;
    public int AttributeCount => Element.Attributes().Count();
    public XPScriptXmlNodeCollection Elements => new(Element.Elements().Select(item => (XPScriptXmlNode)new XPScriptXmlElement(item)));
    public XPScriptXmlAttributeCollection Attributes => new(Element.Attributes());

    public void Rename(object? nameValue) => Element.Name = XPScriptNativeXml.RequireName(nameValue);

    public void SetAttribute(object? nameValue, object? value)
""", "element-properties");

        generated = ReplaceRequired(generated,
            """
    public void RemoveAttribute(object? nameValue)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        Element.Attribute(name)?.Remove();
    }

    public XPScriptXmlElement AddElement(object? nameValue, object? value = null)
""",
            """
    public bool RemoveAttribute(object? nameValue)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        var attribute = Element.Attribute(name);
        if (attribute is null) return false;
        attribute.Remove();
        return true;
    }

    public XPScriptXmlAttribute? GetAttributeNode(object? nameValue)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        var attribute = Element.Attribute(name);
        return attribute is null ? null : new XPScriptXmlAttribute(attribute);
    }

    public void RemoveAllAttributes() => Element.RemoveAttributes();

    public XPScriptXmlElement AddElement(object? nameValue, object? value = null)
""", "attribute-api");

        generated = ReplaceRequired(generated,
            """
    public void Add(object? value)
    {
        if (value is not XPScriptXmlNode node)
            throw new XPScriptRuntimeException(13, "XmlElement.Add requires XmlNode.");
        Element.Add(CloneNode(node.Node));
    }

    public void AddText(object? value) => Element.Add(new System.Xml.Linq.XText(XPScriptNativeXml.ScalarText(value)));
""",
            """
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

    public XPScriptXmlElement PrependElement(object? nameValue, object? value = null)
    {
        var child = new System.Xml.Linq.XElement(XPScriptNativeXml.RequireName(nameValue));
        if (value is not null && !XPScriptNullRuntime.IsNull(value)) child.Value = XPScriptNativeXml.ScalarText(value);
        Element.AddFirst(child);
        return new XPScriptXmlElement(child);
    }

    public void AddText(object? value) => Element.Add(new System.Xml.Linq.XText(XPScriptNativeXml.ScalarText(value)));
""", "child-insertion");

        generated = ReplaceRequired(generated,
            """
    public XPScriptXmlNodeCollection GetElements(object? nameValue)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        return new XPScriptXmlNodeCollection(Element.Elements()
            .Where(item => item.Name.LocalName.Equals(name, StringComparison.Ordinal))
            .Select(item => (XPScriptXmlNode)new XPScriptXmlElement(item)));
    }

    public XPScriptXmlNode? SelectSingleNode(object? xpathValue)
""",
            """
    public XPScriptXmlNodeCollection GetElements(object? nameValue)
    {
        var name = XPScriptNativeXml.RequireName(nameValue);
        return new XPScriptXmlNodeCollection(Element.Elements()
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
""", "element-removal");

        generated = ReplaceRequired(generated,
            """
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
""",
            """
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
""", "attribute-types");

        generated = ReplaceRequired(generated,
            """
    public bool Indent { get; set; } = true;
    public bool OmitXmlDeclaration { get; set; } = true;
    public XPScriptXmlElement? Root => Document.Root is null ? null : new XPScriptXmlElement(Document.Root);

    public XPScriptXmlElement CreateElement(object? name) => XPScriptNativeXml.CreateElement(name);

    public void SetRoot(object? value)
""",
            """
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
    public XPScriptXmlNode CreateTextNode(object? value) => new XPScriptXmlNode(new System.Xml.Linq.XText(XPScriptNativeXml.ScalarText(value)));
    public XPScriptXmlNode CreateCData(object? value) => new XPScriptXmlNode(new System.Xml.Linq.XCData(XPScriptNativeXml.ScalarText(value)));
    public XPScriptXmlNode CreateComment(object? value) => new XPScriptXmlNode(new System.Xml.Linq.XComment(XPScriptNativeXml.ScalarText(value)));
    public XPScriptXmlNode CreateProcessingInstruction(object? target, object? data)
        => new XPScriptXmlNode(new System.Xml.Linq.XProcessingInstruction(XPScriptNativeXml.RequireName(target), XPScriptRuntime.CStr(data)));

    public XPScriptXmlElement AddRoot(object? name, object? value = null)
    {
        if (Document.Root is not null) throw new XPScriptRuntimeException(5, "XML document already has a root element.");
        var root = new System.Xml.Linq.XElement(XPScriptNativeXml.RequireName(name));
        if (value is not null && !XPScriptNullRuntime.IsNull(value)) root.Value = XPScriptNativeXml.ScalarText(value);
        Document.Add(root);
        return new XPScriptXmlElement(root);
    }

    public void SetRoot(object? value)
""", "document-creation");

        generated = ReplaceRequired(generated,
            """
    public XPScriptXmlNode? SelectSingleNode(object? xpathValue)
""",
            """
    public bool RemoveRoot()
    {
        if (Document.Root is null) return false;
        Document.Root.Remove();
        return true;
    }

    public void Clear() => Document.RemoveNodes();

    public void LoadXml(object? value)
    {
        var parsed = XPScriptNativeXml.Parse(value);
        Document.RemoveNodes();
        foreach (var node in parsed.Document.Nodes()) Document.Add(XPScriptXmlNode.CloneNode(node));
    }

    public XPScriptXmlNode? SelectSingleNode(object? xpathValue)
""", "document-mutation", occurrence: 2);

        generated = ReplaceRequired(generated,
            """
internal sealed class XPScriptXmlNodeCollection
{
    private readonly System.Collections.Generic.List<XPScriptXmlNode> _items;
    internal XPScriptXmlNodeCollection(System.Collections.Generic.IEnumerable<XPScriptXmlNode> items) => _items = items.ToList();
    public int Count => _items.Count;
    public XPScriptXmlNode? Get(object? indexValue)
""",
            """
internal sealed class XPScriptXmlNodeCollection : System.Collections.IEnumerable
{
    private readonly System.Collections.Generic.List<XPScriptXmlNode> _items;
    internal XPScriptXmlNodeCollection(System.Collections.Generic.IEnumerable<XPScriptXmlNode> items) => _items = items.ToList();
    public int Count => _items.Count;
    public XPScriptXmlNode? First => _items.Count == 0 ? null : _items[0];
    public XPScriptXmlNode? Last => _items.Count == 0 ? null : _items[^1];
    public XPScriptXmlNode? Get(object? indexValue)
""", "node-collection");

        generated = ReplaceRequired(generated,
            """
        return _items[index];
    }
}

internal sealed class XPScriptXmlValidationResult
""",
            """
        return _items[index];
    }
    public System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
}

internal sealed class XPScriptXmlValidationResult
""", "node-collection-enumeration");

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage, int occurrence = 1)
    {
        var start = 0;
        var index = -1;
        for (var i = 0; i < occurrence; i++)
        {
            index = source.IndexOf(oldValue, start, StringComparison.Ordinal);
            if (index < 0) throw new CompilerException($"Unable to install native XML DOM runtime ({stage}).");
            start = index + oldValue.Length;
        }
        return source[..index] + newValue + source[(index + oldValue.Length)..];
    }
}
