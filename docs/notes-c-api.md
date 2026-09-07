# HCL Notes/Domino C API wrapper

XPscript exposes a native HCL Notes/Domino object model backed by the Notes/Domino C API. The implementation does not use the Domino Java APIs, JNI, JNA, or JNX.

This page documents the public XPscript surface implemented by the generated runtime. It is the compatibility contract for the members listed here. The runtime surface is assembled from the core Notes sources and their post-processors, so documentation must follow the final generated surface rather than an individual source fragment.

## NotesSession

Exactly one `NotesSession` may be active in a process. A session can be created with an explicit Notes/Domino runtime directory, `notes.ini`, and optional ID password. XPscript also supports the default constructor when the runtime can be resolved by the host-specific discovery rules.

### Properties

| Property | Type | Access | Description |
| --- | --- | --- | --- |
| `RuntimeDirectory` | String | read-only | Resolved Notes/Domino runtime directory. |
| `ProgramDir` | String | read-only | LotusScript-compatible alias of `RuntimeDirectory`. |
| `NotesIni` | String | read-only | Explicit `notes.ini` path, or an empty string when no explicit path was supplied. |
| `IniDir` | String | read-only | Directory containing the resolved `notes.ini`, when one can be located. |
| `DataDir` | String | read-only | Notes data directory resolved from the active `notes.ini` `Directory=` setting. |
| `Username` | String | read-only | Current Notes user name. |
| `UserName` | String | read-only | Alias of `Username`. |
| `CommonUsername` | String | read-only | Common-name form derived from the current Notes user name. |
| `CommonUserName` | String | read-only | Alias of `CommonUsername`. |
| `NotesVersion` | String | read-only | Notes/Domino runtime version text. |
| `NotesBuildVersion` | Long | read-only | Runtime build number. |
| `Platform` | String | read-only | LotusScript-compatible platform name for the current host. |
| `ConvertMIME` | Boolean | read/write | Controls automatic conversion of MIME parts to composite rich text when documents are opened. Defaults to `True`. |
| `IsRecycled` | Boolean | read-only | `True` after the session has been recycled. |

### Functions and methods

| Member | Return type | Description |
| --- | --- | --- |
| `GetEnvironmentString(name)` | String | Reads a Notes environment value. Non-system names are resolved with a leading `$`. |
| `GetEnvironmentString(name, system)` | String | Reads a Notes environment value and controls whether the name is treated as a system variable. |
| `GetEnvironmentValue(name)` | Variant | Reads an environment value and returns an Integer when the stored text is an integer; otherwise returns `Nothing`. |
| `GetEnvironmentValue(name, system)` | Variant | Numeric environment lookup with explicit system-variable handling. |
| `OpenDatabase(server, filePath)` | `NotesDatabase` | Opens a local or server NSF. An empty server selects a local database. Failed opens return a closed wrapper with `IsOpen = False`. |
| `OpenByReplicaID(server, replicaId)` | `NotesDatabase` | Locates and opens a database by replica ID. |
| `CreateName(value)` | `NotesName` | Creates and parses a Notes name. |
| `CreateDateTime(value)` | `NotesDateTime` | Parses a Notes date/time value. |
| `CreateDateTimeNow()` | `NotesDateTime` | Creates a Notes date/time representing the current time. |
| `CreateStream()` | `NotesStream` | Creates an in-memory/file-capable Notes stream wrapper. |
| `CreateDXLImporter()` | `NotesDXLImporter` | Creates a native DXL importer wrapper. |
| `CreateDXLExporter()` | `NotesDXLExporter` | Creates a native DXL exporter wrapper. |
| `CreateRichTextStyle()` | `NotesRichTextStyle` | Creates a rich-text character style initialized to `STYLE_NO_CHANGE` semantics. |
| `CreateRichTextParagraphStyle()` | `NotesRichTextParagraphStyle` | Creates a paragraph style initialized from the native default compound-text style. |
| `Recycle()` | Void | Recycles child Notes objects, releases the password hook, terminates Notes, and unloads the native runtime. |

## NotesDocument

