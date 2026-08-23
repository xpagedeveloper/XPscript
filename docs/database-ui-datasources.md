# Database UI data sources

XPScript database UI data sources provide one consistent JSON binding model for `UIListView` and `UIForm` without changing the database storage model. JSON is the runtime transport/binding representation only. SQL databases keep searchable native columns, Supabase keeps native PostgreSQL columns, and Domino keeps native document items.

The same provider-neutral model applies to record attachments. An attachment collection is scoped to one parent/owner record or document. Attachment file names are metadata, not identifiers.

## Data ownership rules

- A list/query/view is exposed as `JsonArray` and can be passed directly to `UIListView.BindData`.
- A complete row/document is exposed as `JsonObject` and can be passed directly to `UIForm.BindData`.
- `UIForm.BindData` keeps the same `JsonObject` instance, so multiple forms can share one record object.
- A form only changes fields it edits; hidden/unbound properties remain in the shared object.
- `SaveRow` writes native database columns/items, never one opaque JSON record blob.
- SQLite generated/hidden columns and SQL Server identity/computed columns remain readable but are excluded from `SaveRow` assignments.
- Domino top-level provider metadata beginning with `@` remains in the object but is excluded from document-item writes.
- Attachment collections are parent-scoped. The same `originalName` may appear on many parents and multiple times on the same parent.
- Every attachment has a stable `attachmentId` GUID. `Get`, `Update` and `Delete` use that ID, never a file name.
- `Save` and `SaveAs` always create a new attachment ID, even when another attachment has the same `originalName`.
- `Update` and `UpdateAs` replace content for an existing attachment while preserving `attachmentId`, `created` and `createdBy`.
- `SetActor` sets the application/session identity used for `createdBy` and `modifiedBy`; the process user is only a fallback.
- The current portable attachment runtime limits one file to 64 MiB because one attachment is buffered at a time.

The executable SQLite regressions [database-uiform-datasource.xps](../samples/database-uiform-datasource.xps) and [database-attachments.xps](../samples/database-attachments.xps) demonstrate both record and attachment flows.

## SQLite

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `XPDBSQLite.QueryArray` | `db.QueryArray(sql [, parameters])` | `sql`: query text; `parameters`: optional scalar `JsonObject`. | Executes the query and returns its array root directly as `JsonArray`. | [database-uiform-datasource.xps](../samples/database-uiform-datasource.xps) |
| `XPDBSQLite.GetRow` | `db.GetRow(table, keyColumn, keyValue)` | Native table, validated key column and lookup value. | Loads exactly one complete row with `SELECT *` as `JsonObject`. | [database-uiform-datasource.xps](../samples/database-uiform-datasource.xps) |
| `XPDBSQLite.SaveRow` | `db.SaveRow(table, keyColumn, data)` | Native table, key column and complete `JsonObject`. | Updates native writable columns using parameters. | [database-uiform-datasource.xps](../samples/database-uiform-datasource.xps) |
| `XPDBSQLite.Attachments` | `db.Attachments(table, keyColumn, keyValue)` | Parent table, key column and unique key value. | Returns an attachment collection scoped to exactly one row. Binary data and metadata are stored in the managed native SQLite BLOB side table. | [database-attachments.xps](../samples/database-attachments.xps) |

## SQL Server

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `XPDbMsSql.QueryArray` | `db.QueryArray(sql [, parameters])` | Query and optional scalar parameters. | Returns query rows directly as `JsonArray`. | [database-uiform-datasource-mssql.xps](../samples/database-uiform-datasource-mssql.xps) |
| `XPDbMsSql.GetRow` | `db.GetRow(table, keyColumn, keyValue)` | `table` or `schema.table`, validated key column and lookup value. | Loads exactly one complete row. | [database-uiform-datasource-mssql.xps](../samples/database-uiform-datasource-mssql.xps) |
| `XPDbMsSql.SaveRow` | `db.SaveRow(table, keyColumn, data)` | Native table, key column and complete `JsonObject`. | Updates native writable columns; identity/computed columns are not assigned. | [database-uiform-datasource-mssql.xps](../samples/database-uiform-datasource-mssql.xps) |
| `XPDbMsSql.Attachments` | `db.Attachments(table, keyColumn, keyValue)` | Parent table, key column and unique key value. | Returns one parent-scoped collection. Default storage uses `varbinary(max)` plus indexed owner metadata, without requiring FILESTREAM. | [database-attachments-mssql.xps](../samples/database-attachments-mssql.xps) |

