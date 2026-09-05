# HCL Notes/Domino C API wrapper

XPscript exposes a native HCL Notes/Domino object model backed by the Notes/Domino C API. The implementation does not use the Domino Java APIs, JNI, JNA, or JNX.

The supported XPscript Notes classes are:

- `NotesSession`
- `NotesDatabase`
- `NotesView`
- `NotesDocumentCollection`
- `NotesDocument`
- `NotesItem`
- `NotesRichTextItem`
- `NotesName`
- `NotesDateTime`
- `NotesAgentResult`

Except for `NotesSession`, objects are created from another Notes object rather than with `New`. Every Notes object supports `Recycle()`, and every wrapper exposes `IsRecycled` while it is alive. Explicitly recycling a typed Notes variable also sets that XPscript variable to `Nothing`.

## NotesSession

Exactly one `NotesSession` may be active in a process. The first constructor argument is the directory containing the Notes/Domino native runtime. The second argument is an optional explicit `notes.ini`. The third optional argument is an ID password used during Notes initialization.

```xpscript
Dim session As NotesSession
Set session = New NotesSession("C:\Program Files\HCL\Notes")

Dim sessionWithIni As NotesSession
Set sessionWithIni = New NotesSession("C:\Program Files\HCL\Notes", "C:\NotesData\notes.ini")
```

### Properties

| Property | Type | Access | Description |
| --- | --- | --- | --- |
| `RuntimeDirectory` | String | read-only | Resolved Notes/Domino runtime directory. |
| `NotesIni` | String | read-only | Resolved `notes.ini` path, or an empty string when the default configuration is used. |
| `Username` | String | read-only | Current Notes user name. |
| `UserName` | String | read-only | Alias of `Username`. |
| `CommonUsername` | String | read-only | Common-name form derived from the current Notes user name. |
| `CommonUserName` | String | read-only | Alias of `CommonUsername`. |
| `NotesVersion` | String | read-only | Notes/Domino runtime version text. |
| `NotesBuildVersion` | Long | read-only | Runtime build number. |
| `IsRecycled` | Boolean | read-only | `True` after the session has been recycled. |

### Functions and methods

| Member | Return type | Description |
| --- | --- | --- |
| `OpenDatabase(server, filePath)` | `NotesDatabase` | Opens a local or server NSF. An empty server selects a local database. If the database cannot be opened, a `NotesDatabase` object is still returned with `IsOpen = False`. |
| `OpenByReplicaID(server, replicaId)` | `NotesDatabase` | Locates and opens a database by replica ID. Returns a closed `NotesDatabase` wrapper if the database cannot be located or opened. |
| `CreateName(value)` | `NotesName` | Creates and parses a Notes name. |
| `CreateDateTime(value)` | `NotesDateTime` | Parses a Notes date/time value. |
| `CreateDateTimeNow()` | `NotesDateTime` | Creates a Notes date/time representing the current time. |
| `Recycle()` | Void | Recycles child objects, terminates the Notes runtime, releases the password hook if used, and unloads the native runtime. |

## NotesDatabase

Create a database from `NotesSession`.

```xpscript
Dim db As NotesDatabase
Set db = session.OpenDatabase("", "names.nsf")

Dim serverDb As NotesDatabase
Set serverDb = session.OpenDatabase("CN=Domino01/O=Example", "apps\customers.nsf")
```

### Properties

| Property | Type | Access | Description |
| --- | --- | --- | --- |
| `Parent` | `NotesSession` | read-only | Owning session. |
| `Server` | String | read-only | Server supplied when the database was opened. Empty for local databases. |
| `FilePath` | String | read-only | NSF path supplied or resolved when opening the database. |
| `FileName` | String | read-only | Final path component of `FilePath`. |
| `IsOpen` | Boolean | read-only | `True` when the native database handle is open. |
| `OpenError` | String | read-only | Error text from the most recent failed database open, or an empty string when no error was recorded. |
| `Title` | String | read/write | Database title. Setting the property updates the NSF title when the database is open. |
| `Categories` | String | read/write | Database categories. |
| `TemplateName` | String | read-only | Template name stored by the database. |
| `DesignTemplateName` | String | read-only | Design template name stored by the database. |
| `ReplicaID` | String | read-only | Database replica ID. |
| `Size` | Long | read-only | Database space usage size returned by the C API. |
| `PercentUsed` | Double | read-only | Percentage of allocated database space currently used. |
| `CurrentAccessLevel` | Integer | read-only | Current user's numeric Notes database access level. |
| `IsRecycled` | Boolean | read-only | `True` after the wrapper has been recycled. |
| `Agents` | NotesAgent array | read-only | All agent design elements in the database. Agent wrappers retain their Note IDs and open native agent handles only when needed. |
| `Forms` | NotesForm array | read-only | All form design elements in the database. Form wrappers retain their Note IDs and open design documents only when needed. |
| `Views` | NotesView array | read-only | All view and folder design elements in the database. |

