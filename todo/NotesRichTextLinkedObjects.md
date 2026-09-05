# Notes rich-text linked objects

Status for LotusScript-compatible rich-text linked objects in XPScript.

Primary references:

- HCL Domino Designer LotusScript classes
- HCL Domino C API
- HCL Domino JNX rich-text and attachment implementations

## Implemented foundation

- [x] `NotesRichTextItem` is the owner/hub for linked rich-text objects.
- [x] Managed CD-record snapshots. Native pointers from `EnumCompositeBuffer` are never retained.
- [x] Record positions use logical record indexes instead of native addresses.
- [x] Rich-text revision tracking on `NotesRichTextItem`.
- [x] Unknown CD records are retained as raw bytes in the record model.
- [x] `NotesRichTextStyle` managed state object.
- [x] `NotesRichTextParagraphStyle` initialized through `CompoundTextInitStyle`.
- [x] `NotesRichTextTab`.
- [x] `NotesRichTextItem.CreateNavigator()`.
- [x] Navigator core search for elements and strings.
- [x] `NotesRichTextItem.CreateRange()`.
- [x] Range position model, `Navigator`, `Style`, `TextParagraph`, `TextRun`, `Type`, `Clone`, `Reset`, `SetBegin`, `SetEnd`.
- [x] Compiler type recognition for the initial eight rich-text linked object types.

## NotesEmbeddedObject and attachments

- [x] Add `NotesEmbeddedObject` to the Notes compiler type model with `Set`, `Nothing`, replacement, and `Recycle()` semantics.
- [x] Implement attachment-backed `NotesEmbeddedObject` properties: `FileCreated`, `FileEncoding`, `FileModified`, `FileSize`, `Name`, `Parent`, `Source`, `Type`.
- [x] Implement attachment `ExtractFile`.
- [x] Implement XPScript extension `ToByteArray()` without a temporary file.
- [x] Use `NSFNoteCipherExtractWithCallback` for normal attachment reads so compressed attachments are streamed/decompressed by Notes.
- [x] Implement `NotesRichTextItem.GetEmbeddedObject(name)`.
- [x] Implement `NotesRichTextItem.EmbeddedObjects`.
- [x] Materialize attachment elements from `NotesRichTextNavigator.GetElement()` as `NotesEmbeddedObject`.
- [x] Implement `NotesRichTextItem.EmbedObject(EMBED_ATTACHMENT, "", source [, name])`.
- [x] Attach physical file data with `NSFNoteAttachFile` and add the corresponding `HOTSPOTREC_TYPE_FILE` CD hotspot to the rich-text item.
- [x] Roll back the `$FILE` object if adding the rich-text hotspot fails.
- [ ] Implement attachment `Remove()` atomically: remove the rich-text hotspot and then detach/deallocate the `$FILE` object.

## Binary array support

- [x] Add an internal XPScript binary-array construction path for attachment data.
- [x] Keep normal LotusScript array declaration/ReDim bounds compatible with LotusScript.
- [x] Allow API-produced binary arrays to represent attachments larger than the normal LotusScript subscript limit.
- [x] Use byte-backed `XPScriptBinaryArray` storage instead of boxing attachment bytes into `LSArray`.
- [x] Route Variant indexing plus `LBound` and `UBound` through `LSDynamicIndexRuntime`.
- [ ] Add regression coverage for empty, small, >32 KiB, multi-megabyte, compressed, and incompressible attachments.

## NotesRichTextNavigator

- [x] `GetElement` for attachment elements.
- [x] `GetFirstElement` for materializable element types.
- [x] `GetLastElement` for materializable element types.
- [x] `GetNextElement` for materializable element types.
- [x] `GetNthElement` for materializable element types.
- [x] `SetPosition`.
- [x] `SetPositionAtEnd`.
- [x] Correct logical grouping of the currently materialized element types instead of treating records inside structural spans as separate LotusScript elements.
- [x] Correct text-run and text-paragraph boundaries across the flattened CD record stream and physical composite item segments.
- [x] Attachment materialization through `NotesEmbeddedObject`.

## NotesRichTextRange

