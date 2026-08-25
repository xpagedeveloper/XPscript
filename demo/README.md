# XPScript demos

This directory contains small demonstration programs grouped by runtime/application type. The examples are intentionally short so they can be copied, compiled and modified without first understanding the regression suite in `samples/`.

## Prerequisites

Build the solution once from the repository root:

```powershell
dotnet restore .\XPScriptCompiler.slnx
dotnet build .\XPScriptCompiler.slnx -c Release
```

The commands below assume `xpscriptc` and `xpscript` are available on `PATH`. During development you can replace them with `dotnet run --project ... --`.

## Console executable

Source: [`console/hello.xps`](console/hello.xps)

```powershell
xpscriptc .\demo\console\hello.xps -o .\out\demo-console.exe --framework-dependent
.\out\demo-console.exe
```

Demonstrates variables, a `For` loop, `Print`, `CStr` and `UCase`.

## Desktop UIForm

Source: [`desktop-ui/customer-form.xps`](desktop-ui/customer-form.xps)

```powershell
xpscriptc .\demo\desktop-ui\customer-form.xps -o .\out\demo-desktop.exe --framework-dependent
.\out\demo-desktop.exe
```

Demonstrates a native UIForm with text fields and required validation.

## Themed desktop UIForm

Source: [`desktop-ui/themed-form.xps`](desktop-ui/themed-form.xps)

```powershell
xpscriptc .\demo\desktop-ui\themed-form.xps -o .\out\demo-desktop-themed.exe --framework-dependent
.\out\demo-desktop-themed.exe
```

Demonstrates Fluent light/dark/system themes, grid layout, placeholders, tooltips, required and regex validation, inline validation errors, and `UIForm.ShowValidationErrors` behavior.

## Browser WebAssembly UIForm

Source: [`browser-wasm/customer-form.xps`](browser-wasm/customer-form.xps)

Run the directory through the web host:

```powershell
xpscript web --root .\demo\browser-wasm --address 127.0.0.1 --port 8080
```

Open `http://127.0.0.1:8080/customer-form.xps`. The `[Platform:browser-wasm]` marker causes on-demand browser-WASM compilation.

## Generic web page

Source: [`web/index.xps`](web/index.xps)

```powershell
xpscript web --root .\demo\web --address 127.0.0.1 --port 8080
```

Open `http://127.0.0.1:8080/`.

## REST API

Source: [`rest-api/users.xps`](rest-api/users.xps)

```powershell
xpscript web --root .\demo\rest-api --address 127.0.0.1 --port 8080
```

Then try:

```powershell
Invoke-RestMethod http://127.0.0.1:8080/api/users/42
Invoke-RestMethod http://127.0.0.1:8080/api/users -Method Post -ContentType 'application/json' -Body '{"name":"Ada","email":"ada@example.com"}'
```

Demonstrates `[Route]`, route parameters, `[FromBody]`, validation and `Response.Created`.

## Web request, session and application state

Source: [`web-state/index.xps`](web-state/index.xps)

```powershell
xpscript web --root .\demo\web-state --address 127.0.0.1 --port 8080 --sessions
```

Open `http://127.0.0.1:8080/`. The demo shows request-local `RequestScope`, process/application state and host-enabled `Session` state in the same route.

## Kestrel hosting

Source: [`kestrel/index.xps`](kestrel/index.xps)

```powershell
xpscript web --root .\demo\kestrel --address 127.0.0.1 --port 8080
```

This is the preferred persistent local web-host demo.

## FastCGI hosting

Source: [`fastcgi/index.xps`](fastcgi/index.xps)

```powershell
xpscript fastcgi --root .\demo\fastcgi --listen 127.0.0.1:9000
```

Connect a FastCGI-capable reverse proxy to the private listener.

## CGI hosting

Source: [`cgi/index.xps`](cgi/index.xps)

Publish the CGI host first:

```powershell
dotnet publish .\src\XPScript.Web.Cgi\XPScript.Web.Cgi.csproj -c Release -r win-x64 --self-contained false -o .\out\cgi
```

Configure the web server with `XPSCRIPT_WEB_ROOT` pointing at `demo/cgi`. CGI is process-per-request, so Kestrel or FastCGI is normally preferable for interactive demonstrations.

