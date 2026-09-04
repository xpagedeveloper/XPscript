# Native XML

XPscript provides a native XML DOM-style API backed by .NET `System.Xml.Linq`. It supports document creation, parsing, navigation, XPath element selection, attributes, mutation, cloning, serialization and internal DTD validation.

The XML runtime is cross-platform and is only included when an application uses the XML API.

## Create a document

Create an empty document with either form:

```xpscript
Dim doc As New XmlDocument
```

or:

```xpscript
Dim doc As XmlDocument
Set doc = New XmlDocument
```

A new document has no root element:

```xpscript
If Not doc.HasRoot Then
    Print "empty"
End If
```

Create the root directly:

```xpscript
Dim root As XmlElement
Set root = doc.AddRoot("person")
```

Or create a detached element and assign it as the root:

```xpscript
Set root = doc.CreateElement("person")
Call doc.SetRoot(root)
```

`SetRoot` clones the supplied element into the document. The source element remains independent.

`DocumentElement` is an alias for `Root`.

## Parse XML

```xpscript
Dim doc As XmlDocument
Set doc = XmlDocument.Parse("<person id=""42""><name>Fredrik</name></person>")
```

`XmlParse(xml)` is the shorthand equivalent:

```xpscript
Set doc = XmlParse(xmlText)
```

A constructor argument also parses XML:

```xpscript
Dim doc As New XmlDocument("<root><child /></root>")
```

`LoadXml(xml)` replaces the contents of an existing `XmlDocument`:

```xpscript
Call doc.LoadXml("<newroot><item>1</item></newroot>")
```

Normal parsing requires well-formed XML. DTD processing is prohibited, `XmlResolver` is disabled, and external entities are not resolved. XML input is limited to 8 MiB.

## Build XML

```xpscript
Dim doc As New XmlDocument
Dim root As XmlElement
Dim person As XmlElement

Set root = doc.AddRoot("people")
Set person = root.AddElement("person")
Call person.SetAttribute("id", 42)
Call person.AddElement("name", "Fredrik")
Call person.AddElement("active", True)

Print doc.Stringify()
```

Scalar values are converted using invariant formatting. Text and attribute values are escaped automatically.

A document can also create detached nodes:

```xpscript
Dim element As XmlElement
Dim node As XmlNode

Set element = doc.CreateElement("item", "value")
Set node = doc.CreateTextNode("plain text")
Set node = doc.CreateCData("raw <content>")
Set node = doc.CreateComment("comment")
Set node = doc.CreateProcessingInstruction("target", "value=1")
```

Detached nodes can later be inserted into an element. Insertions clone the supplied node, so the original remains independent.

## XmlDocument reference

| Member | Behavior |
|---|---|
| `Root` | Returns the root `XmlElement`, or `Nothing` when empty. |
| `DocumentElement` | Alias for `Root`. |
| `HasRoot` | `True` when the document has a root element. |
| `ChildNodes` | Top-level XML nodes as `XmlNodeCollection`. |
| `Indent` | Controls formatted serialization. Default `True`. |
| `OmitXmlDeclaration` | Omits the XML declaration when `True`. Default `True`. |
| `CreateElement(name)` | Creates a detached element. |
| `CreateElement(name, value)` | Creates a detached element with text content. |
| `CreateTextNode(value)` | Creates a detached text node. |
| `CreateCData(value)` | Creates a detached CDATA node. |
| `CreateComment(value)` | Creates a detached comment. |
| `CreateProcessingInstruction(target, data)` | Creates a detached processing instruction. |
| `AddRoot(name [, value])` | Creates and attaches the root element. Raises an error if a root already exists. |
| `SetRoot(element)` | Replaces or creates the root using a clone of `element`. |
| `RemoveRoot()` | Removes the root. Returns `True` if one existed. |
| `Clear()` | Removes all nodes and the XML declaration. |
| `LoadXml(xml)` | Replaces document contents by securely parsing XML text. |
| `SelectSingleNode(xpath)` | Returns the first matching element or `Nothing`. |
| `SelectNodes(xpath)` | Returns matching elements as `XmlNodeCollection`. |
| `ValidateDTD(dtd)` | Validates against internal DTD declarations. |
| `IsValidDTD(dtd)` | Boolean DTD validation helper. |
| `Stringify()` | Serializes the document. |

## XmlNode navigation

All element objects are also `XmlNode` objects.

```xpscript
Dim node As XmlNode
Set node = doc.SelectSingleNode("/people/person[1]")

Print node.NodeType
Print node.Name
Print node.Parent.Name
Print node.NextSibling.Name
```

`XmlNode` exposes:

