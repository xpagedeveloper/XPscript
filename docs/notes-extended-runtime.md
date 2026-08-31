# Notes extended runtime samples

The Notes runtime regression samples include:

- [`samples/notes-note-collection-runtime-test.xps`](../samples/notes-note-collection-runtime-test.xps) for NotesNoteCollection selection, iteration and set operations.
- [`samples/notes-note-collection-dxl-runtime-test.xps`](../samples/notes-note-collection-dxl-runtime-test.xps) for using NotesNoteCollection directly as NotesDXLExporter input.
- [`samples/notes-stream-runtime-test.xps`](../samples/notes-stream-runtime-test.xps) for NotesStream memory/file text I/O.
- [`samples/notes-agent-runtime-test.xps`](../samples/notes-agent-runtime-test.xps) for NotesDatabase.GetAgent and NotesAgent execution/properties.
- [`samples/notes-document-design-metadata-runtime-test.xps`](../samples/notes-document-design-metadata-runtime-test.xps) for NotesDocument IsValid, IsProfile, Items and XPscript design-element metadata.
- [`samples/notes-extended-surface.xps`](../samples/notes-extended-surface.xps) for cross-platform compile-time surface coverage.

## NotesDocument design metadata

XPscript exposes the read-only NotesDocument properties `IsDesign`, `DesignType`, `DesignTitle` and `DesignAlias` in addition to the LotusScript-compatible `IsValid` and `IsProfile` properties.

`IsDesign` is true for Domino design notes. `DesignType` uses the native note class and `$Flags` to distinguish common design types including Form, Subform, Page, Frameset, View, Folder, Navigator, Agent, ScriptLibrary, DatabaseScript, SharedField, SharedColumn, resources, XPage and CustomControl. Non-design documents return `False` from `IsDesign` and empty strings from all three design string properties.

`DesignTitle` and `DesignAlias` are derived from `$TITLE`. XPscript supports both Domino text-list names and pipe-separated names: the first name is the title, and remaining names are exposed by `DesignAlias` joined with `|`.

The public XPscript API follows the LotusScript object model. Backend implementation details may use XPscript-native collections, streams and Domino C API primitives where that produces simpler and safer runtime code.