## Supabase / PostgREST

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `HTTPDBSupabase.QueryArray` | `db.QueryArray(table [, query])` | PostgREST table/view and optional query. | Returns rows as `JsonArray`. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `HTTPDBSupabase.GetRow` | `db.GetRow(table, keyColumn, keyValue)` | Table, key column and value. | Loads exactly one complete row. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `HTTPDBSupabase.SaveRow` | `db.SaveRow(table, keyColumn, data)` | Table, key and complete row object. | PATCHes native PostgreSQL/PostgREST fields. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `HTTPDBSupabase.SetAttachmentBucket` | `db.SetAttachmentBucket(bucket)` | Existing Storage bucket name. | Selects the Supabase Storage bucket used by `Attachments`; default is `attachments`. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `HTTPDBSupabase.Attachments` | `db.Attachments(table, keyColumn, keyValue)` | Parent table, key column and value. | Verifies the row exists and returns a parent-scoped collection backed by Supabase Storage. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |

## Domino REST

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `HTTPDBDominoRest.GetViewArray` | `db.GetViewArray(viewName [, query])` | Domino view/list and optional query. | Returns view entries as `JsonArray`. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `HTTPDBDominoRest.QueryArray` | `db.QueryArray(queryPayload)` | Domino query string/payload. | Returns query documents as `JsonArray`. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `HTTPDBDominoRest.GetRow` | `db.GetRow(unid)` | 32-character document UNID. | Loads the complete Notes document as one shared `JsonObject`. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `HTTPDBDominoRest.SaveRow` | `db.SaveRow(unid, data)` | UNID and complete shared object. | Updates native document items while excluding top-level `@...` provider metadata. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `HTTPDBDominoRest.Attachments` | `db.Attachments(unid [, fieldName])` | Notes UNID and optional rich-text item such as `Body`. | Returns attachments scoped to the same Notes document; `fieldName` targets the native rich-text item for save/update/delete. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |

## Attachment identity and metadata

`originalName` is never unique. `attachmentId` is the stable identity.

A metadata object contains at least:

```json
{
  "attachmentId": "91f12b98-378c-4dd2-a83d-e4674d9f16e3",
  "originalName": "contracts.pdf",
  "contentType": "application/pdf",
  "size": 183244,
  "created": "2026-08-23T13:21:00Z",
  "modified": "2026-08-23T13:32:00Z",
  "createdBy": "creator@example",
  "modifiedBy": "editor@example",
  "checksumSha256": "..."
}
```

Two or more attachments on the same parent may all have `originalName = "contracts.pdf"`; their `attachmentId` values remain different.

## Attachment collection

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `AttachmentCollection.SetActor` | `files.SetActor(actor)` | Application/session user identity. | Sets the audit identity used by subsequent `Save`/`Update`. Without an override, the process user is used as fallback. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.List` | `files.List()` | none | Alias for `GetMetadata()` returning metadata for all attachments belonging to the current parent. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.GetMetadata` | `files.GetMetadata()` or `files.GetMetadata(attachmentId)` | Optional attachment GUID. | Returns all metadata as `JsonArray`, or one metadata `JsonObject` by ID. Does not download binary content. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.FindByName` | `files.FindByName(originalName)` | Original display/file name. | Returns every matching metadata object. Multiple matches are valid and expected. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.Save` | `files.Save(sourcePath)` | Existing local source file. | Creates a new attachment with a new GUID and returns its metadata. It never replaces an existing attachment merely because the name matches. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.SaveAs` | `files.SaveAs(sourcePath, originalName)` | Existing file and display/original name. | Creates a new attachment with the supplied `originalName`, even if that name already exists. Returns metadata. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.Update` | `files.Update(attachmentId, sourcePath)` | Existing attachment ID and replacement local file. | Replaces binary content while preserving `attachmentId`, `created` and `createdBy`; updates size, checksum, `modified` and `modifiedBy`. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.UpdateAs` | `files.UpdateAs(attachmentId, sourcePath, originalName)` | Existing ID, replacement file and new original name. | Same as `Update`, but also changes `originalName`. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.Get` | `files.Get(attachmentId, targetPath)` | Attachment GUID and local output path. | Downloads exactly one attachment by stable ID. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.GetAll` | `files.GetAll(targetFolder)` | Local destination directory only. Parent/owner is already fixed by `Attachments(...)`. | Downloads all attachments for that parent. Local names are prefixed with `attachmentId` so duplicate original names cannot collide. Returns metadata plus `localPath`. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.Delete` | `files.Delete(attachmentId)` | Attachment GUID. | Deletes exactly one attachment from the current parent. | [database-attachments.xps](../samples/database-attachments.xps) |

