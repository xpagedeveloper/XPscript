# Web programming

XPScript web applications use the same `.xps` language and a shared dispatcher across Kestrel, FastCGI and CGI.

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

## PreCompile

`[PreCompile:file.xps]` asks the web engine to warm another route. If the extension is omitted, the engine also tries `.xps`. A missing target is reported and skipped rather than stopping the server.

Startup warms the default document and its direct precompile targets. Nested targets are not recursively warmed at startup. If `index.xps` precompiles `kalle.xps`, and `kalle.xps` declares `[PreCompile:nisse.xps]`, `nisse.xps` is warmed after the first real request to `kalle.xps`.

Already compiled files are reused while their source/dependency snapshot remains unchanged. Ordinary server-side `.xps` pages also persist their compiled DLL/PDB artifact on disk. The persistent cache survives server restarts and is reused only when the source/dependency snapshot, runtime, compiler configuration and compiler version still match. A changed source or dependency automatically causes recompilation and replacement of the old artifact. Browser-WASM pages keep their separate bundle cache. The response is allowed to complete before sublevel warmup runs in the background.

## Request

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
| `Request.CgiVariables` | CGI variable collection. |

Methods include `Query(name)`, `QueryFirst(name)`, `Header(name)`, `HeaderFirst(name)`, `Cookie(name)`, `BodyText()`, `BodyBytes()`, `Form(name)`, `FormFirst(name)`, `Files()`, `Files(name)`, `FileFirst(name)` and `Cgi(name)`.

All CGI variables supplied by the transport are available through the Request object.

Example:

```xpscript
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.Write("method=" + Request.Method)
    Response.Write(" query=" + Request.Query_String_Decoded)
End Sub
```

## Response

`Response` represents the outgoing HTTP response. Set `Response.ContentType` before writing when a specific media type is required. Use `Response.Write(value)` to append response content. UIForm also writes its generated web UI through the current response.

```xpscript
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "application/json; charset=utf-8"
    Response.Write("{\"ok\":true}")
End Sub
```

## Session

Sessions are enabled by default for Kestrel and FastCGI. CGI sessions are enabled only when `cgi.sessionFolder` is configured in `web.cfg`. If no session folder is configured, CGI has no Session object. The configured folder persists sessions across CGI processes.

## Application

Web application state belongs to the configured site/runtime. Keep tenant and user data scoped explicitly. Do not use global application state as a substitute for authentication or authorization.

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

CGI and FastCGI transports populate CGI-compatible request variables. Use `Request.Cgi("VARIABLE_NAME")` when you need a specific variable. Prefer normalized Request properties such as `Method`, `ContentType`, `RemoteAddress` and `Query_String` when an equivalent property exists.

## Security boundaries

The configured web root is a trust boundary. XPScript route resolution constrains resolved source files to that root. Never expose `.xps` source as ordinary static files. Validate query/form/header/cookie values before use. Treat uploaded filenames as untrusted. Bind FastCGI to loopback or a Unix socket unless the network is explicitly trusted.

See [Getting started](getting-started.md) for host setup and parameters and [UIForm](uiform.md) for forms.


### Session roles

Use `Session.SetRole(role)`, `Session.GetRole()`, `Session.HasRole(role)` and `Session.RemoveRole(role)`. Routes can require a role with `[Role:admin]`. `[Rule:name]` remains supported separately.

```json
{ "cgi": { "sessionFolder": "./sessions" } }
```
