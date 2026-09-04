# Database UI data sources

XPScript database UI data sources provide one consistent JSON binding model for `UIListView` and `UIForm` without changing the database storage model. JSON is the runtime transport/binding representation only. SQL databases keep searchable native columns, Supabase keeps native PostgreSQL columns, and Domino keeps native document items.

The same provider-neutral model applies to record attachments. An attachment collection is scoped to one parent/owner record or document. Attachment file names are metadata, not identifiers.

## Data ownership rules

- A list/query/view is exposed as `XPJsonArray` and can be passed directly to `UIListView.BindData`.
- A complete row/document is exposed as `XPJsonObject` and can be passed directly to `UIForm.BindData`.
- `UIForm.BindData` keeps the same `XPJsonObject` instance, so multiple forms can share one record object.
- A form only changes fields it edits; hidden/unbound properties remain in the shared object.
- `SaveRow` writes native database columns/items, never one opaque JSON record blob.
- SQLite generated/hidden columns and SQL Server identity/computed columns remain readable but are excluded from `SaveRow` assignments.
- Domino top-level provider metadata beginning with `@` remains in the object but is excluded from document-item writes.
- Attachment collections are parent-scoped. The same `originalName` may appear on many parents and multiple times on the same parent.
- Every attachment has a stable `attachmentId` GUID. Binary retrieval and delete use that ID, never a file name.
- `Save` and `SaveAs` always create a new attachment ID, even when another attachment has the same `originalName`.
- Attachments are immutable. To replace a file, delete the old attachment and create a new one with a new `attachmentId`.
- `createdBy` is supplied explicitly when `Save` or `SaveAs` is called.
- The current portable attachment runtime limits one file to 64 MiB because one attachment is buffered at a time.

The executable SQLite regressions [database-uiform-datasource.xps](../samples/database-uiform-datasource.xps) and [database-attachments.xps](../samples/database-attachments.xps) demonstrate both record and attachment flows.

## SQLite

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `XPDBSQLite.QueryArray` | `db.QueryArray(sql [, parameters])` | `sql`: query text; `parameters`: optional scalar `XPJsonObject`. | Executes the query and returns its array root directly as `XPJsonArray`. | [database-uiform-datasource.xps](../samples/database-uiform-datasource.xps) |
| `XPDBSQLite.GetRow` | `db.GetRow(table, keyColumn, keyValue)` | Native table, validated key column and lookup value. | Loads exactly one complete row with `SELECT *` as `XPJsonObject`. | [database-uiform-datasource.xps](../samples/database-uiform-datasource.xps) |
| `XPDBSQLite.SaveRow` | `db.SaveRow(table, keyColumn, data)` | Native table, key column and complete `XPJsonObject`. | Updates native writable columns using parameters. | [database-uiform-datasource.xps](../samples/database-uiform-datasource.xps) |
| `XPDBSQLite.Attachments` | `db.Attachments(table, keyColumn, keyValue)` | Parent table, key column and unique key value. | Returns an attachment collection scoped to exactly one row. Binary data and metadata are stored in the managed native SQLite BLOB side table. | [database-attachments.xps](../samples/database-attachments.xps) |

## SQL Server

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `XPDbMsSql.QueryArray` | `db.QueryArray(sql [, parameters])` | Query and optional scalar parameters. | Returns query rows directly as `XPJsonArray`. | [database-uiform-datasource-mssql.xps](../samples/database-uiform-datasource-mssql.xps) |
| `XPDbMsSql.GetRow` | `db.GetRow(table, keyColumn, keyValue)` | `table` or `schema.table`, validated key column and lookup value. | Loads exactly one complete row. | [database-uiform-datasource-mssql.xps](../samples/database-uiform-datasource-mssql.xps) |
| `XPDbMsSql.SaveRow` | `db.SaveRow(table, keyColumn, data)` | Native table, key column and complete `XPJsonObject`. | Updates native writable columns; identity/computed columns are not assigned. | [database-uiform-datasource-mssql.xps](../samples/database-uiform-datasource-mssql.xps) |
| `XPDbMsSql.Attachments` | `db.Attachments(table, keyColumn, keyValue)` | Parent table, key column and unique key value. | Returns one parent-scoped collection. Default storage uses `varbinary(max)` plus indexed owner metadata, without requiring FILESTREAM. | [database-attachments-mssql.xps](../samples/database-attachments-mssql.xps) |

