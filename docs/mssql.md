# SQL Server database

`XPDbMsSql` provides direct SQL Server access through the official `Microsoft.Data.SqlClient` provider. The same class supports SQL Server, SQL Server Express and Azure SQL connections that use SQL authentication or integrated Windows authentication. XPScript identifiers are case-insensitive, while `XPDbMsSql` is the documented spelling.

The API is available to Windows, Linux and macOS applications and to server-side web routes. It is not available to `browser-wasm` targets.

## Open a connection

Pass a standard SQL Server connection string to the constructor. The connection opens immediately.

```xpscript
Dim db As New XPDbMsSql(Environ("APP_MSSQL_CONNECTION"))
```

Keep credentials outside source files. Environment variables, a protected service configuration store or a secrets manager are appropriate sources. `XPDbMsSql` never exposes the stored connection string and provider failures do not include SQL text, connection details or parameter values.

SQL Server with SQL authentication:

```text
Server=db.example.com,1433;Database=application;User ID=app_user;Password=secret;Encrypt=True;TrustServerCertificate=False
```

SQL Server Express with Windows authentication:

```text
Server=.\SQLEXPRESS;Database=application;Integrated Security=True;Encrypt=True;TrustServerCertificate=True
```

`TrustServerCertificate=True` is suitable only when the server certificate cannot be validated, such as a local development instance. Production servers should use a trusted certificate and `TrustServerCertificate=False`. A named Express instance can require SQL Server Browser or an explicitly configured TCP port.

## Execute parameterized SQL

Use a `JsonObject` for named parameters. Bare keys and keys beginning with `$`, `@` or `:` are normalized to SQL Server `@name` parameters.

```xpscript
Dim parameters As New JsonObject
Call parameters.Set("name", "Ada")
Call parameters.Set("active", True)

Call db.Execute("INSERT INTO customers(name, active) VALUES (@name, @active)", parameters)
```

Parameter values must be JSON scalar values or null. SQL identifiers cannot be parameterized. Table names, column names and sort expressions must come from trusted application code or an explicit allowlist.

Use `Scalar` with `OUTPUT INSERTED` when an insert must return an identity value:

```xpscript
Dim id As Variant
id = db.Scalar("INSERT INTO customers(name) OUTPUT INSERTED.id VALUES (@name)", parameters)
```

## Query rows

`Query` returns a `JsonDocument` whose root is an array. Each row is a JSON object keyed by column name. Duplicate names receive numeric suffixes. SQL `NULL` becomes JSON null, binary values become Base64 text, and date, time, GUID and time-span values become invariant text.

```xpscript
Dim rows As JsonDocument
Dim array As JsonArray
Dim row As JsonObject

Set rows = db.Query("SELECT id, name FROM customers WHERE name = @name", parameters)
Set array = rows.Root.AsArray()
Set row = array.Get(0)
Print CStr(row.Get("name"))
```

`Scalar` returns the first column of the first row as a `Variant`. It returns null for SQL `NULL` or when no row is returned.

## Transactions

One transaction can be active per `XPDbMsSql` instance.

```xpscript
Call db.BeginTransaction()
Call db.Execute("UPDATE customers SET active = @active WHERE name = @name", parameters)
Call db.Commit()
```

Use `Rollback` to discard the transaction. `Close` attempts to roll back an active transaction before closing the connection.

## API reference

| Member | Behavior |
|---|---|
| `XPDbMsSql(connectionString)` | Validates the connection string and opens the connection. |
| `Open()` | Reopens a connection after `Close`. Calling it on an open connection has no effect. |
| `Close()` | Rolls back an active transaction and closes the connection. |
| `Execute(sql [, parameters])` | Executes non-query SQL and returns the affected row count. |
| `Query(sql [, parameters])` | Returns rows as a JSON array document. |
| `Scalar(sql [, parameters])` | Returns the first value or null. |
| `BeginTransaction()` | Starts a transaction. |
| `Commit()` | Commits the active transaction. |
| `Rollback()` | Rolls back the active transaction. |
| `DataSource` | Returns the connected server name while the connection is open. |
| `Database` | Returns the connected database name while the connection is open. |
| `IsOpen` | Reports whether the connection is open. |
| `InTransaction` | Reports whether a transaction is active. |
| `Timeout` | Command timeout in seconds. Valid range is 0.1 to 300. |
| `MaxRows` | Maximum rows returned by `Query`. Valid range is 1 to 50000. |

SQL text is limited to 1 MiB. A command accepts at most 2100 parameters. Query results are limited to 1024 columns, 100000 JSON nodes and a 16 MiB payload. These limits prevent unbounded generated output and do not replace database permissions, query timeouts or server-side resource controls.
