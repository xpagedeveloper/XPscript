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
- [x] Updated Notes MIME sample compiles successfully in macOS branch CI
- [x] Updated Domino runtime regression completed with exit code 0
- [x] Immediate root `ContentAsText` verified against the mutated UTF-8 payload
- [x] Save/reopen root `ContentAsText` verified against the UTF-8 payload
- [x] Save/reopen `GetContentAsText` verified against the UTF-8 payload
- [x] Save/reopen `GetContentAsBytes` verified against the UTF-8 payload
- [x] Save/reopen `GetEntityAsText` verified to return root RFC822 data with MIME headers
- [x] Existing multipart MIME traversal still verifies `text/html; charset=UTF-8` after the readback changes

## Remaining

- [ ] Recheck the full Compile workflow after the existing Linux `native-csv-regression.xps` compile failure is resolved; Linux currently fails before reaching the Notes MIME compile step