## Supabase / PostgREST

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `XPHttpDbSupabase.QueryArray` | `db.QueryArray(table [, query])` | PostgREST table/view and optional query. | Returns rows as `XPJsonArray`. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `XPHttpDbSupabase.GetRow` | `db.GetRow(table, keyColumn, keyValue)` | Table, key column and value. | Loads exactly one complete row. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `XPHttpDbSupabase.SaveRow` | `db.SaveRow(table, keyColumn, data)` | Table, key and complete row object. | PATCHes native PostgreSQL/PostgREST fields. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `XPHttpDbSupabase.SetAttachmentBucket` | `db.SetAttachmentBucket(bucket)` | Existing Storage bucket name. | Selects the Supabase Storage bucket used by `Attachments`; default is `attachments`. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `XPHttpDbSupabase.Attachments` | `db.Attachments(table, keyColumn, keyValue)` | Parent table, key column and value. | Verifies the row exists and returns a parent-scoped collection backed by Supabase Storage. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |

## Domino REST

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `XPHttpDbDominoRest.GetViewArray` | `db.GetViewArray(viewName [, query])` | Domino view/list and optional query. | Returns view entries as `XPJsonArray`. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `XPHttpDbDominoRest.QueryArray` | `db.QueryArray(queryPayload)` | Domino query string/payload. | Returns query documents as `XPJsonArray`. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `XPHttpDbDominoRest.GetRow` | `db.GetRow(unid)` | 32-character document UNID. | Loads the complete Notes document as one shared `XPJsonObject`. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `XPHttpDbDominoRest.SaveRow` | `db.SaveRow(unid, data)` | UNID and complete shared object. | Updates native document items while excluding top-level `@...` provider metadata. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `XPHttpDbDominoRest.Attachments` | `db.Attachments(unid [, fieldName])` | Notes UNID and optional rich-text item such as `Body`. | Returns attachments scoped to the same Notes document. `fieldName` targets the native rich-text item. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |

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
  "createdBy": "creator@example",
  "checksumSha256": "..."
}
```

Two or more attachments on the same parent may all have `originalName = "contracts.pdf"`; their `attachmentId` values remain different.

## Attachment collection

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `AttachmentCollection.List` | `files.List()` | none | Alias for `GetMetadata()` returning metadata for all attachments belonging to the current parent. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.GetMetadata` | `files.GetMetadata()` or `files.GetMetadata(attachmentId)` | Optional attachment GUID. | Returns all metadata as `XPJsonArray`, or one metadata `XPJsonObject` by ID. Does not download binary content. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.FindByName` | `files.FindByName(originalName)` | Original display/file name. | Returns every matching metadata object. Multiple matches are valid and expected. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.Save` | `files.Save(sourcePath, createdBy)` | Existing local source file and creator identity. | Creates a new immutable attachment with a new GUID and returns its metadata. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.SaveAs` | `files.SaveAs(sourcePath, originalName, createdBy)` | Existing file, display/original name and creator identity. | Creates a new immutable attachment even if the same name already exists. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.Get` | `files.Get(attachmentId, targetPath)` | Attachment GUID and disk target. | Compatibility alias for `SaveToDisk`. Desktop uses normal OS file permissions. Web uses the private export sandbox. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.SaveToDisk` | `files.SaveToDisk(attachmentId, targetPath)` | Attachment GUID and output path. | On desktop, writes to the requested OS path. On web/server, `targetPath` must be relative and is resolved inside a private non-web-served sandbox. Not available in browser-WASM. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.GetAll` | `files.GetAll(targetFolder)` | Local destination directory only. Parent/owner is already fixed by `Attachments(...)`. | Desktop exports all attachments under the requested directory. Web requires a relative private directory and exports only inside the private sandbox. Browser-WASM must use `SendToBrowser` per attachment. | [database-attachments.xps](../samples/database-attachments.xps) |
| `AttachmentCollection.SendToBrowser` | `files.SendToBrowser(attachmentId [, downloadName])` | Attachment GUID and optional safe browser download name. | Server web streams bytes through the active `Response` with `Content-Disposition: attachment`; browser-WASM creates a browser Blob download. No web-accessible disk file is created. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `AttachmentCollection.Delete` | `files.Delete(attachmentId)` | Attachment GUID. | Deletes exactly one attachment from the current parent. To replace content, delete then create a new attachment. | [database-attachments.xps](../samples/database-attachments.xps) |

