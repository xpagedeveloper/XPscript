# HCL Notes wrapper TODO

This tracks functionality still missing or needing validation in the XPscript HCL Notes/Domino C API wrapper.

## Post-merge compiler fixes

- Improve generated-code diagnostics so normal compiler output maps errors back to the original `.xps` source line instead of exposing `Program.cs` line numbers. Add a new `--debug` option that preserves/shows generated `Program.cs` diagnostics and line numbers for compiler/runtime debugging, while default `--info` output remains source-mapped to `.xps`.

## Build and runtime validation

- Add a compiler-only probe that transpiles and compiles the generated Notes runtime without requiring an HCL installation.
- Run the manual V1 sample against an installed HCL Notes client on Windows.
- Validate all 32/64-bit ABI assumptions against the current HCL C API headers used by the installed client.
- Verify LMBCS translation constants and Unicode/path behavior with non-ASCII names and paths.
- Verify NotesSession initialization with default `notes.ini` and explicit `notes.ini` paths.
- Verify graceful shutdown/recycle with many child objects and error paths.
- Manually validate `NotesInitThread`/`NotesTermThread` behavior under multi-threaded .NET hosts and Domino server runtime conditions.

## Domino server integration

Current target: standalone XPscript processes running with a Domino server runtime, server `notes.ini`, and the configured server ID file.

Very low priority / future:

- Native Domino server add-in task mode, including `AddInMain`, `AddInShouldTerminate`, server task lifecycle, `load xpscript`, `tell xpscript ...`, console logging and controlled shutdown. This is intentionally lower priority than validating the standalone server process model.

## NotesSession

Implemented: `Username`, `CommonUsername`, `NotesVersion`, `NotesBuildVersion`, `OpenDatabase`, `OpenByReplicaID`, `CreateName`, `CreateDateTime`, `CreateDateTimeNow`.

Missing / future:

- Additional LotusScript NotesSession properties and methods beyond V1.
- Formula object/caching support for repeated `NSFSearch` formulas.
- Additional ID/security/session operations if needed.

## NotesDatabase

Implemented properties: `Parent`, `Server`, `FilePath`, `FileName`, `IsOpen`, `Title`, `Categories`, `TemplateName`, `DesignTemplateName`, `ReplicaID`, `Size`, `PercentUsed`, `CurrentAccessLevel`, `Created`, `LastModified`, `FileFormat`, `IsFTIndexed`, and `LastFTIndexed`.

Implemented methods: `OpenView`, `GetDocumentByNoteId`, `GetDocumentByUNID`, `CreateDocument`, `CreateDocumentCollection`, `GetProfileDocument`, `Search`, `FTSearch`, `GetModifiedDocuments`, `RunAgent`, `Create`, `Remove`, `CreateCopy`, `SetReplicaId`, `RemoveFTIndex`, and `Recycle`.

Missing / future:

- Additional ACL/database access properties and ACL manipulation.
- Database compact/fixup/replication/admin operations.
- More complete agent options and execution context handling.

## NotesView

Implemented: `Name`, `ColumnNames`, `GetDocumentByKey`, `GetAllDocumentsByKey`, `GetFirstDocument`, `GetLastDocument`, `GetNextDocument`, `GetPrevDocument`, `FTSearch`, `Refresh`, `CreateViewEntryCollection`, `CreateViewNav`, view entry/category navigation, `AutoUpdate`, `MarkAllRead`, `MarkAllUnread`, and `Recycle`.

Missing / future:

- Native numeric/date multi-column key lookup and complete `NIFFindByKey` compatibility remain. Managed array view keys now compare text, numeric, and `NotesDateTime` values across multiple columns and multi-valued column results. The native completion still requires an `ITEM_TABLE` key buffer, sorted-column order, and the `FIND_PARTIAL`/case-sensitivity flags described by the [HCL NIFFindByKey documentation](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Func/NIFFindByKey/).
- Exact validation of all `NIFFindByKey`/collation semantics.

## NotesDocumentCollection