### Functions and methods

| Member | Return type | Description |
| --- | --- | --- |
| `OpenView(name)` | `NotesView` or `Nothing` | Opens a view by name. Returns `Nothing` when the database is closed. Empty view names raise runtime error 5. |
| `GetDocumentByNoteId(noteId)` | `NotesDocument` or `Nothing` | Opens a document by numeric or hexadecimal Note ID. |
| `OpenDocumentByNoteId(noteId)` | `NotesDocument` or `Nothing` | Alias of `GetDocumentByNoteId`. |
| `GetDocumentByUNID(unid)` | `NotesDocument` or `Nothing` | Opens a document by Universal ID. |
| `OpenDocumentByUNID(unid)` | `NotesDocument` or `Nothing` | Alias of `GetDocumentByUNID`. |
| `Search(formula)` | `NotesDocumentCollection` or `Nothing` | Executes a formula search with no explicit result limit. |
| `Search(formula, maxResults)` | `NotesDocumentCollection` or `Nothing` | Executes a formula search and limits results when `maxResults > 0`. |
| `FTSearch(query)` | `NotesDocumentCollection` or `Nothing` | Runs a full-text search against the database. |
| `FTSearch(query, maxResults)` | `NotesDocumentCollection` or `Nothing` | Runs a full-text search with a result limit. The database must have the required FT index for the query. |
| `RunAgent(name)` | `NotesAgentResult` or `Nothing` | Runs an agent by name. |
| `RunAgent(name, document)` | `NotesAgentResult` or `Nothing` | Runs an agent with a `NotesDocument` as document context. |
| `Recycle()` | Void | Recycles owned views, documents, collections, agent results, and then closes the native database handle. |

`FullTextSearch` was an earlier internal/public name. The supported XPscript API is `FTSearch`.

## NotesView

Create a view from an open `NotesDatabase`.

```xpscript
Dim view As NotesView
Set view = db.OpenView("People")
```

### Properties

| Property | Type | Access | Description |
| --- | --- | --- | --- |
| `Name` | String | read-only | View name used when opening the collection. |
| `AutoUpdate` | Boolean | read/write | Controls whether document navigation refreshes the native collection before locating the next document. Defaults to `True`. |
| `IsRecycled` | Boolean | read-only | `True` after the wrapper has been recycled. |

### Functions and methods

| Member | Return type | Description |
| --- | --- | --- |
| `GetFirstDocumentByKey(key)` | `NotesDocument` or `Nothing` | Finds the first matching document using an exact text-key match. V1 lookup targets the first sorted text column. |
| `GetFirstDocumentByKey(key, exactMatch)` | `NotesDocument` or `Nothing` | Finds the first document by text key and controls exact versus partial matching. |
| `GetAllDocumentsByKey(key)` | `NotesDocumentCollection` | Returns all exact text-key matches. |
| `GetAllDocumentsByKey(key, exactMatch)` | `NotesDocumentCollection` | Returns text-key matches and controls exact versus partial matching. |
| `FTSearch(query)` | `NotesDocumentCollection` | Runs a full-text search scoped to the view. |
| `FTSearch(query, maxResults)` | `NotesDocumentCollection` | Runs a view full-text search with a result limit. |
| `GetFirstDocument()` | `NotesDocument` or `Nothing` | Starts navigation from the beginning of the view and returns the first document. |
| `GetNextDocument(document)` | `NotesDocument` or `Nothing` | Locates `document` in the current view state and returns the immediate next document. Returns `Nothing` if the supplied document is no longer present or is the last document. |
| `Refresh()` | Void | Calls the native collection update and rebuilds the view navigation snapshot. |
| `Recycle()` | Void | Closes the native NIF collection handle. |

### AutoUpdate navigation semantics

Each `NotesView` instance has independent navigation state, even when two objects point to the same underlying view design.

