# REST API development

This document describes the REST functionality currently implemented in XPScript web runtime.

## Explicit routes

Use `[Route:/path]` together with one or more HTTP method rules.

```xpscript
[Anonymous]
[Get]
[Route:/api/users/{id}]
Sub GetUser([FromRoute] id As Integer)
    Response.OK(id)
End Sub
```

Route parameters use `{name}`. Invalid scalar conversion returns HTTP 400 without invoking the route. Duplicate route and method combinations are rejected. Parameter names do not make otherwise identical templates unique.

## Parameter binding

Supported explicit bindings are:

- `[FromRoute]`
- `[FromQuery]`
- `[FromHeader]`
- `[FromHeader:"Header-Name"]`
- `[FromBody]`

Without an explicit binding the runtime checks a matching route parameter, then query string, then JSON body for a complex type. Query and header binding preserve HTTP multi-value semantics internally.

## Request object

For direct request access, singular methods return the first value and `All` methods return every value.

```xpscript
Dim id As String
Dim token As String

id = Request.Query("id")
token = Request.BearerToken
```

Request properties currently implemented:

- `Request.Method`
- `Request.Path`
- `Request.PathInfo`
- `Request.QueryString`
- `Request.Query_String`
- `Request.Query_String_Decoded`
- `Request.ContentType`
- `Request.ContentLength`
- `Request.Body`
- `Request.Host`
- `Request.Scheme`
- `Request.RemoteAddress`
- `Request.Protocol`
- `Request.Headers`
- `Request.Cookies`
- `Request.CgiVariables`
- `Request.Authorization`
- `Request.BearerToken`
- `Request.CancellationToken`
- `Request.IsCancellationRequested`

Request methods currently implemented:

- `Request.Query(name)`
- `Request.QueryAll(name)`
- `Request.Form(name)`
- `Request.FormAll(name)`
- `Request.Header(name)`
- `Request.HeaderAll(name)`
- `Request.Cookie(name)`
- `Request.Cgi(name)`
- `Request.BodyText()`
- `Request.BodyBytes()`
- `Request.Files()`
- `Request.Files(name)`
- `Request.FileFirst(name)`

`Query`, `Form` and `Header` return the first value or an empty string when the value is missing. `QueryAll`, `FormAll` and `HeaderAll` return all values. `Cookie` and `Cgi` return `Null` when the named value is missing.

`QueryFirst`, `FormFirst` and `HeaderFirst` remain compatibility aliases. New code should use `Query`, `Form` and `Header`.

`Request.Authorization` returns the first Authorization header value. `Request.BearerToken` returns the token only when exactly one Authorization value exists, uses the Bearer scheme and contains a non-empty token. Ambiguous or malformed Authorization input returns `Null`.

CGI-compatible variables are normalized across Kestrel, FastCGI and CGI. The base set includes `REQUEST_METHOD`, `REQUEST_URI`, `QUERY_STRING`, `PATH_INFO`, `SCRIPT_NAME`, `SERVER_NAME`, `SERVER_PORT`, `SERVER_PROTOCOL`, `REMOTE_ADDR`, `CONTENT_TYPE`, `CONTENT_LENGTH` and `HTTPS`. Request headers are also exposed as CGI-style `HTTP_*` variables. CGI and FastCGI preserve additional transport-provided environment variables.

## JSON body

`Body` is a reserved web runtime identifier. It cannot be used as a variable, parameter, procedure or class name.

```xpscript
Dim raw As String
raw = Body.Text()
```

Typed JSON request models can be bound with `[FromBody]`.

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
    Response.Created("/api/users/42", payload)
