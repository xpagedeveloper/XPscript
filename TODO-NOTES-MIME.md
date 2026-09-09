# NotesMIMEEntity implementation TODO

Branch: `feature/notes-mime-entity`

## Done

- [x] Native MIME directory ownership and document lifecycle integration
- [x] Root entity and MIME tree traversal through Domino MIME directory APIs
- [x] Native content type, subtype, charset and boundary metadata reads
- [x] `CreateMIMEEntity("Body")` creates a native `TYPE_MIME_PART`
- [x] MIME stream writer mirrors the JNX BODY flow through a temporary note, `MIMEStreamItemize(full)`, then copies `Body` and `$file` items
- [x] Body-only guards for note-level MIME directory access
- [x] Compile-time Notes MIME surface sample
- [x] Root-entity `SetContentFromText` writes through the MIME stream/itemize writer
- [x] Root-entity `SetContentFromBytes` uses the same writeback path
- [x] Cached MIME directory is invalidated before root itemization so stale native entity handles are closed immediately
- [x] The mutating root wrapper rebinds to a fresh MIME directory/root entity after successful writeback
- [x] Save/reopen regression coverage calls `SetContentFromText` with UTF-8 text
- [x] Regression checks immediate content type/subtype and charset metadata after mutation
- [x] Regression checks content type/subtype and charset after save/reopen
- [x] Child-entity mutation remains explicitly unsupported
- [x] Added `docs/notes-mime-entity.md` for native MIME behavior, mutation and lifecycle
- [x] Updated `skills/xpscript-programming/SKILL.md` with the supported Notes MIME coding pattern
- [x] Compiler project builds successfully with the new postprocessor stage
- [x] macOS CI passes `Compile Notes MIME surface`
- [x] Runtime Placeholder Guard CI passes
- [x] Documentation site CI passes
- [x] Reviewed branch CI status after the source/doc updates

## Remaining

- [ ] Add root content readback support before asserting written body bytes/text after save/reopen
- [ ] Run the MIME runtime sample against an installed Domino runtime and confirm every PASS/FAIL assertion
- [ ] Recheck the full Compile workflow after the existing Linux `native-csv-regression.xps` compile failure is resolved; Linux currently fails before reaching the Notes MIME compile step
