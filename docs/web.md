# Web programming

XPScript web applications use the same `.xps` language and a shared dispatcher across Kestrel, FastCGI and CGI.

For the complete verified REST API, Response, Session, Application and RequestScope reference, see [REST API development](rest-api.md).

## Route files

A site is a directory of `.xps` files:

```text
site/
  index.xps
  users.xps
  admin/
    index.xps
```

`/`, `/index` and `/index.xps` resolve to the default `index.xps`. `/users`, `/users.xps` and procedure routes such as `/users/Save` are resolved by the common router.

## Route rules

Rules are written before route procedures. Their order does not define their meaning.

```xpscript
[Anonymous]
[Get]
[PreCompile:kalle.xps]
Sub Index()
    Response.Write("Hello")
End Sub
```

Supported HTTP method rules include the standard methods handled by the web runtime. `Request.Method` always contains the normalized actual HTTP method. A misspelled or unknown bracket rule is reported on the console but does not stop compilation. Conflicting `[Anonymous]` and `[Authenticated]` rules are reported, and `[Authenticated]` takes precedence.

Role rules use OR semantics. A user needs any positive role declared for the route. A role prefixed with `!` is forbidden and overrides matching positive roles.

```xpscript
[Authenticated]
[Get]
[Role:admin;user]
[Role:editor]
[Role:!blocked]
Sub Index()
    Response.Write("Authorized")
End Sub
```

The example allows `admin`, `user` or `editor`, but rejects a principal or session that also has `blocked`.

## REST API routes

`[Route:/path]` creates an explicit REST route that is independent of the `.xps` filename. Route parameters use `{name}` and bind to procedure parameters by name.

```xpscript
[Anonymous]
[Get]
[Route:/api/users/{id}]
Function GetUser(ByVal id As Integer) As String
    GetUser = "user-" + id
End Function
```

`GET /api/users/42` binds `id` to the integer `42`. Invalid scalar conversion returns HTTP 400 instead of invoking the procedure. Returning a value from a Function automatically serializes that value as JSON when the route has not already written a response.

Explicit parameter sources can be combined with `[Route]`:

```xpscript
[Anonymous]
[Get]
[Route:/api/orders/{id}]
Sub GetOrder([FromRoute] id As Integer, [FromQuery] details As Boolean, [FromHeader:"X-Tenant-ID"] tenantId As String)
    Response.OK(id)
End Sub
```

Supported parameter bindings are `[FromRoute]`, `[FromQuery]`, `[FromHeader]`, `[FromHeader:"Header-Name"]` and `[FromBody]`. Route, query and header values are converted to the declared parameter type. Without an explicit binding, the runtime first checks a matching route parameter, then query string, then JSON body for a complex type.

Explicit REST routes are indexed across the web root. Duplicate route/method combinations are rejected. Parameter names in templates do not make otherwise identical templates unique, so `/api/users/{id}` and `/api/users/{name}` conflict when they use the same HTTP method.

See [REST API development](rest-api.md) for the complete route, validation, CORS, rate-limit and response-helper reference.

## JSON request body and models

`Body` is a reserved XPScript runtime identifier for web request content. It cannot be used as a variable, parameter, procedure or class name. It gives direct access to request content:

```xpscript
Response.OK(Body.Text())
```

For typed JSON input, use a class and bind a differently named parameter with `[FromBody]`. A complex model parameter is also treated as JSON body input when no explicit source is given.

```xpscript
Public Class CreateUserRequest
    [Required]
    [MaxLength:100]
    Public Name As String

    [Required]
    [Email]
    Public Email As String

    [Range:18;120]
    Public Age As Integer
End Class

[Anonymous]
[Post]
[Route:/api/users]
Sub CreateUser([FromBody] payload As CreateUserRequest)
    Response.OK(payload)
End Sub
```

JSON binding accepts `application/json` and structured `+json` media types. Property matching is case-insensitive. The default JSON body limit is 4 MiB.

Model validation rules are `[Required]`, `[MaxLength:n]`, `[Email]` and `[Range:min;max]`. These rules apply to REST request model fields. Validation failures return HTTP 400 using `application/problem+json` and include an `errors` object keyed by field name.

## REST response helpers

REST helpers are methods on `Response`. They serialize data with the shared JSON configuration.

```xpscript
Response.OK(data)
Response.Json(data)
Response.Created("/api/users/42", data)
Response.NoContent()
Response.BadRequest("Invalid input")
Response.NotFound("User not found")
Response.Unauthorized()
Response.Forbidden()
Response.Conflict("Already exists")
Response.problem(400, "Invalid request", "Email is required")
```

`Response.OK(data)` returns HTTP 200 and JSON. `Response.Created(location, data)` returns HTTP 201 and a `Location` header. `Response.NoContent()` returns HTTP 204. Problem helpers use the RFC Problem Details shape with `type`, `title`, `status` and `detail`.

