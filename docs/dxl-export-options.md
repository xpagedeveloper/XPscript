# NotesDXLExporter option values

XPscript exposes the numeric HCL Domino DXL exporter options directly. The numeric values below are the values to assign in XPscript.

## RichTextOption

Controls how Notes rich-text items are represented in exported DXL.

| Value | Mode | Meaning |
| ---: | --- | --- |
| `0` | DXL | Convert rich text to structured DXL elements. This is the normal choice when the exported XML should be readable and portable. |
| `1` | ItemData | Preserve rich text as Notes item data instead of converting it to structured DXL. |

Example:

```xpscript
exporter.RichTextOption = 0
```

Use `0` for normal structured DXL. Use `1` when preserving the Notes item representation is more important than producing semantic rich-text DXL.

## MIMEOption

Controls how native MIME items are represented in exported DXL.

| Value | Mode | Meaning |
| ---: | --- | --- |
| `0` | DXL | Convert MIME content to the DXL MIME representation. |
| `1` | ItemData | Preserve MIME as Notes item data instead of converting it to DXL MIME elements. |

Example:

```xpscript
exporter.MIMEOption = 0
```

Use `0` for normal DXL interchange. Use `1` when the native Notes item representation must be retained.

## ValidationStyle

Controls which XML validation reference Domino writes for the exported DXL.

| Value | Mode | Meaning |
| ---: | --- | --- |
| `0` | None | Do not request DTD or XML Schema validation. |
| `1` | DTD | Use the Domino DXL DTD validation style. |
| `2` | XML Schema | Use XML Schema validation. |

Example:

```xpscript
exporter.ValidationStyle = 1
```

For conventional Domino DXL export, `1` is usually the most recognizable choice. Use `0` when the output should not carry a validation reference, and `2` when XML Schema validation is required.

## Typical configurations

Structured DXL suitable for inspection, source control, and interchange:

```xpscript
Dim exporter As NotesDXLExporter
Set exporter = session.CreateDXLExporter()

exporter.RichTextOption = 0
exporter.MIMEOption = 0
exporter.ValidationStyle = 1
exporter.ForceNoteFormat = False
exporter.OutputDOCTYPE = True
```

More native Notes-oriented item representation:

```xpscript
exporter.RichTextOption = 1
exporter.MIMEOption = 1
```

`RichTextOption`, `MIMEOption`, and `ValidationStyle` are numeric option properties. Boolean exporter properties such as `ForceNoteFormat`, `ExitOnFirstFatalError`, and `OutputDOCTYPE` should be set with `True` or `False` in XPscript.

After an export operation, `NotesDXLExporter.Log` can be read to inspect Domino's DXL result log:

```xpscript
Call exporter.ExportDocument(doc, "out/document.dxl")
Print exporter.Log
```

See also [`notes-c-api.md`](notes-c-api.md) and the runnable [`notes-dxl-import-export-surface.xps`](../samples/notes-dxl-import-export-surface.xps) sample.