With `AutoUpdate = True`, `GetFirstDocument()` and `GetNextDocument(doc)` update the collection and read the current view order before returning a document. `GetNextDocument` finds the supplied document by Note ID in that current order.

With `AutoUpdate = False`, switching the property to `False` updates the collection once and captures the current Note ID order. `GetFirstDocument()` and `GetNextDocument(doc)` then use that frozen order. `Refresh()` explicitly refreshes that particular view object even while `AutoUpdate` is `False`.

```xpscript
Dim liveView As NotesView
Dim frozenView As NotesView

Set liveView = db.OpenView("($VIMGroups)")
Set frozenView = db.OpenView("($VIMGroups)")

liveView.AutoUpdate = True
frozenView.AutoUpdate = False
```

## NotesDocumentCollection

Searches and view lookups return copied Note IDs. The collection does not keep all matching notes open.

### Properties

| Property | Type | Access | Description |
| --- | --- | --- | --- |
| `Count` | Integer | read-only | Number of Note IDs in the collection. |
| `FirstNoteId` | String or `Nothing` | read-only | First Note ID as an 8-digit hexadecimal string, or `Nothing` for an empty collection. |
| `IsRecycled` | Boolean | read-only | `True` after the wrapper has been recycled. |

### Functions, indexing, and bounds

| Member | Return type | Description |
| --- | --- | --- |
| `GetNoteIdString(index)` | String | Returns the Note ID at a zero-based index as an 8-digit hexadecimal string. |
| `Get(index)` | String | Alias of `GetNoteIdString(index)`. |
| `collection(index)` | String | XPscript index syntax. The compiler rewrites this to `GetNoteIdString(index)`. |
| `LBound(collection)` | Integer | Returns `0`. |
| `UBound(collection)` | Integer | Returns `Count - 1`. |
| `Recycle()` | Void | Releases the managed Note ID snapshot. |

To open a returned document, pass the Note ID to the owning database:

```xpscript
Dim docs As NotesDocumentCollection
Dim doc As NotesDocument
Dim noteId As String

Set docs = db.Search("@All", 10)
If docs.Count > 0 Then
    noteId = docs(0)
    Set doc = db.GetDocumentByNoteId(noteId)
End If
```

## NotesDocument

A `NotesDocument` owns an open native note handle and is created from a database, view navigation, or another Notes API returning a document.

### Properties

| Property | Type | Access | Description |
| --- | --- | --- | --- |
| `NoteId` | Long-compatible numeric value | read-only to XPscript | Native Note ID value. |
| `NoteIdHex` | String | read-only | Note ID formatted as 8 hexadecimal digits. |
| `UniversalId` | String | read-only | Document Universal ID. |
| `Items` | String array | read-only | Array containing the name of every item in native item order. Duplicate item names are preserved. No `NotesItem` wrappers are allocated by this property. |
| `IsRecycled` | Boolean | read-only | `True` after the wrapper has been recycled. |

### Functions and methods

| Member | Return type | Description |
| --- | --- | --- |
| `GetFirstItem(itemName)` | `NotesItem`, `NotesRichTextItem`, or `Nothing` | Returns the first matching item. Composite items are returned as `NotesRichTextItem`. |
| `CreateNotesItem(itemName)` | `NotesItem` | Creates an empty text item and returns its wrapper. Item name cannot be empty. |
| `ReplaceItemValue(itemName, value)` | `NotesItem` | Creates the item if necessary, replaces its value, and returns the resulting item wrapper. |
| `HasItem(itemName)` | Boolean | Tests whether an item exists. |
| `GetValue(itemName)` | Variant | Returns the native item value through the basic V1 value conversion. |
| `GetItemValue(itemName)` | Variant array | XPscript indexed-value surface. Returns all values as a one-dimensional array. |
| `GetItemValue(itemName)(index)` | Variant | Returns one zero-based value from a text, number, time, or corresponding list/range item. Out-of-range access raises runtime error 9. |
| `GetString(itemName)` | String | Returns a text value. |
| `GetNumber(itemName)` | Double | Returns a numeric value. |
| `GetDateTime(itemName)` | `NotesDateTime` | Returns a Notes time/date item as a new `NotesDateTime` wrapper. |
| `SetValue(itemName, value)` | Void | Writes a value using the generic native item setter. |
| `SetString(itemName, value)` | Void | Writes a text item. |
| `SetNumber(itemName, value)` | Void | Writes a numeric item. |
| `SetDateTime(itemName, value)` | Void | Writes a `NotesDateTime`. Other value types raise runtime error 13. |
| `RemoveItem(itemName)` | Void | Removes an item by name. |
| `SaveAttachment(attachmentName, path)` | Boolean | Extracts an attachment from the document to `path`. Returns `False` when it cannot be saved. |
| `Save()` | Void | Saves the note and refreshes its Note ID. |
| `Recycle()` | Void | Closes the native note handle. |

