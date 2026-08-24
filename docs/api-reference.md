# XPScript runtime API reference

This is the searchable runtime-object reference for XPScript. Use [commands.md](commands.md) for language statements, operators, built-in scalar functions and compiler CLI options. Use this page for runtime objects such as HTTP, JSON, databases, AI, UI and web state.

Every row contains the member title, accepted syntax, parameters and their purpose, a short behavior description, and a complete `.xps` example that can be copied and compiled. The linked topical pages provide longer explanations and security guidance.

## Quick navigation

- [Application runtime](#application-runtime)
- [Native HTTP client](#native-http-client)
- [HTTP response](#http-response)
- [Native JSON](#native-json)
- [SQLite](#sqlite)
- [SQL Server](#sql-server)
- [Supabase HTTP database](#supabase-http-database)
- [Domino REST database](#domino-rest-database)
- [XPAi](#xpai)
- [AITool](#aitool)
- [UIForm](#uiform)
- [UIListView](#uilistview)
- [REST routes and binding](#rest-routes-and-binding)
- [Response](#response)
- [Session](#session)
- [Web Application state](#web-application-state)
- [RequestScope](#requestscope)

## Application runtime

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Application.ArgCount` | `Application.ArgCount` | none | Returns the number of program command-line arguments. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.Args` | `Application.Args(index)` | `index`: zero-based argument index. | Returns one program argument. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.CommandLine` | `Application.CommandLine` | none | Returns the convenience command-line string. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.ExecutablePath` | `Application.ExecutablePath` | none | Returns the full executable path. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.ExecutableDirectory` | `Application.ExecutableDirectory` | none | Returns the executable directory. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.TempPath` | `Application.TempPath` | none | Returns the operating-system temporary directory. | [application-runtime.xps](../samples/application-runtime.xps) |

## Native HTTP client

See [HTTP client](http-client.md) for request limits, UIForm helpers and security details.

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `HttpClient` | `Dim http As New HttpClient` | none | Creates an outgoing HTTP client. | [http-client.xps](../demo/http/http-client.xps) |
| `HttpClient.Get` | `http.Get(url)` | `url`: absolute HTTP/HTTPS URL. | Sends a GET request and returns `HttpResponse`. | [http-client.xps](../demo/http/http-client.xps) |
| `HttpClient.Post` | `http.Post(url, body [, contentType])` | `url`, request `body`, optional MIME `contentType`. | Sends a POST request. | [native-http-json.xps](../samples/native-http-json.xps) |
| `HttpClient.Put` | `http.Put(url, body [, contentType])` | `url`, `body`, optional `contentType`. | Sends a PUT request. | [native-http-regression.xps](../samples/native-http-regression.xps) |
| `HttpClient.Patch` | `http.Patch(url, body [, contentType])` | `url`, `body`, optional `contentType`. | Sends a PATCH request. | [native-http-regression.xps](../samples/native-http-regression.xps) |
| `HttpClient.Delete` | `http.Delete(url)` | `url`: absolute URL. | Sends a DELETE request. | [native-http-regression.xps](../samples/native-http-regression.xps) |
| `HttpClient.GetJson` | `http.GetJson(url)` | `url`: absolute URL. | Sends GET, requires success and returns `JsonDocument`. | [native-http-uiform-data.xps](../samples/native-http-uiform-data.xps) |
| `HttpClient.PostJson` | `http.PostJson(url, data)` | `url`, JSON-compatible `data`. | Sends JSON with POST. | [native-http-uiform-data.xps](../samples/native-http-uiform-data.xps) |
| `HttpClient.PutJson` | `http.PutJson(url, data)` | `url`, JSON-compatible `data`. | Sends JSON with PUT. | [native-http-uiform-data.xps](../samples/native-http-uiform-data.xps) |
| `HttpClient.PatchJson` | `http.PatchJson(url, data)` | `url`, JSON-compatible `data`. | Sends JSON with PATCH. | [native-http-uiform-data.xps](../samples/native-http-uiform-data.xps) |
| `HttpClient.AddQuery` | `http.AddQuery(url, name, value)` | base `url`, query `name`, query `value`. | URL-encodes and appends one query parameter. | [native-http-uiform-data.xps](../samples/native-http-uiform-data.xps) |
| `HttpClient.PostForm` | `http.PostForm(url, data)` | `url`, scalar `JsonObject` form `data`. | Sends `application/x-www-form-urlencoded`. | [native-http-uiform-data.xps](../samples/native-http-uiform-data.xps) |
| `HttpClient.LoadForm` | `http.LoadForm(form, url)` | `form`: `UIForm`; `url`: JSON-object endpoint. | GETs JSON and binds it to a form. | [native-http-uiform-data.xps](../samples/native-http-uiform-data.xps) |
| `HttpClient.SaveForm` | `http.SaveForm(form, url)` | `form`, destination `url`. | POSTs `form.Data` as JSON. | [native-http-uiform-data.xps](../samples/native-http-uiform-data.xps) |
| `HttpClient.PutForm` | `http.PutForm(form, url)` | `form`, destination `url`. | PUTs `form.Data` as JSON. | [native-http-uiform-data.xps](../samples/native-http-uiform-data.xps) |
| `HttpClient.SetHeader` | `http.SetHeader(name, value)` | header `name`, header `value`. | Adds or replaces a validated request header. | [http-client.xps](../demo/http/http-client.xps) |
| `HttpClient.RemoveHeader` | `http.RemoveHeader(name)` | header `name`. | Removes one caller-defined header. | [native-http-regression.xps](../samples/native-http-regression.xps) |
| `HttpClient.ClearHeaders` | `http.ClearHeaders()` | none | Clears caller-defined headers. | [native-http-regression.xps](../samples/native-http-regression.xps) |
| `HttpClient.Timeout` | `http.Timeout = seconds` | `seconds`: request timeout. | Gets or sets total request timeout. | [http-client.xps](../demo/http/http-client.xps) |
| `HttpClient.Dispose` | `http.Dispose()` | none | Cancels/releases client resources. | [http-client.xps](../demo/http/http-client.xps) |

## HTTP response

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `HttpResponse.StatusCode` | `response.StatusCode` | none | HTTP status code. | [http-client.xps](../demo/http/http-client.xps) |
| `HttpResponse.StatusText` | `response.StatusText` | none | HTTP reason/status text. | [native-http-json.xps](../samples/native-http-json.xps) |
| `HttpResponse.Body` | `response.Body` | none | Response body as text. | [http-client.xps](../demo/http/http-client.xps) |
| `HttpResponse.BodyLength` | `response.BodyLength` | none | Response-body byte length. | [native-http-regression.xps](../samples/native-http-regression.xps) |
| `HttpResponse.ContentType` | `response.ContentType` | none | Response content type. | [http-client.xps](../demo/http/http-client.xps) |
| `HttpResponse.Headers` | `response.Headers` | none | Response headers as the runtime header object. | [native-http-regression.xps](../samples/native-http-regression.xps) |
| `HttpResponse.IsSuccess` | `response.IsSuccess` | none | `True` for an HTTP success status. | [http-client.xps](../demo/http/http-client.xps) |
| `HttpResponse.Json` | `response.Json()` | none | Parses the body and returns `JsonDocument`. | [native-http-uiform-data.xps](../samples/native-http-uiform-data.xps) |
| `HttpResponse.SaveBodyToFile` | `response.SaveBodyToFile(path)` | destination `path`. | Saves the response body using runtime file-boundary rules. | [native-http-regression.xps](../samples/native-http-regression.xps) |

## Native JSON

See the runnable [native HTTP/JSON sample](../samples/native-http-json.xps).

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `JsonDocument.Parse` | `JsonDocument.Parse(text)` | JSON `text`. | Parses JSON into a `JsonDocument`. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonDocument.Stringify` | `document.Stringify()` | none | Serializes the document. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonObject.Get` | `obj.Get(name)` | property `name`. | Returns a property value or runtime null. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonObject.Set` | `obj.Set(name, value)` | property `name`, JSON-compatible `value`. | Adds or replaces a property. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonObject.Remove` | `obj.Remove(name)` | property `name`. | Removes a property. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonObject.Contains` | `obj.Contains(name)` | property `name`. | Tests whether the property exists. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonObject.Count` | `obj.Count` | none | Number of properties. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonArray.Add` | `arr.Add(value)` | JSON-compatible `value`. | Appends a value. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonArray.Get` | `arr.Get(index)` | zero-based `index`. | Returns one array value. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonArray.Set` | `arr.Set(index, value)` | `index`, replacement `value`. | Replaces one array element. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonArray.RemoveAt` | `arr.RemoveAt(index)` | zero-based `index`. | Removes one array element. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonArray.Count` | `arr.Count` | none | Number of array elements. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonElement.Type` | `element.Type` | none | Returns the JSON element type name. | [native-json-regression.xps](../samples/native-json-regression.xps) |
| `JsonElement.Value` | `element.Value` | none | Returns the scalar runtime value where applicable. | [native-json-regression.xps](../samples/native-json-regression.xps) |
| `JsonParse` | `JsonParse(text)` | JSON `text`. | Parses JSON. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonStringify` | `JsonStringify(value)` | JSON-compatible `value`. | Serializes a value as JSON. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonEncode` | `JsonEncode(value)` | JSON-compatible `value`. | Serializes with native JSON conversion rules. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonDecode` | `JsonDecode(text)` | JSON `text`. | Parses JSON and returns `JsonDocument`. | [native-http-json.xps](../samples/native-http-json.xps) |

## SQLite

See [SQLite database](sqlite.md) for path/security rules.

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `XPDBSQLite` | `New XPDBSQLite(path [, readOnly])` | relative database `path`; optional `readOnly`. | Opens SQLite immediately. | [sqlite-demo.xps](../demo/sqlite/sqlite-demo.xps) |
| `XPDBSQLite.Open` | `db.Open()` | none | Reopens a closed connection. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.Close` | `db.Close()` | none | Rolls back an active transaction and closes. | [sqlite-demo.xps](../demo/sqlite/sqlite-demo.xps) |
| `XPDBSQLite.Execute` | `db.Execute(sql [, parameters])` | SQL text; optional `JsonObject` parameters. | Executes non-query SQL and returns affected rows. | [sqlite-demo.xps](../demo/sqlite/sqlite-demo.xps) |
| `XPDBSQLite.Query` | `db.Query(sql [, parameters])` | SQL text; optional parameters. | Returns rows as JSON array document. | [sqlite-demo.xps](../demo/sqlite/sqlite-demo.xps) |
| `XPDBSQLite.Scalar` | `db.Scalar(sql [, parameters])` | SQL text; optional parameters. | Returns the first value or null. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.BeginTransaction` | `db.BeginTransaction()` | none | Starts one transaction. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.Commit` | `db.Commit()` | none | Commits the transaction. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.Rollback` | `db.Rollback()` | none | Rolls back the transaction. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.LastInsertRowId` | `db.LastInsertRowId` | none | Returns SQLite `last_insert_rowid()` for this connection. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.DatabasePath` | `db.DatabasePath` | none | Returns resolved file path or `:memory:`. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.ReadOnly` | `db.ReadOnly` | none | Reports read-only mode. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.IsOpen` | `db.IsOpen` | none | Reports whether connection is open. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.InTransaction` | `db.InTransaction` | none | Reports active transaction state. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.Timeout` | `db.Timeout = seconds` | `seconds`: 0.1 through 300. | Sets command timeout. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.MaxRows` | `db.MaxRows = count` | `count`: 1 through 50000. | Bounds rows returned by `Query`. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |

## SQL Server

See [SQL Server database](mssql.md).

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `XPDbMsSql` | `New XPDbMsSql(connectionString)` | SQL Server `connectionString`. | Validates and opens the connection. | [mssql-demo.xps](../demo/mssql/mssql-demo.xps) |
| `XPDbMsSql.Open` | `db.Open()` | none | Reopens a closed connection. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPDbMsSql.Close` | `db.Close()` | none | Rolls back active transaction and closes. | [mssql-demo.xps](../demo/mssql/mssql-demo.xps) |
| `XPDbMsSql.Execute` | `db.Execute(sql [, parameters])` | SQL; optional `JsonObject` parameters. | Executes non-query SQL. | [mssql-demo.xps](../demo/mssql/mssql-demo.xps) |
| `XPDbMsSql.Query` | `db.Query(sql [, parameters])` | SQL; optional parameters. | Returns rows as JSON array document. | [mssql-demo.xps](../demo/mssql/mssql-demo.xps) |
| `XPDbMsSql.Scalar` | `db.Scalar(sql [, parameters])` | SQL; optional parameters. | Returns first value or null. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPDbMsSql.BeginTransaction` | `db.BeginTransaction()` | none | Starts one transaction. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPDbMsSql.Commit` | `db.Commit()` | none | Commits active transaction. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPDbMsSql.Rollback` | `db.Rollback()` | none | Rolls back active transaction. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPDbMsSql.DataSource` | `db.DataSource` | none | Returns connected server name while open. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPDbMsSql.Database` | `db.Database` | none | Returns connected database name while open. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPDbMsSql.IsOpen` | `db.IsOpen` | none | Reports open state. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPDbMsSql.InTransaction` | `db.InTransaction` | none | Reports transaction state. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPDbMsSql.Timeout` | `db.Timeout = seconds` | `seconds`: 0.1 through 300. | Sets command timeout. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPDbMsSql.MaxRows` | `db.MaxRows = count` | `count`: 1 through 50000. | Bounds query row count. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |

## Supabase HTTP database

See [HTTP database clients](httpdb.md).

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `HTTPDBSupabase` | `New HTTPDBSupabase(baseUrl, apiKey)` | Supabase `baseUrl`, API `apiKey`. | Creates a Cloud or self-hosted PostgREST client. | [httpdb-supabase.xps](../samples/httpdb-supabase.xps) |
| `SetBearerToken` | `db.SetBearerToken(token)` | user/service bearer `token`. | Sets Authorization bearer context. | [httpdb-supabase.xps](../samples/httpdb-supabase.xps) |
| `SetSchema` | `db.SetSchema(schema)` | PostgREST `schema`. | Selects Accept/Content profile schema. | [httpdb-supabase.xps](../samples/httpdb-supabase.xps) |
| `Select` | `db.Select(table [, query])` | table; optional PostgREST query. | Reads table rows. | [httpdb-supabase.xps](../samples/httpdb-supabase.xps) |
| `Eq` | `db.Eq(column, value)` | column and scalar value. | Creates an encoded equality filter. | [httpdb-supabase.xps](../samples/httpdb-supabase.xps) |
| `Insert` | `db.Insert(table, data)` | table and JSON data. | Inserts and returns representation. | [httpdb-supabase.xps](../samples/httpdb-supabase.xps) |
| `Update` | `db.Update(table, filter, data)` | table, required filter, JSON data. | Updates matching rows. | [httpdb-supabase.xps](../samples/httpdb-supabase.xps) |
| `Upsert` | `db.Upsert(table, data)` | table and JSON data. | Performs merge-duplicate upsert. | [httpdb-supabase.xps](../samples/httpdb-supabase.xps) |
| `Delete` | `db.Delete(table, filter)` | table and mandatory filter. | Deletes matching rows. | [httpdb-supabase.xps](../samples/httpdb-supabase.xps) |
| `Rpc` | `db.Rpc(name, arguments)` | RPC function `name`, JSON `arguments`. | Calls PostgREST RPC. | [httpdb-supabase.xps](../samples/httpdb-supabase.xps) |
| `ConfigureCloudManagement` | `db.ConfigureCloudManagement(projectRef, token)` | project reference and management token. | Enables Supabase Cloud SQL administration. | [httpdb-supabase.xps](../samples/httpdb-supabase.xps) |
| `ConfigureSqlEndpoint` | `db.ConfigureSqlEndpoint(url, token)` | admin SQL endpoint and bearer token. | Configures a self-hosted SQL administration endpoint. | [httpdb-supabase.xps](../samples/httpdb-supabase.xps) |
| `ExecuteSql` | `db.ExecuteSql(sql)` | privileged SQL text. | Executes SQL through configured administration path. | [httpdb-supabase.xps](../samples/httpdb-supabase.xps) |
| `CreateTable` | `db.CreateTable(sql)` | CREATE TABLE SQL. | Convenience alias for privileged SQL execution. | [httpdb-supabase.xps](../samples/httpdb-supabase.xps) |
| `AlterTable` | `db.AlterTable(sql)` | ALTER TABLE SQL. | Convenience SQL administration alias. | [httpdb-supabase.xps](../samples/httpdb-supabase.xps) |
| `CreateView` | `db.CreateView(sql)` | CREATE VIEW SQL. | Convenience SQL administration alias. | [httpdb-supabase.xps](../samples/httpdb-supabase.xps) |
| `AlterView` | `db.AlterView(sql)` | CREATE OR REPLACE/ALTER view SQL. | Convenience SQL administration alias. | [httpdb-supabase.xps](../samples/httpdb-supabase.xps) |

## Domino REST database

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `HTTPDBDominoRest` | `New HTTPDBDominoRest(baseUrl, bearerToken, dataSource)` | server URL, optional retained bearer token, REST API data source. | Creates an HCL Domino REST API client. | [httpdb-domino.xps](../samples/httpdb-domino.xps) |
| `Login` | `domino.Login(user, password)` | Domino user name and password. | Authenticates and retains returned bearer token. | [httpdb-domino.xps](../samples/httpdb-domino.xps) |
| `SetBearerToken` | `domino.SetBearerToken(token)` | bearer token. | Replaces retained token. | [httpdb-domino.xps](../samples/httpdb-domino.xps) |
| `SetDataSource` | `domino.SetDataSource(name)` | configured REST API data source. | Changes active data source. | [httpdb-domino.xps](../samples/httpdb-domino.xps) |
| `CreateDocument` | `domino.CreateDocument(data)` | document `JsonObject`. | Creates a Domino document. | [httpdb-domino.xps](../samples/httpdb-domino.xps) |
| `GetDocument` | `domino.GetDocument(unid)` | 32-hex-character UNID. | Retrieves one document. | [httpdb-domino.xps](../samples/httpdb-domino.xps) |
| `UpdateDocument` | `domino.UpdateDocument(unid, data)` | UNID and full document data. | Replaces/updates a document with PUT. | [httpdb-domino.xps](../samples/httpdb-domino.xps) |
| `PatchDocument` | `domino.PatchDocument(unid, changes)` | UNID and partial JSON changes. | Partially updates a document. | [httpdb-domino.xps](../samples/httpdb-domino.xps) |
| `DeleteDocument` | `domino.DeleteDocument(unid)` | document UNID. | Deletes a document. | [httpdb-domino.xps](../samples/httpdb-domino.xps) |
| `ListViews` | `domino.ListViews()` | none | Lists configured views. | [httpdb-domino.xps](../samples/httpdb-domino.xps) |
| `GetView` | `domino.GetView(view [, query])` | view name; optional query string. | Reads view entries. | [httpdb-domino.xps](../samples/httpdb-domino.xps) |
| `Query` | `domino.Query(query)` | Domino query string or JSON payload. | Executes Domino query API. | [httpdb-domino.xps](../samples/httpdb-domino.xps) |
| `ListForms` | `domino.ListForms()` | none | Lists configured forms through setup API. | [httpdb-domino.xps](../samples/httpdb-domino.xps) |
| `Logout` | `domino.Logout()` | none | Logs out and clears retained bearer token. | [httpdb-domino.xps](../samples/httpdb-domino.xps) |

## XPAi

See [XPAi client](ai.md) and [tools/session memory](ai-tools-sessions.md).

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `XPAi` | `New XPAi(endpoint [, apiKey])` | absolute endpoint; optional API key. | Creates a custom OpenAI-compatible client. | [ai-tool-demo.xps](../demo/ai/ai-tool-demo.xps) |
| `XPAi preset` | `New XPAi(preset, apiKey [, providerConfiguration])` | provider preset, key, optional provider config. | Creates OpenAI, Claude, OpenRouter or Azure client. | [xpai.xps](../samples/xpai.xps) |
| `EndpointPath` | `ai.EndpointPath = path` | origin-relative request `path`. | Replaces path without changing configured origin. | [xpai.xps](../samples/xpai.xps) |
| `Model` | `ai.Model = model` | provider model identifier. | Sets default model. | [ai-tool-demo.xps](../demo/ai/ai-tool-demo.xps) |
| `Temperature` | `ai.Temperature = value` | number from 0 through 2. | Sets sampling temperature. | [xpai.xps](../samples/xpai.xps) |
| `MaxOutputTokens` | `ai.MaxOutputTokens = count` | token count. | Sets output token limit. | [xpai.xps](../samples/xpai.xps) |
| `Timeout` | `ai.Timeout = seconds` | seconds from 0.1 through 3600. | Sets total request timeout. | [xpai.xps](../samples/xpai.xps) |
| `AddMessage` | `ai.AddMessage(role, content)` | role and message content. | Adds system/user/assistant message. | [ai-tool-demo.xps](../demo/ai/ai-tool-demo.xps) |
| `GetMessages` | `ai.GetMessages()` | none | Returns cloned message array document. | [xpai.xps](../samples/xpai.xps) |
| `ClearMessages` | `ai.ClearMessages()` | none | Clears stored messages. | [xpai.xps](../samples/xpai.xps) |
| `SetOption` | `ai.SetOption(name, value)` | request JSON property and value. | Adds/replaces optional provider request property. | [xpai.xps](../samples/xpai.xps) |
| `RemoveOption` | `ai.RemoveOption(name)` | option name. | Removes one request option. | [xpai.xps](../samples/xpai.xps) |
| `ClearOptions` | `ai.ClearOptions()` | none | Clears extra request options. | [xpai.xps](../samples/xpai.xps) |
| `SetHeader` | `ai.SetHeader(name, value)` | validated header name/value. | Adds/replaces provider header. | [xpai.xps](../samples/xpai.xps) |
| `RemoveHeader` | `ai.RemoveHeader(name)` | header name. | Removes one caller-defined header. | [xpai.xps](../samples/xpai.xps) |
| `ClearHeaders` | `ai.ClearHeaders()` | none | Clears caller-defined headers. | [xpai.xps](../samples/xpai.xps) |
| `Complete` | `ai.Complete([messages [, model]])` | optional messages and one-request model. | Sends non-streaming request and can execute registered tools. | [ai-tool-demo.xps](../demo/ai/ai-tool-demo.xps) |
| `Stream` | `ai.Stream([messages,] callback [, model])` | callback; optional messages/model. | Streams SSE text chunks to callback. | [xpai.xps](../samples/xpai.xps) |
| `StreamCallback` | `ai.StreamCallback(callback, context...)` | callback name plus fixed context values. | Streams chunks with callback context arguments. | [xpai.xps](../samples/xpai.xps) |
| `Cancel` | `ai.Cancel()` | none | Cancels active request/stream. | [xpai.xps](../samples/xpai.xps) |
| `Dispose` | `ai.Dispose()` | none | Cancels active work and releases resources. | [xpai.xps](../samples/xpai.xps) |
| `AutoExecuteTools` | `ai.AutoExecuteTools = value` | Boolean. | Enables/disables automatic non-streaming tool execution. | [ai-tool-demo.xps](../demo/ai/ai-tool-demo.xps) |
| `MaxToolIterations` | `ai.MaxToolIterations = count` | 1 through 32. | Bounds automatic tool/model continuation. | [ai-tool-demo.xps](../demo/ai/ai-tool-demo.xps) |
| `AddTool` | `ai.AddTool(tool)` | registered `AITool`. | Adds a tool to this client. | [ai-tool-demo.xps](../demo/ai/ai-tool-demo.xps) |
| `RemoveTool` | `ai.RemoveTool(name)` | tool name. | Removes a tool and returns whether it existed. | [xpai.xps](../samples/xpai.xps) |
| `ClearTools` | `ai.ClearTools()` | none | Removes all registered tools. | [xpai.xps](../samples/xpai.xps) |
| `HasTool` | `ai.HasTool(name)` | tool name. | Tests tool registration. | [xpai.xps](../samples/xpai.xps) |
| `GetTool` | `ai.GetTool(name)` | tool name. | Returns a registered tool. | [xpai.xps](../samples/xpai.xps) |
| `GetToolNames` | `ai.GetToolNames()` | none | Returns deterministic tool names. | [xpai.xps](../samples/xpai.xps) |
| `ToolCount` | `ai.ToolCount()` | none | Returns registered tool count. | [xpai.xps](../samples/xpai.xps) |
| `SessionId` | `ai.SessionId` | none | Returns captured provider session identifier. | [xpai.xps](../samples/xpai.xps) |
| `HasSession` | `ai.HasSession` | none | Reports whether a session ID is stored. | [xpai.xps](../samples/xpai.xps) |
| `SessionRequestProperty` | `ai.SessionRequestProperty = name` | provider continuation property name. | Selects JSON field used to send stored session ID. | [xpai.xps](../samples/xpai.xps) |
| `ResetSession` | `ai.ResetSession()` | none | Clears stored session ID only. | [xpai.xps](../samples/xpai.xps) |
| `NewRequest` | `ai.NewRequest()` | none | Clears messages/session while preserving client config/tools. | [xpai.xps](../samples/xpai.xps) |

## AITool

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `AITool` | `New AITool(name)` | logical tool name. | Creates an AI tool container. | [ai-tool-demo.xps](../demo/ai/ai-tool-demo.xps) |
| `Description` | `tool.Description = text` | provider-visible description. | Sets tool description. | [ai-tool-demo.xps](../demo/ai/ai-tool-demo.xps) |
| `Timeout` | `tool.Timeout = seconds` | 0.1 through 3600 seconds. | Sets tool timeout metadata. | [xpai.xps](../samples/xpai.xps) |
| `AddFunction` | `tool.AddFunction(name, description, callback [, context...])` | AI name, description, module callback, optional fixed context. | Registers allowed AI function to callback mapping. | [ai-tool-demo.xps](../demo/ai/ai-tool-demo.xps) |
| `AddParameter` | `tool.AddParameter(functionName, name, type, description, required)` | target function; parameter name/type/description/required flag. | Adds JSON-schema function parameter. | [ai-tool-demo.xps](../demo/ai/ai-tool-demo.xps) |
| `RemoveFunction` | `tool.RemoveFunction(name)` | function name. | Removes function. | [xpai.xps](../samples/xpai.xps) |
| `HasFunction` | `tool.HasFunction(name)` | function name. | Tests function registration. | [xpai.xps](../samples/xpai.xps) |
| `GetFunction` | `tool.GetFunction(name)` | function name. | Returns registered function object. | [xpai.xps](../samples/xpai.xps) |
| `GetFunctionNames` | `tool.GetFunctionNames()` | none | Returns registered function names. | [xpai.xps](../samples/xpai.xps) |
| `FunctionCount` | `tool.FunctionCount()` | none | Returns number of functions. | [ai-tool-demo.xps](../demo/ai/ai-tool-demo.xps) |
| `SetRequestProperty` | `tool.SetRequestProperty(name, value)` | context property and JSON-compatible value. | Adds/replaces isolated tool request context. | [xpai.xps](../samples/xpai.xps) |
| `RemoveRequestProperty` | `tool.RemoveRequestProperty(name)` | context property name. | Removes context property. | [xpai.xps](../samples/xpai.xps) |
| `ClearRequestProperties` | `tool.ClearRequestProperties()` | none | Clears context properties. | [xpai.xps](../samples/xpai.xps) |
| `GetRequestContext` | `tool.GetRequestContext()` | none | Returns defensive JSON context copy. | [xpai.xps](../samples/xpai.xps) |
| `ToJsonObject` | `tool.ToJsonObject()` | none | Returns tool metadata as `JsonObject`. | [xpai.xps](../samples/xpai.xps) |
| `ToJson` | `tool.ToJson()` | none | Serializes tool metadata. | [xpai.xps](../samples/xpai.xps) |
| `AIToolFunction.AddParameter` | `fn.AddParameter(name, type, description, required)` | parameter schema fields. | Adds parameter directly to function object. | [xpai.xps](../samples/xpai.xps) |
| `AIToolFunction.RemoveParameter` | `fn.RemoveParameter(name)` | parameter name. | Removes parameter. | [xpai.xps](../samples/xpai.xps) |
| `AIToolFunction.HasParameter` | `fn.HasParameter(name)` | parameter name. | Tests parameter registration. | [xpai.xps](../samples/xpai.xps) |
| `AIToolFunction.ParameterCount` | `fn.ParameterCount()` | none | Returns parameter count. | [xpai.xps](../samples/xpai.xps) |

`AIToolCall` passed to callbacks exposes `ToolName`, `FunctionName`, `CallId`, `Arguments` and `SessionId`. The compiler verifies static `AddFunction` callback names and arity.

## UIForm

See [UIForm](uiform.md) and [extended fields](uiform-fields.md).

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `UIForm` | `New UIForm(title)` | form title. | Creates shared desktop/web/browser form. | [customer-form.xps](../demo/desktop-ui/customer-form.xps) |
| `BindData` | `form.BindData(data)` | `JsonObject` or object-root `JsonDocument`. | Binds form values to JSON data. | [ui-form-core.xps](../samples/ui-form-core.xps) |
| `AddTextField` | `form.AddTextField(name, label)` | binding name and display label. | Adds text input. | [customer-form.xps](../demo/desktop-ui/customer-form.xps) |
| `AddTextArea` | `form.AddTextArea(name, label)` | name and label. | Adds multiline text input. | [ui-form-core.xps](../samples/ui-form-core.xps) |
| `AddNumberField` | `form.AddNumberField(name, label)` | name and label. | Adds numeric field. | [ui-form-core.xps](../samples/ui-form-core.xps) |
| `AddCheckBox` | `form.AddCheckBox(name, label)` | name and label. | Adds Boolean checkbox. | [ui-form-core.xps](../samples/ui-form-core.xps) |
| `AddDateField` | `form.AddDateField(name, label)` | name and label. | Adds date field. | [ui-form-core.xps](../samples/ui-form-core.xps) |
| `AddSelect` | `form.AddSelect(name, label)` | name and label. | Adds single-select control. | [ui-form-listboxes.xps](../samples/ui-form-listboxes.xps) |
| `AddRadioGroup` | `form.AddRadioGroup(name, label)` | name and label. | Adds radio group. | [ui-form-core.xps](../samples/ui-form-core.xps) |
| `AddListBox` | `form.AddListBox(name, label)` | name and label. | Adds single-selection list box. | [ui-form-listboxes.xps](../samples/ui-form-listboxes.xps) |
| `AddMultiListBox` | `form.AddMultiListBox(name, label)` | name and label. | Adds multi-selection list box. | [ui-form-listboxes.xps](../samples/ui-form-listboxes.xps) |
| `AddOption` | `form.AddOption(field, value [, label])` | field name, option value, optional label. | Adds option to selectable control. | [ui-form-listboxes.xps](../samples/ui-form-listboxes.xps) |
| `ClearOptions` | `form.ClearOptions(field)` | field name. | Removes configured options. | [ui-form-listboxes.xps](../samples/ui-form-listboxes.xps) |
| `GetFieldValues` | `form.GetFieldValues(field)` | field name. | Returns selected values as `JsonArray`. | [ui-form-listboxes.xps](../samples/ui-form-listboxes.xps) |
| `SetRequired` | `form.SetRequired(field, required)` | field name and Boolean. | Sets required validation. | [customer-form.xps](../demo/desktop-ui/customer-form.xps) |
| `SetFieldPlaceholder` | `form.SetFieldPlaceholder(field, text)` | field and hint text. | Sets/clears placeholder. | [ui-form-core.xps](../samples/ui-form-core.xps) |
| `SetFieldTooltip` | `form.SetFieldTooltip(field, text)` | field and tooltip text. | Sets/clears tooltip. | [ui-form-core.xps](../samples/ui-form-core.xps) |
| `SetDateRange` | `form.SetDateRange(field, min, max)` | date field, min/max ISO date. | Configures date bounds. | [ui-form-core.xps](../samples/ui-form-core.xps) |
| `SetTimeRange` | `form.SetTimeRange(field, min, max)` | time field, min/max time. | Configures time bounds. | [ui-form-core.xps](../samples/ui-form-core.xps) |
| `SetDateTimeRange` | `form.SetDateTimeRange(field, min, max)` | datetime field, min/max local datetime. | Configures datetime bounds. | [ui-form-core.xps](../samples/ui-form-core.xps) |
| `SetMonthRange` | `form.SetMonthRange(field, min, max)` | month field, `yyyy-MM` bounds. | Configures month bounds. | [ui-form-core.xps](../samples/ui-form-core.xps) |
| `AddSeparator` | `form.AddSeparator(name)` | structural field name. | Adds visual separator. | [ui-form-structural-elements.xps](../samples/ui-form-structural-elements.xps) |
| `AddSpacer` | `form.AddSpacer(name)` | structural field name. | Adds spacing block. | [ui-form-structural-elements.xps](../samples/ui-form-structural-elements.xps) |
| `AddGridColumns` | `form.AddGridColumns(columns)` | total grid column count. | Creates shared layout grid. | [customer-form.xps](../demo/browser-wasm/customer-form.xps) |
| `Grid.SetFieldPosition` | `grid.SetFieldPosition(field, columns)` | field name and span. | Sets field width/span in current row. | [customer-form.xps](../demo/browser-wasm/customer-form.xps) |
| `Grid.AddNewRow` | `grid.AddNewRow()` | none | Forces next field to a new row. | [browser-wasm-uiform.xps](../samples/browser-wasm-uiform.xps) |
| `ShowDialog` | `form.ShowDialog()` | none | Renders/shows the active UI backend and returns its state/result. | [customer-form.xps](../demo/desktop-ui/customer-form.xps) |
| `AddFileField` | `form.AddFileField(name, label)` | name and label. | Adds file-upload/path field. | [ui-form-additional-fields.xps](../samples/ui-form-additional-fields.xps) |
| `SetFileOptions` | `form.SetFileOptions(field, accept, maxBytes, multiple)` | field, accept list, per-file limit, multiple flag. | Configures file input. | [ui-form-additional-fields.xps](../samples/ui-form-additional-fields.xps) |
| `AddMultiSelect` | `form.AddMultiSelect(name, label)` | name and label. | Adds multi-select field. | [ui-form-additional-fields.xps](../samples/ui-form-additional-fields.xps) |
| `AddCheckBoxGroup` | `form.AddCheckBoxGroup(name, label)` | name and label. | Adds checkbox group. | [ui-form-additional-fields.xps](../samples/ui-form-additional-fields.xps) |
| `AddTelField` | `form.AddTelField(name, label)` | name and label. | Adds telephone field. | [ui-form-additional-fields.xps](../samples/ui-form-additional-fields.xps) |
| `AddWeekField` | `form.AddWeekField(name, label)` | name and label. | Adds ISO week field. | [ui-form-additional-fields.xps](../samples/ui-form-additional-fields.xps) |
| `AddDecimalField` | `form.AddDecimalField(name, label)` | name and label. | Adds decimal numeric field. | [ui-form-additional-fields.xps](../samples/ui-form-additional-fields.xps) |
| `AddCurrencyField` | `form.AddCurrencyField(name, label, currency)` | name, label, 3-letter currency code. | Adds currency field. | [ui-form-additional-fields.xps](../samples/ui-form-additional-fields.xps) |
| `AddRichTextField` | `form.AddRichTextField(name, label)` | name and label. | Adds rich-text field. | [ui-form-additional-fields.xps](../samples/ui-form-additional-fields.xps) |
| `SetLength` | `form.SetLength(field, min, max)` | field and length bounds. | Configures text length validation. | [ui-form-additional-fields.xps](../samples/ui-form-additional-fields.xps) |
| `SetNumberRange` | `form.SetNumberRange(field, min, max)` | numeric field and bounds. | Configures numeric bounds. | [ui-form-additional-fields.xps](../samples/ui-form-additional-fields.xps) |
| `AddLookupField` | `form.AddLookupField(name, label, url [, valueField, labelField, remote])` | field metadata, endpoint, optional property names and remote-search flag. | Adds endpoint-backed lookup field. | [ui-form-lookup-fields.xps](../samples/ui-form-lookup-fields.xps) |
| `AddAutoCompleteField` | `form.AddAutoCompleteField(name, label, url [, valueField, labelField, remote])` | field metadata and endpoint configuration. | Adds endpoint-backed autocomplete. | [ui-form-lookup-fields.xps](../samples/ui-form-lookup-fields.xps) |
| `SetRemoteSearchOptions` | `form.SetRemoteSearchOptions(field, searchParam, valueParam, minChars, maxResults)` | lookup field and remote-search settings. | Configures server-side lookup/autocomplete search. | [ui-form-lookup-fields.xps](../samples/ui-form-lookup-fields.xps) |
| `IsDirty` | `form.IsDirty` | none | Reports whether bound field data differs from clean baseline. | [ui-form-additional-fields.xps](../samples/ui-form-additional-fields.xps) |
| `DirtyFields` | `form.DirtyFields` | none | Returns names of currently dirty fields. | [ui-form-additional-fields.xps](../samples/ui-form-additional-fields.xps) |

## UIListView

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `UIListView` | `New UIListView(title)` | list title. | Creates list/table UI model. | [ui-list-view-web.xps](../samples/ui-list-view-web.xps) |
| `BindData` | `list.BindData(rows)` | `JsonArray` rows. | Binds list data. | [ui-list-view-web.xps](../samples/ui-list-view-web.xps) |
| `AddColumn` | `list.AddColumn(name, label)` | row property name and display label. | Adds visible column. | [ui-list-view-web.xps](../samples/ui-list-view-web.xps) |
| `SetColumnWidth` | `list.SetColumnWidth(name, width)` | column name and pixel width. | Configures column width. | [ui-list-view-web.xps](../samples/ui-list-view-web.xps) |
| `SetColumnLabel` | `list.SetColumnLabel(name, label)` | column name and new label. | Changes label, including from callbacks. | [ui-list-view-web.xps](../samples/ui-list-view-web.xps) |
| `SetKeyField` | `list.SetKeyField(name)` | row property used as key. | Configures selected key. | [ui-list-view-web.xps](../samples/ui-list-view-web.xps) |
| `SetSortable` | `list.SetSortable(value)` | Boolean. | Enables/disables sorting. | [ui-list-view-web.xps](../samples/ui-list-view-web.xps) |
| `SetFilterEnabled` | `list.SetFilterEnabled(value)` | Boolean. | Enables/disables filtering UI. | [ui-list-view-web.xps](../samples/ui-list-view-web.xps) |
| `SetOnSelect` | `list.SetOnSelect(callback)` | registered module callback name. | Invokes callback when selection changes. | [ui-list-view-web.xps](../samples/ui-list-view-web.xps) |
| `SetOnDoubleClick` | `list.SetOnDoubleClick(callback)` | registered callback name. | Invokes callback on double-click/open. | [ui-list-view-web.xps](../samples/ui-list-view-web.xps) |
| `AddRowButton` | `list.AddRowButton(name, label, callback)` | action name, label, callback. | Adds handler action per row. | [ui-list-view-web.xps](../samples/ui-list-view-web.xps) |
| `AddRowNavigationButton` | `list.AddRowNavigationButton(name, label, target)` | action name, label, target script. | Adds navigation action per row. | [ui-list-view-web.xps](../samples/ui-list-view-web.xps) |
| `SetRowAction` | `list.SetRowAction(target)` | target script. | Configures default row navigation target. | [ui-list-view-web.xps](../samples/ui-list-view-web.xps) |
| `GetSelectedRow` | `list.GetSelectedRow()` | none | Returns selected row object. | [ui-list-view-web.xps](../samples/ui-list-view-web.xps) |
| `GetSelectedKey` | `list.GetSelectedKey()` | none | Returns selected row key. | [ui-list-view-web.xps](../samples/ui-list-view-web.xps) |
| `RemoveSelectedRow` | `list.RemoveSelectedRow()` | none | Removes selected bound row. | [ui-list-view-web.xps](../samples/ui-list-view-web.xps) |
| `ShowDialog` | `list.ShowDialog()` | none | Renders/shows list UI. | [ui-list-view-web.xps](../samples/ui-list-view-web.xps) |

## REST routes and binding

See [REST API development](rest-api.md). The complete runnable demo is [`demo/rest-api/users.xps`](../demo/rest-api/users.xps).

| Rule/member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Anonymous` | `[Anonymous]` | none | Allows unauthenticated route access. | [users.xps](../demo/rest-api/users.xps) |
| `Authenticated` | `[Authenticated]` | none | Requires authenticated request/session. | [web-rest-api.xps](../samples/web-rest-api.xps) |
| `Get` | `[Get]` | none | Allows HTTP GET. | [users.xps](../demo/rest-api/users.xps) |
| `Post` | `[Post]` | none | Allows HTTP POST. | [users.xps](../demo/rest-api/users.xps) |
| `Route` | `[Route:/api/items/{id}]` | explicit path template and optional `{parameter}` segments. | Creates filename-independent REST route. | [users.xps](../demo/rest-api/users.xps) |
| `Role` | `[Role:admin;user]` | semicolon-separated allowed/forbidden roles; prefix forbidden role with `!`. | Applies route role authorization. | [web-rest-api.xps](../samples/web-rest-api.xps) |
| `FromRoute` | `[FromRoute] id As Integer` | procedure parameter. | Binds route segment. | [users.xps](../demo/rest-api/users.xps) |
| `FromQuery` | `[FromQuery] q As String` | procedure parameter. | Binds query-string value. | [web-rest-api.xps](../samples/web-rest-api.xps) |
| `FromHeader` | `[FromHeader] value As String` | parameter; optional explicit header name form. | Binds request header. | [web-rest-api.xps](../samples/web-rest-api.xps) |
| `FromBody` | `[FromBody] payload As Model` | typed request model parameter. | Binds JSON request body. | [users.xps](../demo/rest-api/users.xps) |
| `Required` | `[Required]` | model field. | Requires a REST model field. | [users.xps](../demo/rest-api/users.xps) |
| `MaxLength` | `[MaxLength:n]` | maximum character count. | Validates REST model text length. | [web-rest-api.xps](../samples/web-rest-api.xps) |
| `Email` | `[Email]` | model field. | Validates email-shaped input. | [users.xps](../demo/rest-api/users.xps) |
| `Range` | `[Range:min;max]` | numeric minimum and maximum. | Validates numeric range. | [web-rest-api.xps](../samples/web-rest-api.xps) |
| `Cors` | `[Cors]`, `[Cors:*]` or `[Cors:origin;origin]` | optional allowed origins. | Enables route CORS and preflight handling. | [web-rest-api.xps](../samples/web-rest-api.xps) |
| `RateLimit` | `[RateLimit:requests;window]` | request count and `s/m/h/d` window. | Applies fixed-window route rate limit. | [web-rest-api.xps](../samples/web-rest-api.xps) |
| `PreCompile` | `[PreCompile:file.xps]` | target route file. | Warms another route compilation. | [web-precompile.xps](../samples/web-precompile.xps) |
| `Platform:browser-wasm` | `[Platform:browser-wasm]` | none | Marks a web-served UI application for browser-WASM compilation. | [customer-form.xps](../demo/browser-wasm/customer-form.xps) |

## Response

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `StatusCode` | `Response.StatusCode = code` | HTTP status code. | Gets/sets response status. | [web-response-runtime.xps](../samples/web-response-runtime.xps) |
| `ContentType` | `Response.ContentType = value` | MIME type. | Gets/sets response content type. | [index.xps](../demo/web/index.xps) |
| `SetHeader` | `Response.SetHeader(name, value)` | header name/value. | Adds/replaces response header. | [web-response-runtime.xps](../samples/web-response-runtime.xps) |
| `AppendHeader` | `Response.AppendHeader(name, value)` | header name/value. | Appends response header value. | [web-response-runtime.xps](../samples/web-response-runtime.xps) |
| `RemoveHeader` | `Response.RemoveHeader(name)` | header name. | Removes response header. | [web-response-runtime.xps](../samples/web-response-runtime.xps) |
| `SetCookie` | `Response.SetCookie(name, value, options)` | cookie name/value/options. | Writes validated cookie. | [web-response-runtime.xps](../samples/web-response-runtime.xps) |
| `DeleteCookie` | `Response.DeleteCookie(name, path, secure, sameSite, domain)` | cookie name and deletion attributes. | Expires cookie. | [web-response-runtime.xps](../samples/web-response-runtime.xps) |
| `Write` | `Response.Write(value)` | value to append. | Writes response text. | [index.xps](../demo/web/index.xps) |
| `WriteBinary` | `Response.WriteBinary(value)` | binary-compatible value. | Writes binary response bytes. | [web-response-runtime.xps](../samples/web-response-runtime.xps) |
| `SendFile` | `Response.SendFile(...)` | file path/options. | Sends allowed file response. | [web-response-runtime.xps](../samples/web-response-runtime.xps) |
| `Clear` | `Response.Clear()` | none | Clears response body/state that can be reset. | [web-response-runtime.xps](../samples/web-response-runtime.xps) |
| `Redirect` | `Response.Redirect(url, statusCode)` | target URL and redirect status. | Produces redirect response. | [web-response-runtime.xps](../samples/web-response-runtime.xps) |
| `Complete` | `Response.Complete()` | none | Marks response completed. | [web-response-runtime.xps](../samples/web-response-runtime.xps) |
| `Json` | `Response.Json(data)` | JSON-compatible data. | Returns HTTP 200 JSON. | [web-rest-api.xps](../samples/web-rest-api.xps) |
| `OK` | `Response.OK(data)` | JSON-compatible data. | Returns HTTP 200 JSON. | [users.xps](../demo/rest-api/users.xps) |
| `Created` | `Response.Created(location, data)` | resource location and data. | Returns HTTP 201 plus `Location`. | [users.xps](../demo/rest-api/users.xps) |
| `NoContent` | `Response.NoContent()` | none | Returns HTTP 204. | [web-rest-api.xps](../samples/web-rest-api.xps) |
| `BadRequest` | `Response.BadRequest(detail)` | problem detail. | Returns HTTP 400 Problem Details. | [web-rest-api.xps](../samples/web-rest-api.xps) |
| `NotFound` | `Response.NotFound(detail)` | problem detail. | Returns HTTP 404 Problem Details. | [web-rest-api.xps](../samples/web-rest-api.xps) |
| `Unauthorized` | `Response.Unauthorized([detail])` | optional problem detail. | Returns HTTP 401. | [web-rest-api.xps](../samples/web-rest-api.xps) |
| `Forbidden` | `Response.Forbidden([detail])` | optional problem detail. | Returns HTTP 403. | [web-rest-api.xps](../samples/web-rest-api.xps) |
| `Conflict` | `Response.Conflict(detail)` | problem detail. | Returns HTTP 409. | [web-rest-api.xps](../samples/web-rest-api.xps) |
| `Problem` | `Response.Problem(status, title, detail)` | HTTP error status, title, detail. | Returns RFC Problem Details. | [web-rest-api.xps](../samples/web-rest-api.xps) |

## Session

Host sessions must be enabled. See [REST API development](rest-api.md).

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Session.Start` | `Session.Start()` | none | Starts/ensures current session. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.Add` | `Session.Add(name, value)` | state key/value. | Adds or overwrites session value atomically. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.Set` | `Session.Set(name, value)` | state key/value. | Writes session value. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.Get` | `Session.Get(name)` | state key. | Returns value or Null. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.Exists` | `Session.Exists(name)` | state key. | Tests key existence. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.Remove` | `Session.Remove(name)` | state key. | Removes key and returns whether it existed. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.Unset` | `Session.Unset(name)` | state key. | Removes key using compatibility alias. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.Clear` | `Session.Clear()` | none | Clears state values. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.Authenticate` | `Session.Authenticate(userId, userName, rules)` | identity fields and authorization rules. | Marks session authenticated. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.SignOut` | `Session.SignOut()` | none | Clears authenticated identity. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.RotateId` | `Session.RotateId()` | none | Rotates session identifier. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.RegenerateId` | `Session.RegenerateId()` | none | Regenerates session ID. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.Abandon` | `Session.Abandon()` | none | Abandons current session. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.Destroy` | `Session.Destroy()` | none | Destroys session. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.SetRole` | `Session.SetRole(role)` | role name. | Adds role. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.GetRoles` | `Session.GetRoles()` | none | Returns role collection. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.HasRole` | `Session.HasRole(role)` | role name. | Tests role membership. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.RemoveRole` | `Session.RemoveRole(role)` | role name. | Removes role. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.HasRule` | `Session.HasRule(rule)` | rule name. | Tests authorization rule. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.Id` | `Session.Id` | none | Current session identifier. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.Count` | `Session.Count` | none | Number of state entries. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.Keys` | `Session.Keys` | none | Current state keys. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.IsAuthenticated` | `Session.IsAuthenticated` | none | Authentication state. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.UserId` | `Session.UserId` | none | Current authenticated user ID. | [web-session-api.xps](../samples/web-session-api.xps) |
| `Session.UserName` | `Session.UserName` | none | Current authenticated user name. | [web-session-api.xps](../samples/web-session-api.xps) |

## Web Application state

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Application.Add` | `Application.Add(name, value)` | shared key/value. | Adds or overwrites process/application state. | [web-application-api.xps](../samples/web-application-api.xps) |
| `Application.Set` | `Application.Set(name, value)` | shared key/value. | Writes application state. | [web-application-api.xps](../samples/web-application-api.xps) |
| `Application.Get` | `Application.Get(name)` | shared key. | Returns value or Null. | [web-application-api.xps](../samples/web-application-api.xps) |
| `Application.Remove` | `Application.Remove(name)` | shared key. | Removes and reports whether key existed. | [web-application-api.xps](../samples/web-application-api.xps) |
| `Application.Clear` | `Application.Clear()` | none | Clears application state. | [web-application-api.xps](../samples/web-application-api.xps) |

## RequestScope

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `RequestScope.Add` | `RequestScope.Add(name, value)` | request-local key/value. | Adds or overwrites request state. | [web-request-scope-api.xps](../samples/web-request-scope-api.xps) |
| `RequestScope.Set` | `RequestScope.Set(name, value)` | key/value. | Writes request state. | [web-request-scope-api.xps](../samples/web-request-scope-api.xps) |
| `RequestScope.Get` | `RequestScope.Get(name)` | key. | Returns value or Null. | [web-request-scope-api.xps](../samples/web-request-scope-api.xps) |
| `RequestScope.Exists` | `RequestScope.Exists(name)` | key. | Tests key existence. | [web-request-scope-api.xps](../samples/web-request-scope-api.xps) |
| `RequestScope.Remove` | `RequestScope.Remove(name)` | key. | Removes and reports whether key existed. | [web-request-scope-api.xps](../samples/web-request-scope-api.xps) |
| `RequestScope.Unset` | `RequestScope.Unset(name)` | key. | Removes key using compatibility alias. | [web-request-scope-api.xps](../samples/web-request-scope-api.xps) |
| `RequestScope.Clear` | `RequestScope.Clear()` | none | Clears request-local state. | [web-request-scope-api.xps](../samples/web-request-scope-api.xps) |
| `RequestScope.Count` | `RequestScope.Count` | none | Number of request-state entries. | [web-request-scope-api.xps](../samples/web-request-scope-api.xps) |
| `RequestScope.Keys` | `RequestScope.Keys` | none | Request-state keys. | [web-request-scope-api.xps](../samples/web-request-scope-api.xps) |