| Member | Behavior |
|---|---|
| `NodeType` | `Element`, `Text`, `CData`, `Comment`, `ProcessingInstruction`, `DocumentType` or `Node`. |
| `Name` | Element name, otherwise an empty string. |
| `Value` | Gets or sets element/text/CDATA/comment/processing-instruction content where applicable. |
| `InnerText` | Alias for `Value`. |
| `OuterXml` | Serialized node XML. |
| `Parent` | Parent `XmlElement`, or `Nothing`. |
| `OwnerDocument` | Owning `XmlDocument`, or `Nothing` for a detached node. |
| `HasParent` | `True` when attached to an element or document. |
| `HasChildNodes` | Whether the node contains child nodes. |
| `ChildCount` | Number of child nodes, including text/comments. |
| `ChildNodes` | Child nodes as `XmlNodeCollection`. |
| `FirstChild` | First child or `Nothing`. |
| `LastChild` | Last child or `Nothing`. |
| `PreviousSibling` | Previous sibling or `Nothing`. |
| `NextSibling` | Next sibling or `Nothing`. |
| `Clone()` | Returns a detached deep clone. |
| `InsertBefore(node)` | Inserts a clone before the current attached node and returns the inserted clone. |
| `InsertAfter(node)` | Inserts a clone after the current attached node and returns the inserted clone. |
| `ReplaceWith(node)` | Replaces the current attached node with a clone and returns the replacement. |
| `Remove()` | Removes the node. Returns `False` when already detached. |
| `Delete()` | Alias for `Remove()`. |
| `Stringify()` | Serializes the node. |

Sibling insertion follows XML document rules. For example, inserting a second root element beside the document root is invalid.

## XmlElement elements and children

`XmlElement` adds element-specific navigation and mutation:

```xpscript
Dim root As XmlElement
Dim item As XmlElement

Set root = doc.AddRoot("items")
Set item = root.AddElement("item", "first")
Set item = root.PrependElement("item", "zero")
```

| Member | Behavior |
|---|---|
| `Name` | Local element name. |
| `Rename(name)` | Renames the element. |
| `Count` | Number of direct child elements. |
| `ElementCount` | Alias for `Count`. |
| `Elements` | All direct child elements as `XmlNodeCollection`. |
| `AddElement(name [, value])` | Appends and returns a child element. |
| `PrependElement(name [, value])` | Prepends and returns a child element. |
| `Add(node)` | Appends a clone of a node. |
| `AppendChild(node)` | Appends a clone and returns the inserted node. |
| `PrependChild(node)` | Prepends a clone and returns the inserted node. |
| `AddText(value)` | Appends a text node. |
| `AddCData(value)` | Appends a CDATA node. |
| `AddComment(value)` | Appends a comment node. |
| `GetElement(name)` | Returns the first direct child element with that name. |
| `GetElements(name)` | Returns matching direct child elements. |
| `GetDescendants(name)` | Returns matching descendant elements at any depth. |
| `RemoveElement(name)` | Removes the first matching direct child. Returns Boolean. |
| `RemoveElements(name)` | Removes all matching direct children and returns the number removed. |
| `RemoveChildren()` | Removes all child nodes but preserves attributes. |
| `RemoveAll()` | Removes all child nodes and all attributes. |
| `SelectSingleNode(xpath)` | XPath element selection relative to the element. |
| `SelectNodes(xpath)` | XPath element collection relative to the element. |

`Value` on an element replaces its child content with text, matching `System.Xml.Linq.XElement.Value` semantics.

## Attributes

String helpers remain available:

```xpscript
Call root.SetAttribute("id", 42)
Print root.GetAttribute("id")
Print root.HasAttribute("id")
Call root.RemoveAttribute("id")
```

`RemoveAttribute(name)` now returns `True` when an attribute was removed and `False` when it did not exist.

For object-level attribute navigation:

```xpscript
Dim attr As XmlAttribute
Dim attrs As XmlAttributeCollection

Set attrs = root.Attributes
Set attr = root.GetAttributeNode("id")

Print attr.Name
Print attr.Value
attr.Value = "43"
Call attr.Delete()
```

`XmlElement` attribute members:

| Member | Behavior |
|---|---|
| `AttributeCount` | Number of attributes. |
| `Attributes` | `XmlAttributeCollection`. |
| `SetAttribute(name, value)` | Adds or replaces an attribute. |
| `GetAttribute(name)` | Returns attribute text or an empty string when missing. |
| `GetAttributeNode(name)` | Returns the `XmlAttribute` object or `Nothing`. |
| `HasAttribute(name)` | Tests for an attribute. |
| `RemoveAttribute(name)` | Removes an attribute and returns Boolean. |
| `RemoveAllAttributes()` | Removes all attributes. |