Implemented as a lightweight NOTEID collection. `GetFirstDocument`, `GetNextDocument`, `GetDocument`, `For Each`, `Count`, `Clone`, `RemoveAll`, `Merge`, `Intersect`, `Subtract`, folder operations, and full-text filtering are supported.

Missing / future:

- Optional lazy paging for extremely large result sets if retaining all NOTEIDs becomes material.

## NotesDocument

Implemented: open by NOTEID/UNID, item reads/writes, `GetFirstItem`, `ReplaceItemValue`, `CreateNotesItem`, `SaveAttachment`, `Save`, `Recycle`, UNID/NOTEID properties, response/parent relations, document metadata, and copy/folder/read-state operations.

Missing / future:

- Encrypt-on-send and additional note metadata.
- MIME support is implemented for native `Body` entities, direct and nested child traversal, MIME content read/write, standard header parameters, arbitrary header enumeration, and MIME directory lifecycle. See [notes-mime-entity.md](../docs/notes-mime-entity.md) and `samples/notes-mime-entity-surface.xps`. Named MIME items other than `Body` remain unsupported.
- Embedded object APIs beyond attachment extraction.
- Full LotusScript-compatible `GetItemValue` semantics for all native item types.

## NotesItem

Implemented: `DateTimeValue`, `IsAuthors`, `IsEncrypted`, `IsNames`, `IsProtected`, `IsReaders`, `IsSigned`, `IsSummary`, `LastModified`, `Name`, `Text`, `Type`, `ValueLength`, `Values`, `Remove`, `CopyToDocument`.

Missing / future:

- Full support for every Notes item datatype in `Values`.
- Object, signature and userdata-specific typed wrappers.
- Extend rich-text/composite value coverage beyond the currently supported wrapper operations.

## NotesRichTextItem

Implemented: inherited `NotesItem` surface, direct creation, `AppendText`, styled text and paragraph operations, table creation, attachment embedding/extraction, embedded-object enumeration, `ConvertToHTML`, navigator/range operations, and `SaveAttachment`.

Missing / future:

- Append doclinks and sections.
- Mutate existing table rows and table formatting through the high-level wrapper.
- Full CD-record traversal exposed as higher-level XPscript objects if needed.

## NotesName

Implemented name parsing, canonical/abbreviated values, hierarchical components, and RFC821/RFC822 address fields.

Missing / future:

- Language-specific parsing and full native multi-OU semantics remain to be validated on additional Notes locales.

## NotesDateTime

Implemented native date/time creation, formatting, zone expansion, adjustment, wildcard, difference, and zone-conversion methods.

Missing / future:

- Validate `TimeZone` sign/semantics against LotusScript on actual Notes clients.
- Wildcard date/time behavior is implemented through `SetAnyDate`, `SetAnyTime`, and empty-value construction.
- Locale-specific parsing/formatting compatibility.

## Agents

Implemented synchronous agent lookup, enumeration, execution with optional document context, server execution, state properties, save/remove, and stdout capture.

Missing / future:

- Validate redirect constants/signatures on supported Notes versions.
- Additional run flags/security options and detailed result/status information.
- Agent timeout/cancellation strategy if needed.

## Search and FT search

Implemented formula `Search` and `FTSearch`, returning NOTEID-only collections.

Missing / future:

- Additional NSFSearch flags/options and since-time searching.
- FT search options, scores, sorting, highlight data and index-state information.
- More explicit handling/testing of all normal no-match statuses.
- Performance/paging tests with very large result sets.

## Memory and native interop

- Never retain pointers returned from movable Notes memory beyond the lock scope.
- Every `OSLockObject` must be paired with `OSUnlockObject`.
- Free only memory owned by the wrapper with the appropriate HCL API.
- Do not free note-owned `BLOCKID` memory.
- Re-resolve item metadata after item mutation rather than retaining stale `BLOCKID`s.
- Continue auditing handle widths separately: DB/NOTE/DHANDLE, HCOLLECTION, agent/context pointers and raw memory addresses are not interchangeable.
