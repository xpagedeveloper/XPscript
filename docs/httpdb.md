# HTTP database clients

XPScript provides two native HTTP database clients for common server-side data access scenarios:

- `HTTPDBSupabase` for Supabase Cloud or self-hosted Supabase.
- `HTTPDBDominoRest` for self-hosted HCL Domino REST API servers.

Both clients use the native XPScript HTTP and JSON runtimes. They support configurable timeouts and return `JsonDocument` objects for JSON responses.

## HTTPDBSupabase

### Create a client

Cloud-hosted Supabase:

```xpscript
Dim db As New HTTPDBSupabase("https://project-ref.supabase.co", apiKey)
```

Self-hosted Supabase:

```xpscript
Dim db As New HTTPDBSupabase("https://supabase.example.com", apiKey)
```

The base URL is not restricted to `supabase.co`. The client appends `/rest/v1` unless the supplied URL already ends with `/rest/v1`.

The supplied API key is sent in the `apikey` header.

For Row Level Security or an authenticated user context, set a bearer token separately:

```xpscript
Call db.SetBearerToken(userJwt)
```

This causes data API requests to include:

```text
Authorization: Bearer <token>
```

### Schema

The default PostgREST schema is `public`.

```xpscript
Call db.SetSchema("private_api")
```

The client uses `Accept-Profile` and, for write operations, `Content-Profile`.

### Select

```xpscript
Dim result As JsonDocument

Set result = db.Select("customers")
```

This requests:

```text
/rest/v1/customers?select=*
```

A raw PostgREST query can be supplied:

```xpscript
Set result = db.Select(
    "customers",
    "select=id,name,email&status=eq.active&order=name.asc"
)
```

### Filters

`Eq` creates an escaped equality filter:

```xpscript
Dim filter As String
filter = db.Eq("id", 42)

Set result = db.Select("customers", "select=*&" & filter)
```

For advanced PostgREST filtering, pass the filter/query string directly.

### Insert

```xpscript
Dim customer As New JsonObject
Call customer.Set("name", "Ada")
Call customer.Set("email", "ada@example.com")

Set result = db.Insert("customers", customer)
```

Insert requests use `Prefer: return=representation`, so modified rows are returned as JSON.

### Update

```xpscript
Call customer.Set("name", "Ada Updated")

Set result = db.Update(
    "customers",
    db.Eq("id", 42),
    customer
)
```

Always use a filter when updating records.

### Upsert

```xpscript
Set result = db.Upsert("customers", customer)
```

Upsert uses PostgREST merge-duplicate semantics and returns the affected rows.

### Delete

```xpscript
Set result = db.Delete(
    "customers",
    db.Eq("id", 42)
)
```

A filter is mandatory.

### PostgreSQL RPC

```xpscript
Dim args As New JsonObject
Call args.Set("customer_id", 42)

Set result = db.Rpc("get_customer_summary", args)
```

This calls the PostgREST `/rpc/<function>` endpoint.

## SQL and database design

Data API keys do not automatically provide arbitrary SQL/DDL execution. XPScript therefore keeps SQL administration separate from normal data access.

### Supabase Cloud Management API

Configure the project reference and a Supabase Management API access token:

```xpscript
Call db.ConfigureCloudManagement(
    "project-ref",
    managementAccessToken
)
```

Then execute SQL:

```xpscript
Set result = db.ExecuteSql("select now()")
```

Database design operations use the same SQL execution path:

```xpscript
Set result = db.CreateTable(
    "create table if not exists app.customer (id bigint primary key, name text not null)"
)

Set result = db.AlterTable(
    "alter table app.customer add column email text"
)

Set result = db.CreateView(
    "create view app.active_customer as select * from app.customer where active = true"
)

Set result = db.AlterView(
    "create or replace view app.active_customer as select id, name from app.customer where active = true"
)
```

`CreateTable`, `AlterTable`, `CreateView`, and `AlterView` are convenience aliases for `ExecuteSql`. They do not parse or rewrite SQL.

### Self-hosted Supabase SQL administration

A self-hosted Supabase installation does not have to expose the Supabase Cloud Management API. Configure an administrator-controlled SQL HTTP endpoint explicitly:

```xpscript
Call db.ConfigureSqlEndpoint(
    "https://supabase.example.com/internal/sql",
    adminToken
)
```

The endpoint receives:

```json
{
  "query": "select now()"
}
```

and must return JSON. The token is sent as a bearer token.

This design keeps normal PostgREST access portable between Supabase Cloud and self-hosted installations while allowing each self-hosted deployment to choose how administrative SQL is exposed.

Do not expose an unrestricted SQL endpoint to browsers or untrusted clients. Treat `ExecuteSql` and all database design helpers as privileged server-side operations.

## HTTPDBDominoRest

`HTTPDBDominoRest` targets a self-hosted HCL Domino REST API server.

### Create a client

```xpscript
Dim domino As New HTTPDBDominoRest(
    "https://domino.example.com:8880",
    bearerToken,
    "customers"
)
```

Arguments:

1. Domino REST API server base URL.
2. Bearer token. It may be an empty string when `Login()` will be used.
3. Domino REST API `dataSource`, which is the configured API/scope name.

The constructor accepts a server URL, `/api/v1` URL, or `/api/setup-v1` URL and normalizes it to the server base URL.

### Login

Domino REST API can issue a bearer token using `/api/v1/auth`:

```xpscript
Dim token As String

token = domino.Login(
    "CN=Ada Lovelace/O=Example",
    password
)
```

The returned bearer token is retained by the client for later requests.

An externally acquired token can be supplied later:

```xpscript
Call domino.SetBearerToken(token)
```

### Change data source

```xpscript
Call domino.SetDataSource("sales")
```

### Create document

```xpscript
Dim data As New JsonObject
Call data.Set("Form", "Customer")
Call data.Set("Name", "Ada")
Call data.Set("Email", "ada@example.com")

Dim created As JsonDocument
Set created = domino.CreateDocument(data)
```

This uses:

```text
POST /api/v1/document?dataSource=<scope>
```

### Retrieve document

```xpscript
Set result = domino.GetDocument(
    "0123456789ABCDEF0123456789ABCDEF"
)
```

This uses:

```text
GET /api/v1/document/<UNID>?dataSource=<scope>
```

UNIDs must contain exactly 32 hexadecimal characters.

### Update document

Full update:

```xpscript
Set result = domino.UpdateDocument(unid, data)
```

Partial update:

```xpscript
Set result = domino.PatchDocument(unid, changes)
```

The methods target `/api/v1/document/<UNID>` using PUT and PATCH respectively.

### Delete document

```xpscript
If domino.DeleteDocument(unid) Then
    Print "Deleted"
End If
```

This uses the documented DELETE endpoint:

```text
DELETE /api/v1/document/<UNID>?dataSource=<scope>
```

### List configured views

```xpscript
Set result = domino.ListViews()
```

This uses:

```text
GET /api/v1/lists?dataSource=<scope>
```

### Read view entries

```xpscript
Set result = domino.GetView("People")
```

Query parameters can be supplied when needed:

```xpscript
Set result = domino.GetView(
    "People",
    "count=100&start=0"
)
```

This targets:

```text
GET /api/v1/lists/<view>?dataSource=<scope>&...
```

### Domino query

For a normal Domino query string:

```xpscript
Set result = domino.Query(
    "Form = 'Customer' and Status = 'Active'"
)
```

XPScript creates the request object with `query`, `viewRefresh`, and `noViews`, and posts it to:

```text
POST /api/v1/query?dataSource=<scope>&action=execute
```

A custom JSON query payload can also be supplied:

```xpscript
Dim query As New JsonObject
Call query.Set("query", "Form = 'Customer'")
Call query.Set("viewRefresh", True)
Call query.Set("noViews", False)

Set result = domino.Query(query)
```

### List forms

```xpscript
Set result = domino.ListForms()
```

This uses the setup API endpoint:

```text
GET /api/setup-v1/design/forms?dataSource=<scope>
```

The account/token must have the access required by the Domino REST API configuration.

### Logout

```xpscript
Call domino.Logout()
```

The client calls `/api/v1/auth/logout` and clears its retained bearer token after a successful response.

## Security

Use HTTPS for production Supabase and Domino connections.

Keep Supabase secret/service keys, Management API tokens, Domino passwords, and Domino bearer tokens on the server side. Do not embed administrative credentials in browser-delivered XPScript or client assets.

Normal Supabase CRUD operations remain subject to Postgres privileges and Row Level Security. Domino operations remain subject to the configured Domino REST API schema, scope, ACL, forms/modes, and user access.