`XmlAttribute` exposes:

| Member | Behavior |
|---|---|
| `Name` | Attribute local name. |
| `Value` | Read/write attribute value. |
| `Parent` | Owning element or `Nothing` when detached. |
| `OwnerElement` | Alias for `Parent`. |
| `IsNamespaceDeclaration` | Whether this is an XML namespace declaration. |
| `Remove()` | Removes the attribute and returns Boolean. |
| `Delete()` | Alias for `Remove()`. |
| `Stringify()` | Serializes the attribute. |

`XmlAttributeCollection` exposes `Count`, `First`, `Last`, `Get(index)`, `Get(name)` and `Has(name)`.

Indexes are zero-based.

## Collections and ForAll

`XmlNodeCollection`, `XmlAttributeCollection` and `XmlValidationErrorCollection` are enumerable with `ForAll`.

```xpscript
Dim nodes As XmlNodeCollection
Set nodes = doc.SelectNodes("/people/person")

ForAll node In nodes
    Print node.Name
End ForAll
```

`XmlNodeCollection` exposes:

- `Count`
- `First`
- `Last`
- `Get(index)`

## XPath

```xpscript
Dim node As XmlNode
Dim nodes As XmlNodeCollection

Set node = doc.SelectSingleNode("/people/person[@id='42']")
Set nodes = doc.SelectNodes("/people/person")
```

`SelectSingleNode` and `SelectNodes` are available on both `XmlDocument` and `XmlElement`.

The current XPath API returns element nodes and does not expose a namespace-manager abstraction. Use the direct attribute API for attributes.

## Delete and replace examples

Delete a selected node:

```xpscript
Dim node As XmlNode
Set node = doc.SelectSingleNode("/people/person[@id='42']")
If Not node Is Nothing Then
    Call node.Delete()
End If
```

Replace a node:

```xpscript
Dim replacement As XmlElement
Set replacement = doc.CreateElement("person")
Call replacement.SetAttribute("id", "99")
Call node.ReplaceWith(replacement)
```

Remove all direct elements with a name:

```xpscript
removed = root.RemoveElements("person")
```

Clear a whole document:

```xpscript
Call doc.Clear()
```

## Serialization

`XmlStringify(documentOrNode)` serializes an XML document, node or attribute.

```xpscript
Print XmlStringify(doc)
Print XmlStringify(root)
```

`XmlDocument.Stringify()` is equivalent for documents.

Formatting properties:

- `Indent`, default `True`
- `OmitXmlDeclaration`, default `True`

When the declaration is emitted it uses UTF-8 without a BOM. Serialized newlines use LF.

`XmlEscape(value)` escapes standalone XML text:

```xpscript
Print XmlEscape("5 < 10 & 20 > 10")
```

Result:

```text
5 &lt; 10 &amp; 20 &gt; 10
```

## Internal DTD validation

DTD validation accepts internal DTD declarations as a string. XPscript does not resolve external DTDs or external entities.

```xpscript
Dim dtd As String
Dim result As XmlValidationResult

dtd = _
    "<!ELEMENT person (name,email)>" & Chr(10) & _
    "<!ELEMENT name (#PCDATA)>" & Chr(10) & _
    "<!ELEMENT email (#PCDATA)>"

Set result = doc.ValidateDTD(dtd)

If result.Valid Then
    Print "Valid"
Else
    Print result.Errors.Get(0).Message
End If
```

`ValidateDTD(dtd)` expects internal DTD declarations only. Do not include a `DOCTYPE` wrapper. `SYSTEM` and `PUBLIC` external identifiers are rejected and validation runs with `XmlResolver = null`.

For a Boolean-only check:

```xpscript
If doc.IsValidDTD(dtd) Then
    Print "Valid"
End If
```

Validation objects expose:

- `XmlValidationResult.Valid`
- `XmlValidationResult.Errors`
- `XmlValidationErrorCollection.Count`
- `XmlValidationErrorCollection.Get(index)`
- `XmlValidationError.Message`
- `XmlValidationError.Line`
- `XmlValidationError.Column`
- `XmlValidationError.Severity`

## Resource and security limits

- XML parse input: 8 MiB maximum.
- DTD validation input: 1 MiB maximum.
- Normal parsing prohibits DTD processing.
- `XmlResolver` is disabled.
- DTD validation rejects `SYSTEM` and `PUBLIC` external identifiers.
- The API does not fetch external entities or schemas.

See `samples/native-xml-dom-regression.xps` for executable creation, navigation, attributes, insertion, replacement, deletion, parsing and clearing coverage.
