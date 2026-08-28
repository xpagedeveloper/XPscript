# NotesDatabase local full-surface test

This test exercises the current XPscript `NotesDatabase` surface against a local HCL Notes database named `xpscript.nsf`.

Test programs:

- `samples/notes-database-full-surface-test.xps`
- `samples/notes-document-remove-test.xps` for `NotesDocument.Remove([force])`

## Required local environment

1. HCL Notes or Domino must be installed and initialized.
2. The test process must be able to load the Notes runtime. The sample currently uses:
   - `C:\Program Files\HCL\Notes`
3. A valid local Notes configuration / `notes.ini` must be available to the runtime.
4. The current Notes ID must have sufficient access to open and write `xpscript.nsf`.
5. Create a local database at exactly:
   - `xpscript.nsf`

The database can otherwise be a simple empty test database.

## Database content required before the first run

### Required

Create one view named exactly:

- `XPScriptTestView`

For the broadest test coverage, give the view a simple selection formula such as:

```text
SELECT @All
```

No documents are required beforehand. The full-surface test creates its own document with:

- `Form = XPScriptTest`
- `Subject = XPscript NotesDatabase smoke test`
- `XPScriptMarker = notes-database-full-surface-test`

The dedicated Remove test creates and deletes its own `XPScriptRemoveTest` documents and therefore needs no pre-existing documents or design elements beyond the database itself.

### Profile document

No profile document has to exist beforehand.

The test calls:

```text
GetProfileDocument("XPScriptTestProfile", "Local")
```

The underlying `NSFProfileOpen` path creates the profile when it does not already exist. The test writes and saves:

- `XPScriptProfileMarker = profile-ok`

### Optional agent test

The agent test is disabled by default.

To test `RunAgent`, create an agent named exactly:

- `XPScriptTestAgent`

It should be runnable by the current Notes ID. A minimal LotusScript agent that prints one line is sufficient, for example an agent whose `Initialize` event prints a recognizable marker.

Then change in the sample:

```text
Const ENABLE_AGENT_TEST = True
```

### Optional full-text search test

`FTSearch` is disabled by default.

To test it:

1. Create a full-text index for `xpscript.nsf` using Notes/Domino administration or the Notes client.
2. Ensure at least one document contains the word `XPscript`.
3. Change:

```text
Const ENABLE_FT_SEARCH = True
```

### RemoveFTIndex warning

`RemoveFTIndex` deletes the database full-text index and is therefore disabled by default.

Only enable it when you intentionally want the test to remove the index:

```text
Const ENABLE_REMOVE_FT_INDEX = True
```

If both FT search and index removal are being tested in one run, keep the order in the sample: `FTSearch` runs before `RemoveFTIndex`.

## Metadata write tests

The sample defaults to:

```text
Const ENABLE_METADATA_WRITES = True
```

It tests writes for:

- `Title`
- `Categories`
- `TemplateName`
- `DesignTemplateName`

For every property, the test:

1. reads the original value,
2. writes a temporary XPscript test value,
3. reads the value back,
4. restores the original value,
5. reads the restored value back.

Use only a disposable/local test database. Even though the test restores the database-info values, changing template metadata can affect application/design behavior in Notes and should not be run against a production NSF.

To run read-only metadata checks instead, set:

```text
Const ENABLE_METADATA_WRITES = False
```

## NotesDocument.Remove behavior

`NotesDocument.Remove()` is equivalent to `NotesDocument.Remove(False)`.

The dedicated test verifies:

- `Remove()` returns `True` when the document is deleted successfully.
- `Remove(True)` exercises the `UPDATE_FORCE` path.
- after successful removal, `GetDocumentByUNID` returns `Nothing` for the deleted document.

LotusScript-compatible conflict behavior is preserved: with `force=False`, an update conflict returns `False`; other Notes errors remain errors rather than being converted to `False`.

`Remove` follows normal Notes soft-deletion behavior when soft deletions are enabled in the database. It is not the same as `RemovePermanently`.

## Surface covered by the sample

The sample exercises the currently implemented members relevant to this NotesDatabase work:

### Properties

- `Parent`
- `Server`
- `FilePath`
- `FileName`
- `IsOpen`
- `Title` read/write
- `Categories` read/write
- `TemplateName` read/write
- `DesignTemplateName` read/write
- `ReplicaID`
- `Size`
- `PercentUsed`
- `CurrentAccessLevel`
- `Created`
- `LastModified`
- `FileFormat`
- `AllDocuments`

### Methods

- `GetView`
- `OpenView` compatibility alias
- `GetDocumentByID`
- `GetDocumentByNoteId` compatibility alias
- `OpenDocumentByNoteId` compatibility alias
- `GetDocumentByUNID`
- `OpenDocumentByUNID` compatibility alias
- `CreateDocument`
- `CreateDocumentCollection`
- `GetProfileDocument`
- `Search`
- `FTSearch` when enabled
- `RunAgent` when enabled
- `RemoveFTIndex` when explicitly enabled
- `NotesDocument.Remove([force])` in the dedicated Remove test
- `Recycle`

## Not covered yet because they are not implemented in this PR

The test intentionally does not pretend these exist:

- `Views`
- `GetProfileDocCollection`
- `IsFTIndexed`
- `LastFTIndexed`
- `CreateFTIndex`
- `UpdateFTIndex`
- `TransactionBegin`
- `TransactionCommit`
- `TransactionRollback`

They should be added to this same test when their exact Domino C API implementations have been verified and added to XPscript.

## Expected side effects

The full-surface test currently leaves its `XPScriptTest` document and profile document for inspection. The dedicated Remove test cleans up the documents it creates by calling `Remove()` and `Remove(True)`.

The original database title/categories/template metadata is restored by the full-surface test, and the FT index is unchanged unless `ENABLE_REMOVE_FT_INDEX` was explicitly set to `True`.