End Sub
```

JSON property matching is case-insensitive. `application/json` and structured `+json` media types are accepted. The default JSON body limit is 4 MiB.

Validation metadata currently supported on class fields is `[Required]`, `[Email]`, `[MaxLength:n]` and `[Range:min;max]`. These are REST model validation rules, not general-purpose XPScript attributes. Validation failures return HTTP 400 using `application/problem+json` with an `errors` object.

## Response object

Base response members currently implemented:

- `Response.StatusCode`
- `Response.ContentType`
- `Response.Completed`
- `Response.MaxBodyBytes`
- `Response.Headers`
- `Response.Body`
- `Response.SetHeader(name, value)`
- `Response.AppendHeader(name, value)`
- `Response.RemoveHeader(name)`
- `Response.SetCookie(name, value, options)`
- `Response.DeleteCookie(name, path, secure, sameSite, domain)`
- `Response.Write(value)`
- `Response.WriteBinary(value)`
- `Response.SendFile(...)`
- `Response.Clear()`
- `Response.Redirect(url, statusCode)`
- `Response.Complete()`

`SetHeader` replaces existing values for the header. `AppendHeader` preserves existing values and appends another value. `RemoveHeader` removes the header. Response header names and values are validated. Cookie names, values and options are validated before `Set-Cookie` is emitted.

REST response helpers currently implemented:

- `Response.Json(data)`
- `Response.OK(data)`
- `Response.Ok(data)`
- `Response.Created(location, data)`
- `Response.NoContent()`
- `Response.BadRequest(detail)`
- `Response.NotFound(detail)`
- `Response.Unauthorized(detail)`
- `Response.Forbidden(detail)`
- `Response.Conflict(detail)`
- `Response.Problem(status, title, detail)`
- `Response.problem(status, title, detail)`

`Response.OK` and `Response.Json` return HTTP 200 with JSON. `Created` returns HTTP 201 and sets `Location`. `NoContent` returns HTTP 204. Problem helpers return `application/problem+json` and require a status from 400 through 599.

## CORS

CORS is configured per route.

```xpscript
[Cors:*]
```

or:

```xpscript
[Cors:https://app.example.com;https://admin.example.com]
```

`[Cors]` is equivalent to `[Cors:*]`. CORS-enabled REST routes handle preflight `OPTIONS` requests. Explicit origins use `Vary: Origin`.

## Rate limiting

Rate limiting is configured per route using a fixed window.

```xpscript
[RateLimit:100;1m]
```

Supported window suffixes are seconds, minutes, hours and days, for example `30s`, `1m`, `1h` and `1d`. Authenticated requests are keyed by user identity when available. Anonymous requests use the remote address. Exceeding the limit returns HTTP 429 with Problem Details and `Retry-After`.

## Session object

Session support must be enabled by the host. The currently implemented Session surface is:

- `Session.Id`
- `Session.Started`
- `Session.Count`
- `Session.Keys`
- `Session.IsAuthenticated`
- `Session.UserId`
- `Session.UserName`
- `Session.Rules`
- `Session.Roles`
- `Session.Start()`
- `Session.Add(name, value)`
- `Session.Set(name, value)`
- `Session.Get(name)`
- `Session.Exists(name)`
- `Session.Remove(name)`
- `Session.Unset(name)`
- `Session.Clear()`
- `Session.HasRule(rule)`
- `Session.Authenticate(userId, userName, rules)`
- `Session.SignOut()`
- `Session.RotateId()`
- `Session.RegenerateId()`
- `Session.Abandon()`
- `Session.Destroy()`
- `Session.SetRole(role)`
- `Session.GetRoles()`
- `Session.HasRole(role)`
- `Session.RemoveRole(role)`

`Session.Add(name, value)` overwrites an existing value atomically. `Session.Get(name)` returns `Null` when the key does not exist. `Session.Remove(name)` does not throw when the key is missing and returns false in that case. `Set` remains supported as a state write operation.

Session state is thread-safe inside a process. Concurrent requests for the same session serialize state access through a per-session lock. Session-store mutations such as ID rotation are protected by the store lock together with the session lock.

## Application scope

Application scope is shared by requests handled by the same application runtime instance.

```xpscript
Application.Add("site-name", "Example")
Dim value
value = Application.Get("site-name")
Application.Remove("site-name")
```

The XPScript Application surface currently exposes:

- `Application.Add(name, value)`
- `Application.Set(name, value)`
- `Application.Get(name)`
- `Application.Remove(name)`
- `Application.Clear()`

`Add` overwrites an existing value. `Get` returns `Null` for a missing key. `Remove` returns false without throwing when the key does not exist.

Application state is thread-safe inside a process. Reads, writes, removes and clear operations are protected by the application-state lock. Concurrent writes do not corrupt state. When multiple concurrent requests write the same key, the last completed write wins.

The CGI persistent Application implementation uses its own synchronization around the persistent record and follows the same `Add`, `Get` and `Remove` semantics.

Application state is process/runtime scoped unless the selected host provides persistent/shared state. Do not assume ordinary in-memory Application state is distributed across several server processes or nodes.

## Request scope

Request scope exists only for the current request.

```xpscript
RequestScope.Add("temporary", "value")
Dim value
value = RequestScope.Get("temporary")
RequestScope.Remove("temporary")
```

Available members:

- `RequestScope.Count`
- `RequestScope.Keys`
- `RequestScope.Add(name, value)`
- `RequestScope.Set(name, value)`
- `RequestScope.Get(name)`
- `RequestScope.Exists(name)`
- `RequestScope.Remove(name)`
- `RequestScope.Unset(name)`
- `RequestScope.Clear()`

`Add` overwrites an existing value. `Get` returns `Null` for a missing key. `Remove` returns false without throwing when the key does not exist. A new request receives a new RequestScope.

RequestScope is internally synchronized as well, although it is normally accessed only by work belonging to one request.

## Scope selection

Use RequestScope for temporary values needed only during one request. Use Session for per-user state that must survive several requests. Use Application for values shared by all requests handled by one application runtime instance.

For multi-process CGI, multiple Kestrel instances, load-balanced servers or several HA nodes, in-memory Application and Session state is not automatically shared between processes. Configure persistent/shared state where required by the hosting model.

## Authentication and roles

REST routes use the normal web authorization rules.

```xpscript
[Authenticated]
[Role:admin;api-user]
[Role:!blocked]
[Get]
[Route:/api/private]
Sub PrivateApi()
    Response.OK("ok")
End Sub
```

Positive roles use OR semantics. A forbidden role prefixed by `!` takes precedence over positive roles.