### Item-name rules

Item names used for creation and replacement cannot be empty. Indexed `GetItemValue` access also rejects `Nothing` and empty item names with runtime error 5. Values themselves may be empty.

### Generic item values

`ReplaceItemValue` and `NotesItem.Values` currently support scalar values and one-dimensional arrays of homogeneous text, numeric, or `NotesDateTime` values. Mixed arrays and unsupported value kinds raise runtime error 13. An empty or unallocated array writes an empty text value.

Current numeric handling accepts XPscript/.NET numeric scalar types and writes list values as Notes number lists. Arrays passed to the setter must be one-dimensional.

## NotesItem

`NotesItem` is a wrapper over an item in a `NotesDocument`. It does not own a separate native note handle. Recycling an item wrapper invalidates that wrapper but does not recycle its parent document.

### Properties

| Property | Type | Access | Description |
| --- | --- | --- | --- |
| `Parent` | `NotesDocument` | read-only | Parent document. |
| `Name` | String | read-only | Actual item name. |
| `DateTimeValue` | `NotesDateTime` or `Nothing` | read/write | Returns a date/time wrapper when the item type is a Notes time value. Setting requires a `NotesDateTime`. |
| `IsAuthors` | Boolean | read/write | Authors-item flag state. Enabling it also enables the names and summary flags and clears readers. |
| `IsEncrypted` | Boolean | read/write | Sealed/encrypted item flag. |
| `IsNames` | Boolean | read/write | Names-item flag. Enabling also enables summary. Disabling clears names, readers, and authors flags. |
| `IsProtected` | Boolean | read/write | Protected item flag. |
| `IsReaders` | Boolean | read/write | Readers-item flag state. Enabling also enables names and summary and clears authors. |
| `IsSigned` | Boolean | read/write | Signed item flag. |
| `IsSummary` | Boolean | read/write | Summary item flag. |
| `LastModified` | `NotesDateTime` | read-only | Item modified time as a new `NotesDateTime` wrapper. |
| `Text` | String | read-only | Item converted to text through the Notes C API. |
| `Type` | Integer | read-only | LotusScript-compatible item type code where the runtime has a mapping. |
| `ValueLength` | Long | read-only | Native item value length. |
| `Values` | Variant array | read/write | All item values. Reading returns a one-dimensional XPscript array. Writing accepts the homogeneous V1 value types described above. |
| `IsRecycled` | Boolean | read-only | `True` after the wrapper has been recycled. |

`Values(index)` is supported for a typed `NotesItem`/`NotesRichTextItem` variable. It returns the zero-based indexed value directly without first materializing the full value array.

When an item has the Notes names flag, `Values` and indexed `Values(index)` convert text values to `NotesName` wrappers.

### Type values

The current `Type` mapping includes common Notes/LotusScript-compatible values such as:

| Value | Meaning |
| ---: | --- |
| `1` | Rich text/composite |
| `768` | Number or number range |
| `1024` | Date/time or time range |
| `1074` | Names |
| `1075` | Readers |
| `1076` | Authors |
| `1085` | Object |
| `1280` | Text or text list |
| `1282` | RFC822 text |
| `1536` | Formula |
| `1792` | User ID |

Other native Notes item types are mapped when the runtime recognizes them; unknown types return `0`.

### Functions and methods

| Member | Return type | Description |
| --- | --- | --- |
| `Remove()` | Void | Removes the represented item from its parent document. Subsequent use of that wrapper raises runtime error 91. |
| `CopyToDocument(document)` | `NotesItem` | Copies the item to another document using the same item name. |
| `CopyToDocument(document, newName)` | `NotesItem` | Copies the item and optionally changes its name. An empty `newName` keeps the original name. |
| `Recycle()` | Void | Invalidates the wrapper. |

## NotesRichTextItem

