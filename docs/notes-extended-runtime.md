# Notes extended runtime samples

The Notes runtime regression samples are executable documentation for the final generated XPscript Notes surface. When a core runtime source and a post-processor differ, the post-processed generated surface is authoritative.

The regression set includes:

- [`samples/notes-full-domino-runtime-test.xps`](../samples/notes-full-domino-runtime-test.xps) for the integrated NotesSession, NotesDatabase, NotesDocument, collection, item, agent, stream, view, DXL and ComputeWithForm surface.
- [`samples/notes-note-collection-runtime-test.xps`](../samples/notes-note-collection-runtime-test.xps) for NotesNoteCollection selection, iteration and set operations.
- [`samples/notes-note-collection-dxl-runtime-test.xps`](../samples/notes-note-collection-dxl-runtime-test.xps) for using NotesNoteCollection directly as NotesDXLExporter input.
- [`samples/notes-stream-runtime-test.xps`](../samples/notes-stream-runtime-test.xps) for NotesStream memory/file text I/O.
- [`samples/notes-agent-runtime-test.xps`](../samples/notes-agent-runtime-test.xps) for NotesDatabase.GetAgent and NotesAgent execution/properties.
- [`samples/notes-document-design-metadata-runtime-test.xps`](../samples/notes-document-design-metadata-runtime-test.xps) for NotesDocument IsValid, IsProfile, Items and XPscript design-element metadata.
- [`samples/notes-extended-surface.xps`](../samples/notes-extended-surface.xps) for cross-platform compile-time surface coverage.

## NotesSession extended surface

The final NotesSession surface includes `ProgramDir`, `IniDir`, `DataDir`, `Platform` and read/write `ConvertMIME` in addition to the core identity/version properties. Session factories include `CreateStream`, `CreateDXLImporter`, `CreateDXLExporter`, `CreateRichTextStyle` and `CreateRichTextParagraphStyle` in addition to the name and date/time factories.

`ConvertMIME` defaults to `True`. When enabled, opening a document converts MIME parts to composite rich text through the native Notes runtime before the document wrapper is returned.

## NotesDocument extended surface

`NotesDocument.Items` returns an array of `NotesItem`/`NotesRichTextItem` wrappers, not item-name strings. `UniversalID` is read/write. The final document surface also includes document metadata and relationship properties such as `Size`, `Responses`, `ParentDocumentUNID`, `Created`, `LastModified`, `LastAccessed`, `IsNewNote`, `IsResponse` and `IsDeleted`.

Implemented document operations include rich-text item creation, item copying, all-item copying, document copying, response creation, folder membership, read/unread state, attachment extraction, ComputeWithForm, save and remove.

## NotesDocument design metadata

XPscript exposes the read-only NotesDocument properties `IsDesign`, `DesignType`, `DesignTitle` and `DesignAlias` in addition to the LotusScript-compatible `IsValid` and `IsProfile` properties.

`IsDesign` is true for Domino design notes. `DesignType` uses the native note class and `$Flags` to distinguish common design types including Form, Subform, Page, Frameset, View, Folder, Navigator, Agent, ScriptLibrary, DatabaseScript, SharedField, SharedColumn, resources, XPage and CustomControl. Non-design documents return `False` from `IsDesign` and empty strings from all three design string properties.

`DesignTitle` and `DesignAlias` are derived from `$TITLE`. XPscript supports both Domino text-list names and pipe-separated names: the first name is the title, and remaining names are exposed by `DesignAlias` joined with `|`.

## Surface audit

`tests/NotesFullRuntimeSurfaceAudit` inspects the final generated Notes runtime rather than an intermediate source fragment. It rejects placeholder-style public implementations and verifies that the public NotesDocument members are represented in the full-surface samples. This is the guard against documentation drifting back to an older partial runtime surface.