- [ ] Parse actual text style from CD records for `Style` instead of returning a default style object.
- [ ] Match LotusScript `Type` semantics for mixed and homogeneous ranges.
- [ ] `FindAndReplace` including Notes-compatible options where the C API exposes equivalent behavior.
- [ ] `Remove`.
- [ ] `SetStyle`.
- [ ] Preserve unaffected and unknown CD records byte-for-byte during range mutations.

## Rich-text editor/rewrite layer

- [ ] Build a shared CD transformation pipeline used by Range, Table, Section, DocLink, attachment removal, and insertion.
- [ ] Write transformed records with `CompoundTextAddCDRecords` rather than holding native CD pointers.
- [ ] Preserve all unknown records and unknown flag bits.
- [ ] Handle rich-text split across multiple physical items with the same item name.
- [ ] Invalidate or safely re-resolve linked objects after structural mutation using the rich-text revision.
- [ ] Add rollback/error handling so failed writes do not leave partially modified rich text.

## NotesRichTextSection

- [ ] Materialize sections from the correct CD span.
- [ ] `BarColor`.
- [ ] `IsExpanded`.
- [ ] `Title`.
- [ ] `TitleStyle`.
- [ ] `Remove`.
- [ ] `SetBarColor`.
- [ ] `SetTitleStyle`.
- [ ] `NotesRichTextItem.BeginSection` / `EndSection`.

## NotesRichTextTable

- [ ] Parse complete table spans including `CDPRETABLEBEGIN`, `CDTABLEDATAEXTENSION`, `CDTABLEBEGIN`, `CDTABLECELL`, `CDTABLEEND`, rows, cells, and nested tables.
- [ ] `AlternateColor`.
- [ ] `Color`.
- [ ] `ColumnCount`.
- [ ] `RightToLeft` after validating current Notes C API record mapping.
- [ ] `RowCount`.
- [ ] `RowLabels`.
- [ ] `Style`.
- [ ] `AddRow`.
- [ ] `Remove`.
- [ ] `RemoveRow`.
- [ ] `SetAlternateColor`.
- [ ] `SetColor`.
- [ ] `NotesRichTextItem.AppendTable`.

## NotesRichTextDocLink

- [ ] Materialize both `CDLINKEXPORT2` and normal Notes `CDLINK2` links.
- [ ] Resolve `CDLINK2` linkage against `$Links`.
- [ ] `DbReplicaID`.
- [ ] `DisplayComment`.
- [ ] `DocUNID`.
- [ ] `HotSpotText`.
- [ ] `HotSpotTextStyle`.
- [ ] `ServerHint`.
- [ ] `ViewUNID`.
- [ ] `Remove`.
- [ ] `RemoveLinkage`.
- [ ] `SetHotSpotTextStyle`.
- [ ] Keep `CDLINK2` and `$Links` synchronized when mutating links.
- [ ] `NotesRichTextItem.AppendDocLink` overloads.

## NotesRichTextItem remaining surface

- [ ] `AppendStyle`.
- [ ] `AppendParagraphStyle`.
- [ ] `AppendTable`.
- [ ] `AppendDocLink` overloads.
- [ ] `BeginInsert`.
- [ ] `EndInsert`.
- [ ] `BeginSection`.
- [ ] `EndSection`.
- [ ] `GetNotesFont`.
- [ ] Correct insertion point semantics across subsequent append operations.

## Supporting Notes types

- [ ] Minimal `NotesColorObject` required by Section and Table.
- [ ] Color conversion and custom RGB behavior matching Notes.
- [ ] Custom font support and `$FONT` / `CDFONTTABLE` / `CDFACE` maintenance.

## Verification

- [x] Cross-platform compiler CI for Navigator/Style/ParagraphStyle/Tab/Range surface.
- [x] Cross-platform compiler CI for `NotesEmbeddedObject` surface.
- [ ] Windows Notes client/runtime integration tests for attachment metadata, `ExtractFile`, and `ToByteArray`.
- [ ] Windows Notes client/runtime integration tests for rich-text reads and mutations.
- [ ] Domino server integration tests where server-only behavior differs.
- [ ] Round-trip tests that compare unaffected CD records before and after mutation.
- [ ] Documents with multiple physical composite segments.
- [ ] Nested tables, sections containing tables, doclinks, images, attachments, and unknown CD records.
- [ ] Multiple attachments with the same original filename.
- [ ] Encrypted/sealed notes where supported.