`NotesRichTextItem` is returned by `GetFirstItem` when the native item type is composite/rich text. It inherits all `NotesItem` properties and methods.

### Additional functions and methods

| Member | Return type | Description |
| --- | --- | --- |
| `GetUnformattedText()` | String | Converts the rich-text item to unformatted text through the native C API. |
| `SaveAttachment(attachmentName, path)` | Boolean | Extracts an attachment associated with this rich-text item to `path`. |

```xpscript
Dim item As NotesItem
Dim richText As NotesRichTextItem

Set item = doc.GetFirstItem("Body")
If item <> Nothing Then
    If item.Type = 1 Then
        Set richText = item
        Print richText.GetUnformattedText()
    End If
End If
```

## NotesName

Create a name from `NotesSession.CreateName`.

```xpscript
Dim name As NotesName
Set name = session.CreateName("CN=Ada Lovelace/O=Example")
```

### Properties

| Property | Type | Access | Description |
| --- | --- | --- | --- |
| `Parent` | `NotesSession` | read-only | Owning session. |
| `Source` | String | read-only | Original trimmed input text. |
| `Canonical` | String | read-only | Native canonical Notes name. |
| `Abbreviated` | String | read-only | Native abbreviated Notes name. |
| `IsHierarchical` | Boolean | read-only | `True` when the canonical representation is hierarchical. |
| `Common` | String | read-only | `CN` component. |
| `Country` | String | read-only | `C` component. |
| `Organization` | String | read-only | `O` component. |
| `OrgUnit1` | String | read-only | First `OU` component. |
| `OrgUnit2` | String | read-only | Second `OU` component. |
| `OrgUnit3` | String | read-only | Third `OU` component. |
| `OrgUnit4` | String | read-only | Fourth `OU` component. |
| `ADMD` | String | read-only | `A` component. |
| `PRMD` | String | read-only | `P` component. |
| `Addr821` | String | read-only | Parsed Internet/RFC821-style address when present. |
| `Addr822LocalPart` | String | read-only | Parsed local part of an Internet address. |
| `Addr822Phrase` | String | read-only | Parsed display phrase from an Internet address. |
| `IsRecycled` | Boolean | read-only | `True` after the wrapper has been recycled. |

### Methods

`NotesName` currently adds no methods beyond `Recycle()`.

## NotesDateTime

`NotesDateTime` stores the native C API `TIMEDATE` representation.

```xpscript
Dim value As NotesDateTime
Set value = session.CreateDateTime("2026-08-25 12:00:00")
Call value.AdjustDay(1)
Print value.LocalTime
```

### Properties

| Property | Type | Access | Description |
| --- | --- | --- | --- |
| `Parent` | `NotesSession` | read-only | Owning session. |
| `IsValidDate` | Boolean | read-only | Currently returns `True` for a successfully constructed Notes date/time. |
| `IsDST` | Boolean | read-only | Daylight-saving indicator returned by native time expansion. |
| `TimeZone` | Integer | read-only | Notes time-zone value returned by native time expansion. |
| `LocalTime` | String | read-only | Native-formatted local date/time. |
| `GMTTime` | String | read-only | Native-expanded GMT date/time. |
| `ZoneTime` | String | read-only | Native-expanded date/time including the current Notes zone interpretation. |
| `DateOnly` | String | read-only | `YYYY-MM-DD` from the expanded local time. |
| `TimeOnly` | String | read-only | `HH:MM:SS` from the expanded local time. |
| `IsRecycled` | Boolean | read-only | `True` after the wrapper has been recycled. |

### Functions and methods

| Member | Return type | Description |
| --- | --- | --- |
| `AdjustSecond(amount)` | Void | Adds or subtracts seconds. |
| `AdjustMinute(amount)` | Void | Adds or subtracts minutes. |
| `AdjustHour(amount)` | Void | Adds or subtracts hours. |
| `AdjustDay(amount)` | Void | Adds or subtracts days. |
| `AdjustMonth(amount)` | Void | Adds or subtracts months. |
| `AdjustYear(amount)` | Void | Adds or subtracts years. |
| `Recycle()` | Void | Invalidates the wrapper. |

## NotesAgent

Represents an agent design element returned by `NotesDatabase.GetAgent` or `NotesDatabase.Agents`.

### Properties

