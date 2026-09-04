# Native XML

XPscript provides a native XML surface parallel to the native JSON API. The runtime uses .NET `System.Xml.Linq` for the document and node model.

## Parse and navigate

```xpscript
Dim doc As XPXmlDocument
Dim root As XPXmlElement

Set doc = XPXmlDocument.Parse("<person id=""42""><name>Fredrik</name></person>")
Set root = doc.Root

Print root.Name
Print root.GetAttribute("id")
Print root.GetElement("name").Value
```

`XmlParse(xml)` is the shorthand equivalent of `XPXmlDocument.Parse(xml)`.

Normal XML parsing requires well-formed XML and does not process DTD declarations. Embedded `DOCTYPE` declarations are rejected by the parser.

## Build XML

```xpscript
Dim doc As New XPXmlDocument
Dim root As XPXmlElement

Set root = doc.CreateElement("person")
Call root.SetAttribute("id", 42)
Call root.AddElement("name", "Fredrik")
Call root.AddElement("active", True)
Call doc.SetRoot(root)

Print XmlStringify(doc)
```

Text values are escaped automatically. `XmlEscape(value)` is available when escaped XML text is needed independently of a document.

```xpscript
Print XmlEscape("5 < 10 & 20 > 10")
```

returns:

```text
5 &lt; 10 &amp; 20 &gt; 10
```

`XPXmlElement` also supports `Add`, `AddText`, `AddCData`, `AddComment`, `GetElement`, `GetElements`, `SetAttribute`, `GetAttribute`, `HasAttribute`, and `RemoveAttribute`.

## XPath

```xpscript
Dim node As XPXmlNode
Dim nodes As XPXmlNodeCollection

Set node = doc.SelectSingleNode("/people/person[@id='42']")
Set nodes = doc.SelectNodes("/people/person")
```

`SelectSingleNode` and `SelectNodes` are available on both `XPXmlDocument` and `XPXmlElement`. The initial API uses ordinary XPath expressions without a namespace-manager abstraction.

## Serialization

`XmlStringify(documentOrNode)` serializes an XML document or node. `XPXmlDocument.Stringify()` is equivalent for documents.

`XPXmlDocument` has these formatting properties:

- `Indent`, default `True`
- `OmitXmlDeclaration`, default `True`

When the declaration is emitted it uses UTF-8 without a BOM. Serialized newlines use LF.

## Internal DTD validation

DTD validation intentionally accepts the DTD as a string. XPscript does not resolve external DTDs or external entities.

```xpscript
Dim dtd As String
Dim result As XPXmlValidationResult

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

`ValidateDTD(dtd)` expects internal DTD declarations only. Do not include a `DOCTYPE` wrapper. `SYSTEM` and `PUBLIC` external identifiers are rejected and the validator runs with `XmlResolver = null`.

For a Boolean-only check use:

```xpscript
If doc.IsValidDTD(dtd) Then
    Print "Valid"
End If
```

Validation results expose:

- `XPXmlValidationResult.Valid`
- `XPXmlValidationResult.Errors`
- `XPXmlValidationErrorCollection.Count`
- `XPXmlValidationErrorCollection.Get(index)`
- `XPXmlValidationError.Message`
- `XPXmlValidationError.Line`
- `XPXmlValidationError.Column`
- `XPXmlValidationError.Severity`

Indexes in XML collections are zero-based.

## Resource limits

Native XML parsing accepts at most 8 MiB of XML text. DTD validation accepts at most 1 MiB of DTD text. These limits are applied before parsing or validation.
