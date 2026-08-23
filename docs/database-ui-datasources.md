# Database UI data sources

XPScript database UI data sources provide one consistent JSON binding model for `UIListView` and `UIForm` without changing the database storage model. JSON is used as the runtime transport and binding representation only. SQL databases keep searchable native columns, Supabase keeps native PostgreSQL columns, and Domino keeps native document items.

## Data ownership rules

- A list/query/view is exposed as `JsonArray` and can be passed directly to `UIListView.BindData`.
- A single complete row/document is exposed as `JsonObject` and can be passed directly to `UIForm.BindData`.
- `UIForm.BindData` keeps the same `JsonObject` instance. Multiple forms can therefore share one record object.
- A form only changes fields that the form actually edits. Properties not shown by that form remain in the shared object.
- `SaveRow` writes properties back to native database columns/items. It does not store the complete record as one JSON blob.
- SQLite generated/hidden columns and SQL Server identity/computed columns remain available in the loaded object but are not assigned by `SaveRow`.
- Domino top-level provider metadata whose names begin with `@` remains available in the loaded object but is not written as a document item.
- SQL identifiers are validated and values are always sent as parameters.

The executable SQLite regression [database-uiform-datasource.xps](../samples/database-uiform-datasource.xps) demonstrates `QueryArray -> UIListView`, two `UIForm` instances sharing the same full record object, and a database field that neither form displays surviving `SaveRow` unchanged.

## SQLite

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `XPDBSQLite.QueryArray` | `db.QueryArray(sql [, parameters])` | `sql`: query text; `parameters`: optional scalar `JsonObject` parameters. | Executes the query and returns its array root directly as `JsonArray`, suitable for `UIListView.BindData`. | [database-uiform-datasource.xps](../samples/database-uiform-datasource.xps) |
| `XPDBSQLite.GetRow` | `db.GetRow(table, keyColumn, keyValue)` | `table`: native SQLite table; `keyColumn`: validated key column; `keyValue`: parameterized lookup value. | Loads the complete matching row using `SELECT *` and returns it as `JsonObject`. Zero or multiple matches are rejected. | [database-uiform-datasource.xps](../samples/database-uiform-datasource.xps) |
| `XPDBSQLite.SaveRow` | `db.SaveRow(table, keyColumn, data)` | `table`: native SQLite table; `keyColumn`: key property and WHERE column; `data`: complete `JsonObject`. | Validates the table schema and updates native writable columns with parameterized values. Generated/hidden columns are not assigned. | [database-uiform-datasource.xps](../samples/database-uiform-datasource.xps) |

## SQL Server

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `XPDbMsSql.QueryArray` | `db.QueryArray(sql [, parameters])` | `sql`: query text; `parameters`: optional scalar `JsonObject`. | Returns query rows directly as `JsonArray` for list binding. | [database-uiform-datasource-mssql.xps](../samples/database-uiform-datasource-mssql.xps) |
| `XPDbMsSql.GetRow` | `db.GetRow(table, keyColumn, keyValue)` | `table`: `table` or `schema.table`; `keyColumn`: validated column; `keyValue`: parameterized lookup value. | Loads the complete row with `SELECT TOP (2) *` and requires exactly one match. | [database-uiform-datasource-mssql.xps](../samples/database-uiform-datasource-mssql.xps) |
| `XPDbMsSql.SaveRow` | `db.SaveRow(table, keyColumn, data)` | `table`: native table; `keyColumn`: key property/column; `data`: complete `JsonObject`. | Reads SQL Server column metadata and updates native writable columns. Identity and computed columns are retained in memory but excluded from `SET`. | [database-uiform-datasource-mssql.xps](../samples/database-uiform-datasource-mssql.xps) |

## Supabase / PostgREST

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `HTTPDBSupabase.QueryArray` | `db.QueryArray(table [, query])` | `table`: PostgREST table/view; `query`: optional PostgREST query such as `select=*&status=eq.active`. | Executes `Select` and exposes the response array directly as `JsonArray`. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `HTTPDBSupabase.GetRow` | `db.GetRow(table, keyColumn, keyValue)` | `table`: table/view; `keyColumn`: validated column; `keyValue`: equality-filter value. | Loads `select=*` for the key with `limit=2` and returns exactly one complete row object. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `HTTPDBSupabase.SaveRow` | `db.SaveRow(table, keyColumn, data)` | `table`: native PostgreSQL table through PostgREST; `keyColumn`: key column; `data`: complete `JsonObject`. | PATCHes the record properties as native PostgREST fields. No JSON container column is introduced by XPScript. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |

## Domino REST

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `HTTPDBDominoRest.GetViewArray` | `db.GetViewArray(viewName [, query])` | `viewName`: Domino view/list name; `query`: optional list query string. | Returns view entries directly as `JsonArray`, suitable for `UIListView.BindData`. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `HTTPDBDominoRest.QueryArray` | `db.QueryArray(queryPayload)` | `queryPayload`: Domino query string or supported query payload. | Executes the Domino query and exposes the returned documents as `JsonArray`. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `HTTPDBDominoRest.GetRow` | `db.GetRow(unid)` | `unid`: 32-character document UNID. | Loads the complete native Domino document response as one shared `JsonObject`, including normal document items and provider metadata. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |
| `HTTPDBDominoRest.SaveRow` | `db.SaveRow(unid, data)` | `unid`: document UNID; `data`: complete shared `JsonObject`. | Updates native document items. Top-level provider metadata properties beginning with `@` remain in `data` but are excluded from the PUT payload. | [httpdb-supabase-domino.xps](../samples/httpdb-supabase-domino.xps) |

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

    ' Both forms mutate the same object. Other properties remain untouched.
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

The list query may intentionally project only columns required for display. When the user opens one record for editing, call `GetRow` with its key to load the complete record before binding one or more forms. This prevents a reduced list projection from becoming the authoritative record used by `SaveRow`.