## WebIIS package

Source: [`webiis/main.xps`](webiis/main.xps)

```powershell
xpscript compile .\demo\webiis\main.xps --target webiis
```

This demonstrates the direct IIS deployment target. See `docs/webiis.md` for packaging and deployment details.

## SQLite

Source: [`sqlite/sqlite-demo.xps`](sqlite/sqlite-demo.xps)

```powershell
xpscriptc .\demo\sqlite\sqlite-demo.xps -o .\out\demo-sqlite.exe --framework-dependent
.\out\demo-sqlite.exe
```

The demo creates `demo.db`, creates a table, inserts one parameterized row and prints a JSON query result.

## SQL Server / SQL Server Express

Source: [`mssql/mssql-demo.xps`](mssql/mssql-demo.xps)

Set a connection string first:

```powershell
$env:XPSCRIPT_MSSQL_CONNECTION = 'Server=.\SQLEXPRESS;Database=master;Integrated Security=true;TrustServerCertificate=true'
xpscriptc .\demo\mssql\mssql-demo.xps -o .\out\demo-mssql.exe --framework-dependent
.\out\demo-mssql.exe
```

The demo uses a temporary SQL Server table and parameterized SQL.

## HTTP database clients

Source: [`httpdb/httpdb-demo.xps`](httpdb/httpdb-demo.xps)

```powershell
xpscriptc .\demo\httpdb\httpdb-demo.xps -o .\out\demo-httpdb.exe --framework-dependent
.\out\demo-httpdb.exe
```

This offline-safe configuration demo creates Supabase and Domino REST clients without sending a network request. It demonstrates `HTTPDBSupabase.SetSchema`, `HTTPDBSupabase.Eq` and `HTTPDBDominoRest.SetDataSource`. See `docs/httpdb.md` for real CRUD/login examples and credential guidance.

## HTTP client

Source: [`http/http-client.xps`](http/http-client.xps)

Start the Kestrel demo first, then in another terminal:

```powershell
$env:XPSCRIPT_DEMO_HTTP_URL = 'http://127.0.0.1:8080/'
xpscriptc .\demo\http\http-client.xps -o .\out\demo-http.exe --framework-dependent
.\out\demo-http.exe
```

Demonstrates `HttpClient`, headers, timeout and `HttpResponse`.

## XPAi and AITool callbacks

Source: [`ai/ai-tool-demo.xps`](ai/ai-tool-demo.xps)

The repository includes a deterministic local AI fixture, so this demo does not require a real provider key:

```powershell
python .\tools\xpai_mock_server.py 18765
```

In another terminal:

```powershell
$env:XPSCRIPT_AI_COMPAT_BASE = 'http://127.0.0.1:18765'
xpscriptc .\demo\ai\ai-tool-demo.xps -o .\out\demo-ai.exe --framework-dependent
.\out\demo-ai.exe
```

Demonstrates `AITool`, JSON parameter schemas, compile-time callback validation, callback context, automatic tool execution and `XPAi.Complete()`.

## Managed and native references

The managed-reference example is [`../samples/managed-reference.xps`](../samples/managed-reference.xps). `Reference` deliberately accepts only an application-local relative DLL path, so prepare the fixture beside the source first:

```powershell
dotnet build .\tests\ManagedReferenceFixture\ManagedReferenceFixture.csproj -c Release
New-Item -ItemType Directory -Force .\samples\managed-reference | Out-Null
Copy-Item .\tests\ManagedReferenceFixture\bin\Release\net10.0\ManagedReferenceFixture.dll .\samples\managed-reference\ManagedReferenceFixture.dll -Force
xpscriptc .\samples\managed-reference.xps -o .\out\demo-managed-reference.exe --framework-dependent
.\out\demo-managed-reference.exe
```

For RID-specific native dependencies use [`../samples/managed-reference-native.xps`](../samples/managed-reference-native.xps). It uses the checked-in native-transitive fixture assets and demonstrates repeatable `ReferenceNative "path" Runtime "rid"` directives.

## Regression samples versus demos

Use `demo/` when learning or showing XPScript. Use `samples/` when validating edge cases, compatibility and compiler regressions. Documentation may link to either, but new user-facing walkthroughs should prefer `demo/` when a matching demo exists.