`Parent`, `Name`, `Owner`, `CommonOwner`, `Comment`, `Query`, `ServerName`,
`ParameterDocID`, `IsNotesAgent`, `IsPublic`, `HasRunSinceModified`, `IsEnabled`,
`Trigger`, `NotesURL`, and `OnBehalfOf` are exposed.

### Methods

`Run`, `RunWithDocumentContext`, `RunOnServer`, `Save`, `Remove`, and `UnLock` are exposed. `RunWithDocumentContext`
passes an open `NotesDocument` as the agent document context. `RunOnServer`
accepts an optional document Note ID and passes that document as context.
`Remove`
permanently deletes the agent design note when the caller has permission.

## NotesAgentResult

Returned by `NotesDatabase.RunAgent`.

### Properties

| Property | Type | Access | Description |
| --- | --- | --- | --- |
| `Success` | Boolean | read-only | Currently `True` for a successfully returned agent run result. Native execution failures raise an XPscript runtime error instead of returning a failed result object. |
| `Status` | Integer | read-only | Currently `0` for a successfully returned agent run result. |
| `Output` | String | read-only | Captured agent stdout/output text. |
| `IsRecycled` | Boolean | read-only | `True` after the wrapper has been recycled. |

### Methods

`NotesAgentResult` currently adds no methods beyond `Recycle()`.

## Object creation rules

Only `NotesSession` is constructed with `New`.

These classes must be obtained from the owning Notes object:

- `NotesDatabase` from `NotesSession.OpenDatabase` or `OpenByReplicaID`
- `NotesView` from `NotesDatabase.OpenView`
- `NotesDocumentCollection` from searches or view lookups
- `NotesDocument` from `NotesDatabase`, `NotesView`, or a Note ID returned by a collection
- `NotesItem` and `NotesRichTextItem` from `NotesDocument`
- `NotesName` from `NotesSession.CreateName`
- `NotesDateTime` from `NotesSession.CreateDateTime`, `CreateDateTimeNow`, document/item time access, or item metadata
- `NotesAgentResult` from `NotesDatabase.RunAgent`

Attempting to use `New` with the other Notes classes is rejected by the compiler.

## Recycling and ownership

`Recycle()` is idempotent. A session tracks its live children and recycles them before calling `NotesTerm` and unloading the native library. A database similarly recycles its owned views, documents, result collections, and agent result objects before `NSFDbClose`.

Native resource ownership remains inside the generated runtime:

- database handles are closed with `NSFDbClose`;
- note handles are closed with `NSFNoteClose`;
- NIF collection handles are closed with `NIFCloseCollection`;
- formula, full-text, and other movable Notes memory handles are released through the matching native memory functions;
- movable Notes memory is accessed only while locked;
- pointers obtained from locked Notes memory are not stored in public wrapper objects;
- view/search result Note IDs are copied into managed arrays before native result memory is released;
- `NotesDocument.Items` copies item names to an XPscript string array and leaves note-owned item blocks under Notes ownership;
- agent stdout is copied while its native memory is valid.

When a typed Notes variable is reassigned with `Set`, XPscript evaluates the replacement first and then recycles the previous wrapper unless both references are the same object. This allows patterns such as:

```xpscript
Set doc = view.GetNextDocument(doc)
```

The next document is resolved before the previous `doc` wrapper is recycled.

## Error handling

Notes wrapper validation and native C API failures are surfaced as `XPScriptRuntimeException` values and therefore participate in normal XPscript error handling.

```xpscript
On Error GoTo Handler

Set doc = db.GetDocumentByUNID("0123456789ABCDEF0123456789ABCDEF")
Exit Sub

Handler:
Print "Error " & CStr(Err) & ": " & Error$
```

Common wrapper-generated errors include runtime error 5 for invalid or empty arguments, runtime error 9 for indexed value access outside its valid range, runtime error 13 for type mismatches, and runtime error 91 for recycled or removed Notes wrappers.

## Validation samples

The repository includes:

- [`notes-c-api-v1.xps`](../samples/notes-c-api-v1.xps), a manual runtime validation program for a machine with Notes/Domino installed.
- [`notes-c-api-surface.xps`](../samples/notes-c-api-surface.xps), a compile-only surface probe whose `Main` routine does not initialize Notes.
- [`notes-database-properties-console.xps`](../samples/notes-database-properties-console.xps), a focused database property sample.
- [`notes-view-autoupdate-console.xps`](../samples/notes-view-autoupdate-console.xps), a focused view navigation and `AutoUpdate` sample.
