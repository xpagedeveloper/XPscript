# Simplified REST API syntax

XPscript REST routes support compact HTTP-method route attributes while retaining the existing `[Get]` plus `[Route:/path]` form.

## Compact routes

These forms are equivalent:

```xpscript
[Anonymous]
[Get]
[Route:/api/users/{id}]
Function GetUser(id As Integer) As String
    GetUser = CStr(id)
End Function
```

```xpscript
[Anonymous]
[Get:/api/users/{id}]
Function GetUser(id As Integer) As String
    GetUser = CStr(id)
End Function
```

The compact form is supported for the normal HTTP method shorthands, including `Get`, `Post`, `Put`, `Delete` and `Patch`. Multiple methods may use the same route on one procedure:

```xpscript
[Get:/api/ping]
[Post:/api/ping]
Function Ping() As String
    Ping = "pong"
End Function
```

## Implicit parameter binding

Explicit `[FromRoute]`, `[FromQuery]` and `[FromBody]` annotations remain available but are not required for ordinary cases.

Without an explicit source, the runtime checks a matching route parameter first, then query string, then JSON body for a complex model.

```xpscript
[Get:/api/users/{id}]
Function GetUser(id As Integer, details As Boolean) As String
    GetUser = CStr(id) & ":" & CStr(details)
End Function
```

`GET /api/users/42?details=true` binds `id` from the route and `details` from the query string.

A complex parameter is bound from a JSON request body when no explicit binding overrides it:

```xpscript
[Post:/api/users]
Sub CreateUser(payload As CreateUserRequest)
    Response.OK(payload)
End Sub
```

Use explicit binding when the source would otherwise be ambiguous or when a header/source name must be specified.

## RoutePrefix

A file can declare one route prefix before its first web route procedure:

```xpscript
[RoutePrefix:/api/users]
```

With that prefix:

```xpscript
[Get:/]
Function ListUsers() As String
End Function

[Get:/{id}]
Function GetUser(id As Integer) As String
End Function
```

produce:

```text
GET /api/users
GET /api/users/{id}
```

The prefix applies to explicit REST routes declared with either compact method-path syntax or `[Route:/...]`.

If `RoutePrefix` is not declared, a function route remains the complete path exactly as before:

```xpscript
[Get:/api/users/{id}]
Function GetUser(id As Integer) As String
End Function
```

still maps to `/api/users/{id}`.

## File-level authentication and roles

`RoutePrefix` also opens a file metadata block. `Anonymous`, `Authenticated` and `Role` rules declared in that block become defaults for subsequent routes in the file.

```xpscript
[RoutePrefix:/api/users]
[Authenticated]
[Role:api-user]
```

A function inherits each rule category unless that same category is declared on the function.

For example, this function inherits `Authenticated` but replaces the file roles with `editor`:

```xpscript
[Post:/]
[Role:editor]
Sub CreateUser(payload As CreateUserRequest)
End Sub
```

This function replaces the file authentication rule with `Anonymous` while leaving the inherited role rule unchanged:

```xpscript
[Get:/public]
[Anonymous]
Function PublicUsers() As String
End Function
```

### Clearing inherited roles

An empty function role declaration clears every inherited required/forbidden role:

```xpscript
[Delete:/{id}]
[Role:]
Sub DeleteUser(id As Integer)
    Response.NoContent()
End Sub
```

`Role:` only clears roles. It does not clear inherited authentication.

Existing function-level route metadata remains supported and existing files without `RoutePrefix` keep their previous scope behavior.

## Starting Kestrel

The web root can now be supplied positionally:

```text
xpscript web ./site
```

This is equivalent to:

```text
xpscript web --root ./site
```

The existing defaults remain `127.0.0.1` and port `8080`, so local development can use:

```text
xpscript web ./site
```

Options can still follow the positional root:

```text
xpscript web ./site --port 9000
```

For production, listener settings should still be explicit when the server must bind beyond loopback.
