# REST API development

This document describes the REST functionality currently implemented in XPScript web runtime.

## Generate REST server code from OpenAPI

XPScript can generate REST server `.xps` source from OpenAPI 3.0.x and 3.1.x specifications in YAML or JSON format.

```text
xpscript openapi generate openapi.yaml
```

The default output file uses the same base name as the specification with the `.xps` extension. Use `-o` or `--output` to select another path.

```text
xpscript openapi generate petstore.yaml -o ./generated/petstore.xps
xpscript openapi generate api.json --force
```

`--force` is required to overwrite an existing generated `.xps` file.

A minimal OpenAPI source can look like this:

```yaml
openapi: 3.1.0
info:
  title: Pet API
  version: 1.0.0
paths:
  /pets/{petId}:
    get:
      operationId: getPet
      parameters:
        - name: petId
          in: path
          required: true
          schema:
            type: integer
            format: int64
      responses:
        '200':
          description: Pet returned
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/Pet'
components:
  schemas:
    Pet:
      type: object
      required: [id, name]
      properties:
        id:
          type: integer
          format: int64
        name:
          type: string
          maxLength: 100
```

The generator creates compilable XPScript REST code. Component schemas become XPScript classes. OpenAPI path, query and header parameters become `[FromRoute]`, `[FromQuery]` and `[FromHeader]` bindings. JSON request bodies become `[FromBody]` parameters.

Each OpenAPI operation gets two contract classes and a handler function. For an operation with `operationId: getPet`, the generated code contains `GetPetRequest`, `GetPetResponse` and `HandleGetPet`.

```xpscript
Public Class GetPetRequest
    Public PetId As Long
End Class

Public Class GetPetResponse
    Public StatusCode As Integer
    Public Data As Variant
End Class

Function HandleGetPet(request As GetPetRequest) As GetPetResponse
    Dim result As GetPetResponse
    Set result = New GetPetResponse
    ' TODO: implement this operation and set result.StatusCode/result.Data.
    result.StatusCode = 501
    HandleGetPet = result
End Function

[Anonymous]
[Get]
[Route:/pets/{petId}]
Sub GetPet([FromRoute:"petId"] pPetId As Long)
    Dim request As GetPetRequest
    Set request = New GetPetRequest
    Dim result As GetPetResponse
    request.PetId = pPetId
    Set result = HandleGetPet(request)
    Call WriteGetPetResponse(result)
End Sub
```

The generated route wrapper owns HTTP parameter binding. Application code belongs in the generated `Handle...` function and returns a generated response contract. Set `StatusCode` to the response status and `Data` to the JSON-compatible value to return. Generated response writers use `Response.Json(status, data)` for JSON responses and `Response.NoContent()` for HTTP 204.

OpenAPI schema mappings currently generated are:

- `integer` with `format: int32` -> `Integer`
- other `integer` -> `Long`
- `number` with `format: float` -> `Single`
- other `number` -> `Double`
- `boolean` -> `Boolean`
- `string` -> `String`
- `string` with `format: date` or `date-time` -> `Date`
- component schema `$ref` -> generated XPScript class
- arrays and free-form objects -> `Variant`

The generator maps OpenAPI validation metadata onto REST model validation where XPScript has an equivalent rule. `required` generates `[Required]`, `format: email` generates `[Email]`, `maxLength` generates `[MaxLength:n]`, and schemas containing both `minimum` and `maximum` generate `[Range:min;max]`.

OpenAPI 3.1 nullable type arrays such as `type: [string, 'null']` are accepted. The non-null type is used for the generated XPScript field.

Current generator constraints are deliberate. Only local `$ref` values are resolved. Typed schema references must point to `#/components/schemas/...`. Request bodies currently require `application/json` or a structured `+json` media type. Path, query and header parameters are supported. Cookie parameters, external references, multipart/form-data generation, advanced JSON Schema composition such as `oneOf`/`anyOf`, and lossless generation for OpenAPI property names that are not valid XPScript identifiers are not yet supported. Unsupported input fails generation instead of silently producing a different API contract.

Generated files are intended to be regenerated from the OpenAPI source. Regeneration overwrites generated handler bodies when `--force` is used, so keep durable business logic in separate application functions/modules if the specification will be regenerated frequently.

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
- `Response.Json(status, data)`
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

`Response.OK` and `Response.Json(data)` return HTTP 200 with JSON. `Response.Json(status, data)` returns JSON with the specified HTTP status from 100 through 599. `Created` returns HTTP 201 and sets `Location`. `NoContent` returns HTTP 204. Problem helpers return `application/problem+json` and require a status from 400 through 599.

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
