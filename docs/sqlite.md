# SQLite database

`XPDBSQLite` provides local SQLite access through the maintained `Microsoft.Data.Sqlite` provider. XPScript identifiers are case-insensitive, so `xpdbsqlite` is accepted, while `XPDBSQLite` is the documented spelling.

The API is available to normal Windows, Linux and macOS applications and to server-side web routes. It is not available to `browser-wasm` targets.

## Open a database

The constructor opens the connection immediately:

```xpscript
Dim db As New XPDBSQLite("app.db")
Dim readOnlyDb As New XPDBSQLite("app.db", True)
Dim memoryDb As New XPDBSQLite(":memory:")
```

A file path must be relative to the compiled application or host executable directory. Rooted paths, paths that escape with `..`, symbolic links and reparse points are rejected. The parent directory must already exist. Use `:memory:` for a private in-memory database.

For a server deployment, create the data directory during deployment and grant the host process only the filesystem permissions it needs. Do not place a writable database under a directory served as static content.

## Execute parameterized SQL

Use a `JsonObject` for named parameters. A bare key such as `name` is bound as `$name`. Keys that already start with `$`, `@` or `:` keep their prefix.

```xpscript
Dim db As New XPDBSQLite("customers.db")
Dim parameters As New JsonObject

Call db.Execute("CREATE TABLE customers (id INTEGER PRIMARY KEY, name TEXT NOT NULL)")
Call parameters.Set("name", "Ada")
Print CStr(db.Execute("INSERT INTO customers(name) VALUES ($name)", parameters))
Call db.Close()
```

Parameter values must be JSON scalar values or null. Never concatenate untrusted values into SQL text. SQL identifiers such as table and column names cannot be parameterized and must come from trusted application code or an explicit allowlist.

## Query rows

`Query` returns a `JsonDocument` whose root is an array. Each row is a JSON object keyed by column name. Duplicate column names receive numeric suffixes. SQLite `NULL` becomes JSON null and BLOB values become Base64 text.

```xpscript
Dim rows As JsonDocument
Dim array As JsonArray
Dim row As JsonObject

Set rows = db.Query("SELECT id, name FROM customers WHERE name = $name", parameters)
Set array = rows.Root.AsArray()
Set row = array.Get(0)
Print CStr(row.Get("name"))
```

`Scalar` returns the first column of the first row as a `Variant`. It returns null when the result is SQL `NULL` or when no row is returned.

```xpscript
Dim count As Variant
count = db.Scalar("SELECT COUNT(*) FROM customers")
Print CStr(count)
```

## Transactions

One transaction can be active per `XPDBSQLite` instance.

```xpscript
Call db.BeginTransaction()
Call db.Execute("UPDATE customers SET name = $name WHERE id = 1", parameters)
Call db.Commit()
```

Use `Rollback` instead of `Commit` to discard the transaction. `Close` rolls back an active transaction before closing the connection.

## API reference

| Member | Behavior |
|---|---|
| `XPDBSQLite(path [, readOnly])` | Opens a file database or `:memory:`. |
| `Open()` | Reopens a connection after `Close`. Calling it on an open connection has no effect. |
| `Close()` | Rolls back an active transaction and closes the connection. |
| `Execute(sql [, parameters])` | Executes non-query SQL and returns the affected row count. |
| `Query(sql [, parameters])` | Returns rows as a JSON array document. |
| `Scalar(sql [, parameters])` | Returns the first value or null. |
| `BeginTransaction()` | Starts a transaction. |
| `Commit()` | Commits the active transaction. |
| `Rollback()` | Rolls back the active transaction. |
| `LastInsertRowId` | Returns SQLite `last_insert_rowid()` for this connection. |
| `DatabasePath` | Returns `:memory:` or the resolved database file path. |
| `ReadOnly` | Reports whether the file connection was opened read-only. |
| `IsOpen` | Reports whether the connection is open. |
| `InTransaction` | Reports whether a transaction is active. |
| `Timeout` | Command timeout in seconds. Default 30, allowed range 0.1 to 300. |
| `MaxRows` | Maximum rows returned by `Query`. Default 10000, allowed range 1 to 50000. |

SQL text is limited to 1 MiB and parameter objects to 999 entries. Query results also use the native JSON limits of 100000 nodes and a 16 MiB estimated payload. Provider failures raise XPScript runtime error 5 without including SQL text, parameter values or database contents in the error message.

The complete executable regression is [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps).
