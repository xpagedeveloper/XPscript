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

## In progress

- [ ] Enable root-entity `SetContentFromText` through the MIME stream/itemize writer
- [ ] Enable root-entity `SetContentFromBytes` through the same writer
- [ ] Invalidate the document MIME directory immediately after successful root writeback
- [ ] Refresh the live root entity after writeback so metadata and charset reads use the new native directory

## Remaining

- [ ] Add save/reopen regression coverage for `SetContentFromText`
- [ ] Verify content type/subtype metadata survives save/reopen
- [ ] Verify charset survives save/reopen
- [ ] Verify written text survives save/reopen
- [ ] Keep child-entity mutation explicitly unsupported until verified per-entity Domino mutation support exists
- [ ] Update API/LLM guidance if the implemented mutation changes recommended XPscript usage
- [ ] Run compiler build and MIME sample compile checks
- [ ] Check branch CI status