### Example with duplicate names

```xpscript
Dim files As Variant
Dim first As XPJsonObject
Dim second As XPJsonObject
Dim matches As XPJsonArray

Set files = db.Attachments("customers", "id", 42)
Set first = files.SaveAs("docs/contract-v1.pdf", "contracts.pdf", "user@example")
Set second = files.SaveAs("docs/contract-v2.pdf", "contracts.pdf", "user@example")

Print first.Get("attachmentId")
Print second.Get("attachmentId")

Set matches = files.FindByName("contracts.pdf")
Print matches.Count
```

### Replace an attachment

Attachments are immutable. Replacing a file means deleting the old attachment and creating a new one:

```xpscript
Call files.Delete(oldAttachmentId)
Set replacement = files.SaveAs("docs/revised-contract.pdf", "contracts.pdf", "editor@example")
```

The replacement has a new `attachmentId`, `created` timestamp and `createdBy` value.

## Disk export security

Desktop programs use the ordinary operating-system security boundary. `SaveToDisk` may write wherever the desktop process account has permission.

Web/server programs do not receive arbitrary filesystem write access through the attachment API. The requested path must be relative, traversal such as `..` is rejected, and the output is resolved beneath an XPScript-managed private export sandbox under the operating-system temporary area. The sandbox is not under the configured web root, so an exported file cannot become directly reachable merely because the web server serves static files from the application root. Symbolic-link/reparse-point directories are rejected.

Use `SendToBrowser` when the intent is to let the HTTP client download an attachment. It streams the attachment through the response instead of publishing a file.

### Server web download

```xpscript
Dim files As Variant
Set files = db.Attachments("customers", "id", 23)
Call files.SendToBrowser(attachmentId, "contract.pdf")
```

`SendToBrowser` uses the active web response, sets a safe attachment `Content-Disposition`, applies the attachment content type and writes the bytes directly to the response body.

### Browser-WASM download

The same public call is valid in a `[Platform:browser-wasm]` program:

```xpscript
Call files.SendToBrowser(attachmentId, "contract.pdf")
```

In browser-WASM no server filesystem is used. XPScript converts the retrieved bytes to a browser Blob, creates a temporary object URL, triggers the browser download and revokes the URL afterwards.

### Get all attachments for one parent

```xpscript
Dim files As Variant
Dim downloaded As XPJsonArray

Set files = db.Attachments("customers", "id", 23)
Set downloaded = files.GetAll("customer-23")
```

On desktop the directory can be an ordinary OS path. On web it is interpreted as a relative path beneath the private export sandbox. The parent remains `customers/id=23` because it was fixed when `files` was created.

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
    Dim customer As XPJsonObject
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
Dim rows As XPJsonArray
Dim list As New UIListView("Customers")

Set rows = db.QueryArray("SELECT id, name, city FROM customers ORDER BY name")
Call list.BindData(rows)
Call list.SetKeyField("id")
Call list.AddColumn("name", "Name")
Call list.AddColumn("city", "City")
```

A reduced list projection should never become the authoritative editable record. When a user opens a row, call `GetRow` with its key and bind the complete result to one or more forms.
