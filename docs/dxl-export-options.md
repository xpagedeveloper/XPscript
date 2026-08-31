# NotesDXLExporter option values

XPscript exposes the numeric HCL Domino DXL exporter options directly and also provides XPscript-specific export options for stable source-control output.

## CleanedDXL

`CleanedDXL` is an XPscript exporter option. It is not a native Domino DXL property.

When `False`, export behavior is unchanged and Domino writes raw DXL directly to the requested destination.

When `True`, XPscript exports Domino's raw DXL to a temporary file, normalizes it, and only then writes the cleaned XML to the requested destination. Raw DXL is therefore never written to the final target path.

```xpscript
exporter.CleanedDXL = True
Call exporter.ExportDatabaseDesign(db, "out/database-design.dxl")
```

The cleaned profile removes volatile Notes/Domino metadata while preserving functional design content and XML element ordering.

Removed elements, matched by local XML name regardless of namespace:

- `noteinfo`
- `updatedby`
- `revisions`
- `wassignedby`
- `agentrun`
- `agentmodified`
- `designchange`
- `databaseinfo`

Removed attributes:

- `replicaid`
- `maintenanceversion`
- `milestonebuild`

The cleaner also removes the explicitly denylisted compiled Java item `$ClassData`. Other `$` items are preserved, including functionally significant items such as `$Flags` and `$FlagsExt`.

XML output is normalized to UTF-8 without BOM, LF line endings, deterministic attribute ordering, stable indentation, and normalized empty-element serialization. XML element order is preserved.

`version` and `designerversion` are retained because they may be relevant when the Domino DXL format itself is part of the comparison.

## ExportDesignToFolders

`ExportDesignToFolders` is an XPscript-specific Boolean option for `ExportDatabaseDesign`.

When enabled, the destination argument to `ExportDatabaseDesign` is treated as a root directory rather than a DXL filename. XPscript exports the design to a temporary DXL document and then creates one DXL file per design element in a type-specific subdirectory.

```xpscript
exporter.CleanedDXL = True
exporter.ExportDesignToFolders = True
Call exporter.ExportDatabaseDesign(db, "out/design")
```

Typical output:

```text
out/design/
  agents/
    My Agent.dxl
  forms/
    Customer.dxl
  views/
    Customers.dxl
  subforms/
    Address.dxl
  scriptlibraries/
    Common.dxl
```

Known design types use stable plural folder names such as `agents`, `forms`, `views`, `subforms`, `folders`, `scriptlibraries`, `sharedfields`, `sharedactions`, `pages`, `framesets`, `outlines`, `navigators`, `images`, and `resources`. Unknown design types use a safe pluralized fallback based on the DXL element name.

The filename is based on the design element's `name` attribute, then `title`, then `$TITLE` or `$$ScriptName` when needed. Invalid filesystem characters are replaced. Duplicate names receive a numeric suffix rather than overwriting an existing file.

For each exported design file, XPscript reads the design element's raw DXL metadata before cleanup and applies:

- Notes/Domino `created` time to the file creation time when the filesystem supports setting it
- Notes/Domino `modified` time to the file last-write time
- `revised` as a fallback for last-write time when `modified` is unavailable

This works together with `CleanedDXL`: the timestamps are captured before volatile `noteinfo` metadata is removed, so the generated DXL remains clean while the filesystem preserves the design element dates.

`ExportDesignToFolders` only changes `ExportDatabaseDesign`. `ExportDocument` and `ExportDocumentCollection` continue to require normal file destinations and are never split into folders.

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

exporter.CleanedDXL = True
exporter.RichTextOption = 0
exporter.MIMEOption = 0
exporter.ValidationStyle = 1
exporter.ForceNoteFormat = False
exporter.OutputDOCTYPE = True
```

Repository-style design export:

```xpscript
exporter.CleanedDXL = True
exporter.ExportDesignToFolders = True
Call exporter.ExportDatabaseDesign(db, "src/design")
```

More native Notes-oriented item representation:

```xpscript
exporter.CleanedDXL = False
exporter.RichTextOption = 1
exporter.MIMEOption = 1
```

`RichTextOption`, `MIMEOption`, and `ValidationStyle` are numeric option properties. Boolean exporter properties such as `CleanedDXL`, `ExportDesignToFolders`, `ForceNoteFormat`, `ExitOnFirstFatalError`, and `OutputDOCTYPE` should be set with `True` or `False` in XPscript.

After an export operation, `NotesDXLExporter.Log` can be read to inspect Domino's DXL result log:

```xpscript
Call exporter.ExportDocument(doc, "out/document.dxl")
Print exporter.Log
```

See also [`notes-c-api.md`](notes-c-api.md) and the runnable [`notes-dxl-import-export-surface.xps`](../samples/notes-dxl-import-export-surface.xps) sample.
