# HCL Notes wrapper TODO

This tracks functionality still missing or needing validation in the XPscript HCL Notes/Domino C API wrapper.

## Build and runtime validation

- Add a compiler-only probe that transpiles and compiles the generated Notes runtime without requiring an HCL installation.
- Run the manual V1 sample against an installed HCL Notes client on Windows.
- Validate all 32/64-bit ABI assumptions against the current HCL C API headers used by the installed client.
- Verify LMBCS translation constants and Unicode/path behavior with non-ASCII names and paths.
- Verify NotesSession initialization with default `notes.ini` and explicit `notes.ini` paths.
- Verify graceful shutdown/recycle with many child objects and error paths.

## NotesSession

Implemented: `Username`, `CommonUsername`, `NotesVersion`, `NotesBuildVersion`, `OpenDatabase`, `OpenByReplicaID`, `CreateName`, `CreateDateTime`, `CreateDateTimeNow`.

Missing / future:

- Additional LotusScript NotesSession properties and methods beyond V1.
- Formula object/caching support for repeated `NSFSearch` formulas.
- Additional ID/security/session operations if needed.

## NotesDatabase

Implemented properties: `Parent`, `Server`, `FilePath`, `FileName`, `IsOpen`, `Title`, `Categories`, `TemplateName`, `DesignTemplateName`, `ReplicaID`, `Size`, `PercentUsed`, `CurrentAccessLevel`.

Implemented methods: `OpenView`, `GetDocumentByNoteId`, `GetDocumentByUNID`, `Search`, `FTSearch`, `RunAgent`, `Recycle`.

Missing / future:

- Create new documents.
- Delete documents.
- Additional ACL/database access properties and ACL manipulation.
- Database modified time, created time and additional database metadata.
- Database compact/fixup/replication/admin operations.
- Profile documents.
- Folder operations.
- Additional full-text index management and FT options.
- More complete agent options and execution context handling.

## NotesView

Implemented: `Name`, `GetFirstDocumentByKey`, `GetAllDocumentsByKey`, `FTSearch`, `Refresh`, `Recycle`.

Missing / future:

- Column metadata and column values.
- General view navigation (`GetFirstDocument`, next/previous, entries, categories).
- Multi-column/multi-value key lookup compatible with LotusScript behavior.
- Exact validation of all `NIFFindByKey`/collation semantics.
- View entry objects and category entries.

## NotesDocumentCollection

Implemented as a lightweight NOTEID collection. `docs(i)`, `Get(i)` and `For Each` return NOTEID strings; `Count`, `UBound`, `LBound` are supported.

Missing / future:

- Additional collection operations if needed (contains, remove, intersect/merge, sorting).
- Optional lazy paging for extremely large result sets if retaining all NOTEIDs becomes material.

## NotesDocument

Implemented: open by NOTEID/UNID, item reads/writes, `GetFirstItem`, `ReplaceItemValue`, `CreateNotesItem`, `SaveAttachment`, `Save`, `Recycle`, UNID/NOTEID properties.

Missing / future:

- Create a new document from `NotesDatabase`.
- Delete document.
- Response/parent document relationships.
- Additional note metadata (created, last modified, signer, encrypt-on-send, etc.).
- MIME support.
- Embedded object APIs beyond attachment extraction.
- Full LotusScript-compatible `GetItemValue` semantics for all native item types.

## NotesItem

Implemented: `DateTimeValue`, `IsAuthors`, `IsEncrypted`, `IsNames`, `IsProtected`, `IsReaders`, `IsSigned`, `IsSummary`, `LastModified`, `Name`, `Text`, `Type`, `ValueLength`, `Values`, `Remove`, `CopyToDocument`.

Missing / future:

- Full support for every Notes item datatype in `Values`.
- Rich text/composite values beyond `NotesRichTextItem` operations.
- MIME/object/signature/userdata-specific typed wrappers.
- Validate all writable item flags against HCL runtime behavior.

## NotesRichTextItem

Implemented: `GetUnformattedText`, `SaveAttachment` for attachments referenced by that rich-text item.

Missing / future:

- Create rich-text items directly.
- Append text, paragraphs, doclinks, tables and sections.
- Embed/attach files.
- Enumerate attachments/embedded objects.
- Rich-text conversion/export APIs.
- Full CD-record traversal exposed as higher-level XPscript objects if needed.

## NotesName

Implemented V1 name parsing/canonical/abbreviated surface.

Missing / future:

- Verify every property against LotusScript `NotesName`, including RFC822-specific fields and multi-OU behavior.
- Replace remaining managed approximations with native name services where that improves compatibility.

## NotesDateTime

Implemented V1 date/time properties and session creation.

Missing / future:

- Validate `TimeZone` sign/semantics against LotusScript on actual Notes clients.
- AnyDate/AnyTime behavior.
- Zone conversion and additional LotusScript methods.
- Locale-specific parsing/formatting compatibility.

## Agents

Implemented synchronous agent execution with optional document context and stdout capture.

Missing / future:

- Validate redirect constants/signatures on supported Notes versions.
- Additional run flags/security options.
- More detailed result/status information.
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