A `NotesDocument` owns an open native note handle and is created from a database, view navigation, document collection, DXL-related operation, or another Notes API returning a document.

### Properties

| Property | Type | Access | Description |
| --- | --- | --- | --- |
| `NoteID` | String | read-only | LotusScript-compatible Note ID formatted as eight hexadecimal digits. |
| `NoteIdHex` | String | read-only | Alias exposing the same eight-digit hexadecimal Note ID. |
| `UniversalID` | String | read/write | Document Universal ID. Setting updates the native note UNID. |
| `Items` | `NotesItem` array | read-only | Array of item wrappers in native item order. Duplicate item names are preserved. |
| `Size` | Long | read-only | Native document size. |
| `Responses` | `NotesDocumentCollection` | read-only | Response documents for this document. |
| `ParentDocumentUNID` | String | read-only | Parent document UNID for a response document, or an empty string when no parent exists. |
| `Created` | `NotesDateTime` | read-only | Native document creation time. |
| `LastModified` | `NotesDateTime` | read-only | Native document last-modified time. |
| `LastAccessed` | `NotesDateTime` | read-only | Native document last-accessed time. |
| `IsValid` | Boolean | read-only | `True` while the wrapper represents a valid live note. |
| `IsNewNote` | Boolean | read-only | `True` before a newly created document receives a Note ID. |
| `IsProfile` | Boolean | read-only | `True` for profile documents. |
| `IsResponse` | Boolean | read-only | `True` when the document has a parent response relationship. |
| `IsDeleted` | Boolean | read-only | Native deleted-note state exposed by the runtime. |
| `IsDesign` | Boolean | read-only | `True` for Domino design notes. |
| `DesignType` | String | read-only | XPscript design classification derived from note class and design flags. |
| `DesignTitle` | String | read-only | Primary design title derived from `$TITLE`. |
| `DesignAlias` | String | read-only | Remaining design aliases derived from `$TITLE`, joined with `|`. |
| `IsRecycled` | Boolean | read-only | `True` after the wrapper has been recycled. |

### Functions and methods

| Member | Return type | Description |
| --- | --- | --- |
| `GetFirstItem(itemName)` | `NotesItem`, `NotesRichTextItem`, or `Nothing` | Returns the first matching item. Composite items are returned as `NotesRichTextItem`. |
| `CreateNotesItem(itemName)` | `NotesItem` | Creates an empty text item. |
| `CreateRichTextItem(itemName)` | `NotesRichTextItem` | Creates a new composite/rich-text item. The name must be non-empty and not already exist. |
| `ReplaceItemValue(itemName, value)` | `NotesItem` | Creates or replaces an item value and returns the resulting item wrapper. |
| `HasItem(itemName)` | Boolean | Tests whether an item exists. |
| `GetValue(itemName)` | Variant | Returns the native item value through the generic value conversion. |
| `GetItemValue(itemName)` | Variant array | Returns all item values as a one-dimensional array. |
| `GetItemValue(itemName)(index)` | Variant | Returns one zero-based value. Out-of-range access raises runtime error 9. |
| `GetString(itemName)` | String | Returns a text value. |
| `GetNumber(itemName)` | Double | Returns a numeric value. |
| `GetDateTime(itemName)` | `NotesDateTime` | Returns a Notes time/date value. |
| `SetValue(itemName, value)` | Void | Writes a value using the generic native item setter. |
| `SetString(itemName, value)` | Void | Writes a text item. |
| `SetNumber(itemName, value)` | Void | Writes a numeric item. |
| `SetDateTime(itemName, value)` | Void | Writes a `NotesDateTime`. Other value types raise runtime error 13. |
| `RemoveItem(itemName)` | Void | Removes an item by name. |
| `CopyItem(item, newName)` | `NotesItem` | Copies an item into this document, optionally under a new name. |
| `CopyAllItems(destination)` | Void | Copies all items to another document without replacing existing destination items. |
| `CopyAllItems(destination, replace)` | Void | Copies all items and controls replacement of existing destination items. |
| `CopyToDatabase(database)` | `NotesDocument` | Copies the document to another open database and returns the copied document. |
| `MakeResponse(parentDocument)` | Void | Makes this document a response to the supplied parent document. |
| `PutInFolder(folderName)` | Void | Adds the document to a folder. |
| `PutInFolder(folderName, createOnFail)` | Void | Adds the document to a folder and optionally creates the folder if required. |
| `RemoveFromFolder(folderName)` | Void | Removes the document from a folder. |
| `MarkRead()` | Void | Marks the document read for the current Notes user. |
| `MarkRead(userName)` | Void | Marks the document read for the supplied Notes user. |
| `MarkUnread()` | Void | Marks the document unread for the current Notes user. |
| `MarkUnread(userName)` | Void | Marks the document unread for the supplied Notes user. |
| `SaveAttachment(attachmentName, path)` | Boolean | Extracts an attachment to `path`; returns `False` when it cannot be saved. |
| `ComputeWithForm()` | Boolean | Computes the document with its form using the default validation behavior. |
| `ComputeWithForm(doDataTypes, raiseError)` | Boolean | Computes with form using the supplied compatibility options. |
| `Save()` | Void | Saves the note and refreshes its Note ID. |
| `Remove(force)` | Void | Deletes the document using the native remove path. |
| `Recycle()` | Void | Closes the native note handle. |

