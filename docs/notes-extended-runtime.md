# Notes extended runtime samples

The Notes runtime regression samples include:

- [`samples/notes-note-collection-runtime-test.xps`](../samples/notes-note-collection-runtime-test.xps) for NotesNoteCollection selection, iteration and set operations.
- [`samples/notes-note-collection-dxl-runtime-test.xps`](../samples/notes-note-collection-dxl-runtime-test.xps) for using NotesNoteCollection directly as NotesDXLExporter input.
- [`samples/notes-stream-runtime-test.xps`](../samples/notes-stream-runtime-test.xps) for NotesStream memory/file text I/O.
- [`samples/notes-agent-runtime-test.xps`](../samples/notes-agent-runtime-test.xps) for NotesDatabase.GetAgent and NotesAgent execution/properties.
- [`samples/notes-extended-surface.xps`](../samples/notes-extended-surface.xps) for cross-platform compile-time surface coverage.

The public XPscript API follows the LotusScript object model. Backend implementation details may use XPscript-native collections, streams and Domino C API primitives where that produces simpler and safer runtime code.