### Example with duplicate names

```xpscript
Dim files As Variant
Dim first As JsonObject
Dim second As JsonObject
Dim matches As JsonArray

Set files = db.Attachments("customers", "id", 42)
Call files.SetActor("user@example")

Set first = files.SaveAs("docs/contract-v1.pdf", "contracts.pdf")
Set second = files.SaveAs("docs/contract-v2.pdf", "contracts.pdf")

' Two different attachments with the same original name.
Print first.Get("attachmentId")
Print second.Get("attachmentId")

Set matches = files.FindByName("contracts.pdf")
Print matches.Count

Call files.Get(first.Get("attachmentId"), "downloads/first.pdf")
```

### Update an attachment while keeping its identity

```xpscript
Dim updated As JsonObject

Call files.SetActor("editor@example")
Set updated = files.Update(attachmentId, "docs/revised-contract.pdf")
```

The existing `attachmentId`, `created` and `createdBy` remain unchanged. `modified`, `modifiedBy`, size, content type and checksum are updated.

### Get all attachments for one parent

```xpscript
Dim files As Variant
Dim downloaded As JsonArray

Set files = db.Attachments("customers", "id", 23)
Set downloaded = files.GetAll("downloads/customer-23")
```

`downloads/customer-23` is only the local destination. The parent is already `customers/id=23` because that is how `files` was created.

### Provider storage model

- **SQLite** stores binary content and metadata in a managed native BLOB side table keyed by `attachmentId` and parent identity.
- **SQL Server** stores binary content in `varbinary(max)` with a GUID primary key and indexed parent hash. FILESTREAM is not required for the simple portable API.
- **Supabase** stores content and metadata in Supabase Storage under a parent-scoped object prefix. The Storage bucket is configured with `SetAttachmentBucket`.
- **Domino** stores the binary as a real Notes document attachment. XPScript uses a unique internal storage name derived from `attachmentId`, while `originalName` remains metadata, allowing multiple attachments with the same displayed name. Metadata is associated with the same Notes document.

## Shared UIForm example

```xpscript
Option Declare

Sub Main()
    Dim db As New XPDBSQLite("customers.db")
    Dim customer As JsonObject
    Dim generalForm As New UIForm("General")
    Dim addressForm As New UIForm("Address")

    Set customer = db.GetRow("customers", "id", 42)
    Call generalForm.BindData(customer)
    Call generalForm.AddTextField("name", "Name")
    Call addressForm.BindData(customer)
    Call addressForm.AddTextField("city", "City")

    Call generalForm.SetFieldValue("name", "Anna Andersson")
    Call addressForm.SetFieldValue("city", "Goteborg")
    Call db.SaveRow("customers", "id", customer)
End Sub
```

Compile a complete runnable version with:

```text
xpscriptc samples/database-uiform-datasource.xps -o database-uiform-datasource --framework-dependent
```

## UIListView example

```xpscript
Dim rows As JsonArray
Dim list As New UIListView("Customers")

Set rows = db.QueryArray("SELECT id, name, city FROM customers ORDER BY name")
Call list.BindData(rows)
Call list.SetKeyField("id")
Call list.AddColumn("name", "Name")
Call list.AddColumn("city", "City")
```

A reduced list projection should never become the authoritative editable record. When a user opens a row, call `GetRow` with its key and bind the complete result to one or more forms.