### Item values

`ReplaceItemValue` and `NotesItem.Values` support scalar values and one-dimensional homogeneous arrays of text, numeric, or `NotesDateTime` values. Mixed arrays and unsupported value kinds raise runtime error 13. Empty or unallocated arrays write an empty text value.

## NotesDocument design metadata

`IsDesign`, `DesignType`, `DesignTitle`, and `DesignAlias` are XPscript extensions used to expose design-note metadata without requiring callers to decode native note classes and `$Flags`. `DesignTitle` and `DesignAlias` are derived from `$TITLE`. Non-design documents return `False` from `IsDesign` and empty strings from the design string properties.

## Other Notes objects

The generated runtime also exposes `NotesDatabase`, `NotesView`, `NotesDocumentCollection`, `NotesNoteCollection`, `NotesItem`, `NotesRichTextItem`, `NotesName`, `NotesDateTime`, `NotesAgent`, `NotesAgentResult`, `NotesStream`, `NotesDXLImporter`, `NotesDXLExporter`, `NotesRichTextStyle`, `NotesRichTextParagraphStyle`, rich-text navigator/range/table objects, and the implemented view-entry/navigation objects. Focused pages in this documentation set describe those surfaces in more detail.

## Object creation rules

Only `NotesSession` is constructed directly with `New`. Other Notes wrappers are obtained from their owning Notes object. Important session factories are `CreateName`, `CreateDateTime`, `CreateDateTimeNow`, `CreateStream`, `CreateDXLImporter`, `CreateDXLExporter`, `CreateRichTextStyle`, and `CreateRichTextParagraphStyle`.

## Recycling and ownership

`Recycle()` is idempotent. A session tracks its live children and recycles them before terminating Notes. Native resource ownership remains inside the generated runtime. Database handles, note handles, NIF collections, DXL handles, and movable Notes memory are released by their matching native operations.

`NotesDocument.Items` returns live `NotesItem`/`NotesRichTextItem` wrappers. Recycling an item wrapper does not recycle its parent document. Recycling the owning session recycles remaining child wrappers.

When a typed Notes variable is reassigned with `Set`, XPscript evaluates the replacement first and then recycles the previous wrapper unless both references are the same object. This supports patterns such as:

```xpscript
Set doc = view.GetNextDocument(doc)
```

## Error handling

Notes wrapper validation and native C API failures are surfaced as `XPScriptRuntimeException` values and participate in normal XPscript error handling. Common wrapper-generated errors include runtime error 5 for invalid arguments, runtime error 9 for indexed access outside its valid range, runtime error 13 for type mismatches, and runtime error 91 for recycled or otherwise invalid Notes wrappers.

## Validation

The authoritative executable surface checks are the Notes runtime samples and `tests/NotesFullRuntimeSurfaceAudit`. The audit inspects the final generated Notes runtime surface, rejects placeholder implementations, and requires public NotesDocument members to be represented by the full-surface samples.
