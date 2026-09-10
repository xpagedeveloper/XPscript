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
- [x] Added `docs/notes-mime-entity.md` for native MIME behavior, mutation and lifecycle
- [x] Updated `skills/xpscript-programming/SKILL.md` with the supported Notes MIME coding pattern
- [x] Compiler project builds successfully with the new postprocessor stage
- [x] macOS CI passes `Compile Notes MIME surface`
- [x] Runtime Placeholder Guard CI passes
- [x] Documentation site CI passes
- [x] Reviewed branch CI status after the source/doc updates
- [x] Domino runtime regression completed with exit code 0
- [x] `CreateMIMEEntity("Body")` verified at runtime as `TYPE_MIME_PART` (`BodyType=25`, `NSFNoteHasMIMEPart=True`)
- [x] `SetContentFromText` verified to refresh root metadata immediately to `text/plain`
- [x] UTF-8 charset verified after save/reopen
- [x] MIME Body verified to remain MIME after save/reopen
- [x] Native MIME tree traversal verified on an existing `multipart/mixed` document containing a `text/html; charset=UTF-8` child
- [x] Implemented root `ContentAsText` through the native MIME stream
- [x] Implemented root `GetContentAsText`, `GetContentAsBytes` and `GetEntityAsText`
- [x] Root readback decodes base64 and quoted-printable transfer encodings and uses the native root charset for text
- [x] Extended save/reopen regression to assert the actual UTF-8 payload through property and stream readback APIs
- [x] Updated `docs/notes-mime-entity.md` with root readback behavior and root-only boundary
- [x] Updated `skills/xpscript-programming/SKILL.md` with the supported root readback pattern and child-content boundary
- [x] Updated Domino runtime regression completed with exit code 0
- [x] Immediate root `ContentAsText` verified against the mutated UTF-8 payload
- [x] Save/reopen root `ContentAsText` verified against the UTF-8 payload
- [x] Save/reopen `GetContentAsText` verified against the UTF-8 payload
- [x] Save/reopen `GetContentAsBytes` verified against the UTF-8 payload
- [x] Save/reopen `GetEntityAsText` verified to return root RFC822 data with MIME headers
- [x] Existing multipart MIME traversal still verifies `text/html; charset=UTF-8` after the readback changes
- [x] Added `CreateChildEntity` root surface for direct multipart children
- [x] Added direct-child `SetContentFromText` and `SetContentFromBytes` writeback
- [x] Added direct-child `CreateHeader`/`NotesMIMEHeader.SetHeaderVal` writeback for attachment headers
- [x] Added base64 child transfer encoding through encoding constant `1727`
- [x] Added executable `multipart/mixed` text + base64 attachment regression from `NotesStream`
- [x] Updated Notes MIME documentation and XPscript programming skill for child attachment creation
- [x] Added bounded post-cleanup child-header bridge without restoring the old managed MIME parser
- [x] Regression refreshes the root wrapper after child writeback before further root operations
- [x] Updated Notes MIME attachment sample passes macOS `Compile Notes MIME surface`
- [x] Domino attachment probe identified `MIMEStreamOpen(read)` status `0x3AF9` on a newly-created empty `TYPE_MIME_PART`
- [x] `CreateChildEntity` now treats `ERR_MIME_NO_DATA (0x3AF9)` on a non-multipart new root as an empty bootstrap state and promotes it to `multipart/mixed`
- [x] Domino runtime verifies empty-root bootstrap and promotion to `multipart/mixed`
- [x] Domino runtime verifies the attachment direct child as `application/octet-stream`
- [x] Domino runtime verifies the attachment child survives save/reopen traversal
- [x] Existing known multipart HTML traversal remains green after attachment changes
- [x] Added direct-child `GetNthHeader(name[, occurrence])`
- [x] Replaced exact root RFC822 serialization assertion with semantic child-header assertions because Domino may normalize MIME quoting/folding
- [x] Tested bounded root-stream, hard-coded `MIMEGetEntityData`, `MIMEEntityGetHeader`, and single-bit entity-data flag probes for child-header readback
- [x] Runtime diagnostics prove `Content-Disposition: attachment` exists immediately after mutation and after MIME-directory reopen
- [x] Runtime diagnostics prove fixed published `MIMESYMBOL` positions do not match the installed Domino runtime
- [x] Runtime diagnostics also disprove the single-bit `MIMEGetEntityData` heuristic: the selected buffer associated `Content-Disposition` with the CTE lookup
- [x] Removed selector guessing from direct-child `GetNthHeader` again
- [x] Direct-child native header lookup now scans only the documented `MIMESYMBOL` range `0..124` through `MIMEEntityGetHeader` and validates returned values by header semantics

## In progress

- [ ] Confirm the full-range native symbol scan resolves `Content-Transfer-Encoding` to `base64` immediately and after MIME-directory reopen
- [ ] Confirm `Content-Disposition` remains `attachment` through save/reopen
- [ ] Implement and verify `Content-Disposition` filename parameter readback separately; `MIMEEntityGetHeader` on the tested runtime returns only the main value `attachment`
- [ ] Confirm both supported header semantics after save/reopen
- [ ] Confirm the updated Notes MIME surface compiles in branch CI
- [ ] Update `docs/notes-mime-entity.md` and `skills/xpscript-programming/SKILL.md` with the final verified direct-child `GetNthHeader`/parameter behavior after the Domino runtime probe is green

## Remaining

- [ ] Split the executable regression into separate assertions for `Content-Disposition: attachment` and its `filename` parameter
- [ ] Remove staged MIME attachment diagnostics after the child-header readback regression is fully green
- [ ] Add nested child-parent mutation beyond direct children of the root entity
- [ ] Recheck the full Compile workflow after the existing Linux `native-csv-regression.xps` compile failure is resolved; Linux currently fails before reaching the Notes MIME compile step