The complete Response member list is maintained in [REST API development](rest-api.md#response-object).

## CORS route rule

CORS is opt-in per route. `[Cors]` and `[Cors:*]` allow any origin. Use one or more explicit origins separated by semicolons when the route should be restricted.

```xpscript
[Anonymous]
[Get]
[Route:/api/public]
[Cors:https://app.example.com;https://admin.example.com]
Sub PublicApi()
    Response.OK("ok")
End Sub
```

The runtime handles CORS preflight `OPTIONS` requests for CORS-enabled REST routes and emits `Access-Control-Allow-Origin`, allowed methods and requested headers. Explicit origins use `Vary: Origin`.

## Rate limiting route rule

`[RateLimit:requests;window]` applies a fixed-window limit to one procedure. Windows support seconds, minutes, hours and days.

```xpscript
[Anonymous]
[Get]
[Route:/api/search]
[RateLimit:100;1m]
Sub Search()
    Response.OK("ok")
End Sub
```

Examples are `10;30s`, `100;1m`, `1000;1h` and `5000;1d`. An authenticated request is keyed by its user identity when available. Anonymous requests use the remote address. Exceeding the limit returns HTTP 429 as Problem Details and includes `Retry-After`.

## PreCompile

`[PreCompile:file.xps]` asks the web engine to warm another route. If the extension is omitted, the engine also tries `.xps`. A missing target is reported and skipped rather than stopping the server.

Startup warms the default document and its direct precompile targets. Nested targets are not recursively warmed at startup. If `index.xps` precompiles `kalle.xps`, and `kalle.xps` declares `[PreCompile:nisse.xps]`, `nisse.xps` is warmed after the first real request to `kalle.xps`.

Already compiled files are reused while their source/dependency snapshot remains unchanged. Ordinary server-side `.xps` pages also persist their compiled DLL/PDB artifact on disk. The persistent cache survives process restarts and is reused only when the source/dependency snapshot, runtime/compiler configuration, compiler build identity and web runtime build identity still match. Changed source or dependencies automatically invalidate the artifact. Browser-WASM pages keep their separate bundle cache. The response is allowed to complete before sublevel warmup runs in the background.

`XpsWebCompilationCacheOptions.EnablePersistentCache` controls persistent artifact reuse for custom hosts. `PersistentCacheDirectory` can override the default per-site cache directory under the process user's local application data directory.

## Request

The Request API uses a singular method for the common first-value case and an `All` method when HTTP permits multiple values.

```xpscript
Dim id As String
Dim tenant As String
Dim token As String

id = Request.Query("id")
tenant = Request.Header("X-Tenant-ID")
token = Request.BearerToken
```

Important properties:

| Member | Description |
|---|---|
| `Request.Method` | Actual normalized HTTP method. |
| `Request.Path` | Request path. |
| `Request.PathInfo` | Path-info portion. |
| `Request.QueryString` | Raw query string. |
| `Request.Query_String` | Raw CGI-compatible query string alias. |
| `Request.Query_String_Decoded` | URL-decoded query string. |
| `Request.ContentType` | Request Content-Type. |
| `Request.ContentLength` | Request body length when known. |
| `Request.Host` | Host value. |
| `Request.Scheme` | Request scheme. |
| `Request.RemoteAddress` | Client/peer address supplied by the host. |
| `Request.Protocol` | HTTP protocol. |
| `Request.Authorization` | First Authorization header value, or an empty string. |
| `Request.BearerToken` | Bearer token when exactly one valid Bearer Authorization value exists, otherwise Null. |
| `Request.Headers` | Read-only multi-value header collection. |
| `Request.Cookies` | Read-only cookie collection. |
| `Request.CgiVariables` | Read-only CGI-compatible variable collection. |

Request value methods:

| Method | Result |
|---|---|
| `Request.Query(name)` | First decoded query value, or an empty string. |
| `Request.QueryAll(name)` | All decoded query values. |
| `Request.Form(name)` | First form value, or an empty string. |
| `Request.FormAll(name)` | All form values. |
| `Request.Header(name)` | First header value, or an empty string. |
| `Request.HeaderAll(name)` | All header values. |
| `Request.Cookie(name)` | Cookie value, or Null when missing. |
| `Request.Cgi(name)` | CGI-compatible variable value, or Null when missing. |

`QueryFirst`, `FormFirst` and `HeaderFirst` remain compatibility aliases for `Query`, `Form` and `Header`. New code should use the shorter singular names.

Query and form names are case-insensitive. Repeated query and form fields preserve their values through the `All` methods. Header lookup is case-insensitive and preserves multiple header values.

Body and upload methods include `BodyText()`, `BodyBytes()`, `Files()`, `Files(name)` and `FileFirst(name)`. These methods enforce their configured size and count limits.

`Request.Authorization` is a convenience alias for `Request.Header("Authorization")`. `Request.BearerToken` only accepts one Authorization header whose scheme is Bearer and whose token is non-empty. Multiple Authorization values are treated as ambiguous and produce `Null`.

## Response

`Response` represents the outgoing HTTP response. Set `Response.ContentType` before writing when a specific media type is required. Use `Response.Write(value)` to append response content. For REST APIs prefer typed response helpers instead of constructing JSON strings manually.

For headers and cookies use the explicit response methods:

```xpscript
Response.SetHeader("Cache-Control", "no-store")
Response.AppendHeader("Vary", "Origin")
Response.SetCookie("session", token)
Response.DeleteCookie("session")
```

`SetHeader` replaces the values for a header. `AppendHeader` adds another value. `RemoveHeader` removes a header. Cookie values and options are validated by the runtime before a `Set-Cookie` header is emitted.

See [Response object](rest-api.md#response-object) for the verified complete member list.

## Session

Sessions are host-controlled. Kestrel sessions must be enabled with `--sessions`. CGI is process-per-request and requires persistent CGI state configuration when state must survive requests. Do not assume in-memory state survives across CGI processes.

Session state supports `Add`, `Set`, `Get`, `Exists`, `Remove`, `Unset` and `Clear`. `Add` overwrites an existing value. `Get` returns `Null` when the key does not exist. `Remove` returns false without throwing when a key does not exist.

Authenticated sessions can hold roles used directly by `[Role:...]` authorization:

```xpscript
Session.Authenticate("42", "Fredrik")
Session.SetRole("admin")
Session.SetRole("editor")
```

Session state is thread-safe inside one runtime process. Concurrent requests using the same session serialize session state mutations through the session record lock.

See [Session object](rest-api.md#session-object) for all implemented Session members.

## Application and Request scopes

Application scope is shared by requests in one application runtime instance. RequestScope exists only for one request.

```xpscript
Application.Add("site", "example")
Session.Add("cart", "123")
RequestScope.Add("temporary", "value")
```

For all three scopes, `Add(name, value)` overwrites an existing key, `Get(name)` returns `Null` for a missing key, and `Remove(name)` is safe for missing keys and returns false.

Application scope is thread-safe inside one runtime process. RequestScope is also internally synchronized. In-memory Application state is not a distributed store across multiple processes or servers.

See [Application scope](rest-api.md#application-scope) and [Request scope](rest-api.md#request-scope) for the complete API and lifetime rules.

## HTTP methods

A route can declare the methods it accepts. Do not assume a UIForm route is GET-only. A form commonly accepts both GET and POST:

```xpscript
[Anonymous]
[Get]
[Post]
Sub Index()
    Response.Write(Request.Method)
End Sub
```

The runtime preserves other valid HTTP methods in `Request.Method` so application logic can inspect the actual request method.

## CGI variables

CGI-compatible request variables are available in Kestrel, FastCGI and CGI hosting. Use `Request.Cgi("VARIABLE_NAME")` when you need a specific variable. Prefer normalized Request properties such as `Method`, `ContentType`, `RemoteAddress` and `Query_String` when an equivalent property exists.

The normalized base set includes `REQUEST_METHOD`, `REQUEST_URI`, `QUERY_STRING`, `PATH_INFO`, `SCRIPT_NAME`, `SERVER_NAME`, `SERVER_PORT`, `SERVER_PROTOCOL`, `REMOTE_ADDR`, `CONTENT_TYPE`, `CONTENT_LENGTH` and `HTTPS`. Incoming headers are also exposed using CGI-style `HTTP_*` names. CGI and FastCGI retain transport-provided environment variables in addition to the normalized values.

## IIS hosting

On Windows Server, the recommended IIS topology is IIS as the public TLS endpoint and reverse proxy to an XPScript Kestrel process on loopback. Direct IIS CGI hosting is also supported for simpler deployments.

See [Hosting XPScript on IIS](iis-hosting.md) for the full configuration, including ARR, URL Rewrite, `web.config`, CGI mappings, application pool settings, filesystem permissions and troubleshooting.

## Security boundaries

The configured web root is a trust boundary. XPScript route resolution constrains resolved source files to that root. Never expose `.xps` source as ordinary static files. Validate query/form/header/cookie values before use. Treat uploaded filenames as untrusted. Bind FastCGI to loopback or a Unix socket unless the network is explicitly trusted.

The persistent compiler cache contains executable .NET assemblies generated from trusted `.xps` source. Keep its directory writable only by the XPScript service identity or an equally trusted administrator.

REST endpoints should use explicit CORS origins when browser callers are known. Do not use `[Cors:*]` for credentialed or sensitive APIs. Rate limits complement authentication and authorization but do not replace them.

See [Getting started](getting-started.md) for host setup and parameters and [UIForm](uiform.md) for forms.
