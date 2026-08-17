# XPScript web runtime

(c) xpagedeveloper.com 2026

This document explains how to build and run web applications with XPScript. It covers the built-in Kestrel host, FastCGI, CGI, routing, Request, Response, Server, RequestScope, Application state and Session support.

The examples in this document use the web runtime that is implemented in the repository today.

## 1. Minimal web application

Create a directory for the site:

```text
site/
  index.xps
```

Create `site/index.xps`:

```xpscript
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/html; charset=utf-8"
    Response.Write("<h1>Hello from XPScript</h1>")
End Sub
```

Start the built-in Kestrel host:

```text
xpscript web --root ./site
```

The default listener is:

```text
http://127.0.0.1:8080
```

Open:

```text
http://127.0.0.1:8080/
```

`index.xps` is the default document.

## 2. Web route attributes

A web-callable `Sub` or `Function` must have route attributes immediately before the declaration.

The first attribute controls authentication:

```xpscript
[Anonymous]
```

or:

```xpscript
[Authenticated]
```

A route must also declare at least one HTTP method.

Supported method attributes are:

```text
[Get]
[Post]
[Put]
[Patch]
[Delete]
[Head]
[Options]
```

Example:

```xpscript
[Anonymous]
[Get]
Sub Index()
    Response.Write("GET request")
End Sub

[Anonymous]
[Post]
Sub Save()
    Response.Write("POST request")
End Sub
```

A procedure can allow more than one method:

```xpscript
[Anonymous]
[Get]
[Post]
Sub Edit()
    Response.Write(Request.Method)
End Sub
```

A `HEAD` request can use a route that permits `GET`. The runtime executes the route but the HTTP transports do not send the response body for HEAD.

### Authorization rules

Authenticated routes can require named rules:

```xpscript
[Authenticated]
[Rule:admin]
[Get]
Sub Admin()
    Response.Write("Admin area")
End Sub
```

A rule can also explicitly forbid access:

```xpscript
[Authenticated]
[Rule:!blocked]
[Get]
Sub Account()
    Response.Write("Account")
End Sub
```

Multiple rule attributes can be combined.

## 3. URL routing

Assume this site structure:

```text
site/
  index.xps
  users.xps
  admin/
    index.xps
```

XPScript resolves these URLs:

```text
/                  -> index.xps
/users             -> users.xps
/users.xps         -> users.xps
/admin/            -> admin/index.xps
/users/Save        -> procedure Save in users.xps
```

For example, `users.xps` can contain:

```xpscript
[Anonymous]
[Get]
Sub Index()
    Response.Write("User list")
End Sub

[Anonymous]
[Post]
Sub Save()
    Response.Write("Saved")
End Sub
```

Requests:

```text
GET  /users
POST /users/Save
```

The route-function part must be a valid XPScript identifier.

Path traversal such as `..`, malformed percent encoding, double-encoded traversal and symlink/reparse-point escapes outside the configured web root are rejected.

### Different default document

The normal default document is:

```text
index.xps
```

You can change it:

```text
xpscript web --root ./site --default-document home.xps
```

The same option exists for FastCGI:

```text
xpscript fastcgi --root /srv/site --default-document home.xps --listen 127.0.0.1:9000
```

The value must be one `.xps` filename. Paths such as `folder/home.xps` are intentionally rejected.

## 4. Request object

`Request` contains data from the current HTTP request.

### Basic request information

```xpscript
[Anonymous]
[Get]
Sub Info()
    Response.Write("Method: " & Request.Method)
    Response.Write("\nPath: " & Request.Path)
    Response.Write("\nPathInfo: " & Request.PathInfo)
    Response.Write("\nQuery: " & Request.QueryString)
    Response.Write("\nHost: " & Request.Host)
    Response.Write("\nScheme: " & Request.Scheme)
    Response.Write("\nProtocol: " & Request.Protocol)
    Response.Write("\nRemote: " & Request.RemoteAddress)
End Sub
```

Important request properties include:

```text
Request.Method
Request.Path
Request.PathInfo
Request.QueryString
Request.Headers
Request.ContentType
Request.ContentLength
Request.Body
Request.Host
Request.Scheme
Request.RemoteAddress
Request.Protocol
Request.Cookies
Request.IsCancellationRequested
```

All request data is untrusted input.

### Query-string values

For:

```text
/search?name=Fredrik&tag=one&tag=two
```

Read the first value:

```xpscript
name = Request.QueryFirst("name")
```

Read all values:

```xpscript
values = Request.Query("tag")
```

The runtime preserves multiple query values rather than silently concatenating them.

The default query parser limits are 16,384 characters and 256 fields.

### Headers

Read the first header value:

```xpscript
value = Request.HeaderFirst("User-Agent")
```

Read all values:

```xpscript
values = Request.Header("Accept")
```

Header names are case-insensitive.

### Cookies

Read one request cookie:

```xpscript
value = Request.Cookie("theme")
```

A missing cookie returns no value rather than throwing an error.

### Request body

Read UTF-8 body text:

```xpscript
body = Request.BodyText()
```

The default helper limit is 1 MiB.

You can use a smaller explicit limit:

```xpscript
body = Request.BodyText(65536)
```

Read the body as bytes:

```xpscript
bytes = Request.BodyBytes()
```

These helpers reject a body larger than the supplied limit.

The Kestrel host also enforces its own transport-level request-body limit. Its current default is 1 MiB.

## 5. HTML form data

### application/x-www-form-urlencoded

HTML:

```html
<form method="post" action="/contact/Save">
  <input name="name">
  <input name="email">
  <button type="submit">Save</button>
</form>
```

`contact.xps`:

```xpscript
[Anonymous]
[Post]
Sub Save()
    Dim name As String
    Dim email As String

    name = Request.FormFirst("name")
    email = Request.FormFirst("email")

    Response.ContentType = "text/plain; charset=utf-8"
    Response.Write("Name: " & name & "\n")
    Response.Write("Email: " & email)
End Sub
```

Use `Request.Form(name)` when a field may contain multiple values.

### multipart/form-data and uploaded files

The request runtime supports bounded multipart parsing.

Available helpers are:

```text
Request.Form(name)
Request.FormFirst(name)
Request.Files()
Request.Files(name)
Request.FileFirst(name)
```

Default multipart limits are:

```text
Total body:          16 MiB
Fields:              256
Files:               32
One file:            8 MiB
Part header bytes:   16 KiB
```

Do not assume an uploaded filename is a safe filesystem path. Treat filename, content type and file data as untrusted input.

## 6. Response object

`Response` controls the HTTP response.

### Write text

```xpscript
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.Write("Hello")
End Sub
```

`Response.Write` writes the value as text. It does not automatically HTML-encode the value.

### Status code

```xpscript
Response.StatusCode = 201
Response.Write("Created")
```

### Content type

```xpscript
Response.ContentType = "application/json; charset=utf-8"
```

### Response headers

```xpscript
Response.SetHeader("X-Application", "XPScript")
```

Append another value:

```xpscript
Response.AppendHeader("Vary", "Accept-Encoding")
```

Remove a header:

```xpscript
Response.RemoveHeader("X-Application")
```

Header names and values are validated. CR/LF response splitting is rejected.

Transport-owned headers such as `Content-Length`, `Transfer-Encoding`, `Connection`, `Keep-Alive` and `Upgrade` cannot be set directly through the normal Response API.

### Redirect

```xpscript
Response.Redirect("/login")
```

The default status is 302.

Explicit status:

```xpscript
Response.Redirect("/new-location", 301)
```

Supported redirect status codes are 301, 302, 303, 307 and 308.

### Clear response

```xpscript
Response.Clear()
```

This clears the body and application response headers and restores the normal response defaults.

### Binary output

```xpscript
Response.WriteBinary(data)
```

Use a suitable content type when returning binary data.

### Send a file from memory

The runtime has `SendFile` support for bounded in-memory file content and uploaded-file objects. It sets a safe `Content-Disposition` header and normalizes the supplied download filename.

## 7. Cookies

Set a basic response cookie:

```xpscript
Response.SetCookie("theme", "dark")
```

The runtime supports cookie options for:

```text
Path
Domain
Expires
MaxAge
Secure
HttpOnly
SameSite
```

Cookie names, values, paths and domains are validated. Control characters and response-splitting values are rejected.

A `SameSite=None` cookie must also use `Secure`.

Delete a cookie:

```xpscript
Response.DeleteCookie("theme")
```

Responses that set cookies are automatically marked `Cache-Control: no-store` unless that directive is already present.

## 8. Server object

`Server` exposes information about the current web host and safe helper functions.

Available information includes:

```text
Server.SiteId
Server.RootPath
Server.HostingMode
Server.StartTimeUtc
Server.RuntimeVersion
Server.Address
Server.Port
```

### Safe MapPath

Convert a site-relative path to a physical path:

```xpscript
path = Server.MapPath("data/example.txt")
```

`MapPath` constrains the result to the configured web root. Traversal and symlink/reparse-point escape attempts are rejected.

Do not build unrestricted filesystem paths by concatenating untrusted request values.

### HTML encoding

```xpscript
safe = Server.HtmlEncode(userInput)
Response.Write(safe)
```

Example:

```text
<x> -> &lt;x&gt;
```

### URL encoding

```xpscript
encoded = Server.UrlEncode(value)
```

### JSON string encoding

```xpscript
encoded = Server.JsonStringEncode(value)
```

This helper encodes one string as a JSON string value, including the surrounding JSON quotes. It intentionally does not serialize arbitrary CLR/runtime objects.

Example:

```xpscript
Response.ContentType = "application/json; charset=utf-8"
Response.Write("{\"name\":" & Server.JsonStringEncode(name) & "}")
```

For larger JSON structures, prefer XPScript's native JSON API rather than manually concatenating JSON.

## 9. RequestScope

`RequestScope` is temporary state that exists only for the current request.

```xpscript
RequestScope.Set("trace", "abc")
value = RequestScope.Get("trace")
```

Typical operations are:

```text
RequestScope.Get(name)
RequestScope.Set(name, value)
RequestScope.Exists(name)
RequestScope.Remove(name)
RequestScope.Unset(name)
RequestScope.Clear()
RequestScope.Count
RequestScope.Keys
```

A new request receives a new RequestScope. Values do not survive into the next request.

Use this for data that several functions in one request need to share without using global mutable state.

## 10. Application web state

The web `Application` object is shared state for one running site instance.

It is separate from the standalone command-line `Application` object documented in `docs/application.md`.

Example:

```xpscript
[Anonymous]
[Get]
Sub Counter()
    Dim value As Variant

    value = Application.Get("counter")

    If IsEmpty(value) Then
        value = 0
    End If

    value = value + 1
    Application.Set("counter", value)

    Response.Write(value)
End Sub
```

Available operations include:

```text
Application.Get(name)
Application.Set(name, value)
Application.Exists(name)
Application.Remove(name)
Application.Unset(name)
Application.Clear()
Application.Count
Application.Keys
```

The store is thread-safe and bounded.

Current default limits are:

```text
Entries:           256
One value:         64 KiB
Total state:       4 MiB
Idle timeout:      20 minutes
```

The initial state store only accepts scalar values, strings and byte arrays. Arbitrary CLR objects are rejected.

Application state is in-memory. It is lost when the server process restarts.

In CGI mode, each request normally runs in a new process, so in-memory Application state must not be used for persistence between requests.

## 11. Session support

The runtime contains a bounded in-memory session implementation.

When Session support is enabled by the hosting integration, scripts can use:

```text
Session.Id
Session.Started
Session.Count
Session.Keys
Session.Get(name)
Session.Set(name, value)
Session.Exists(name)
Session.Remove(name)
Session.Unset(name)
Session.Clear()
Session.RotateId()
Session.RegenerateId()
Session.Abandon()
Session.Destroy()
```

Example:

```xpscript
[Anonymous]
[Get]
Sub Cart()
    Session.Set("cart", "product-123")
    Response.Write(Session.Get("cart"))
End Sub
```

The default session cookie name in the runtime is:

```text
XPSID
```

Session identifiers are generated from 32 cryptographically secure random bytes and encoded in URL-safe Base64 form.

Default runtime session limits are:

```text
Idle timeout:          20 minutes
Maximum sessions:      10,000
Entries per session:   128
One value:             64 KiB
Bytes per session:     1 MiB
SameSite:              Lax
```

Session values use the same restricted state-value policy as Application state. Arbitrary runtime/CLR objects are not accepted.

Important: the current `xpscript web` CLI creates the standard Kestrel host without passing an `XpsSessionStore`. Therefore the Session object is not automatically enabled by the stock CLI command at this point. Session is available to hosts that construct the web runtime with an `XpsSessionStore`. If a script tries to access Session when the host has not enabled it, the runtime rejects the access.

CGI is process-per-request and is not suitable for in-memory session persistence across requests. Use Kestrel or FastCGI with a session-enabled host when persistent in-memory sessions are required.

## 12. Authentication with Session

The session runtime supports an authentication convention used by `[Authenticated]` and `[Rule:*]` routes.

Login example:

```xpscript
[Anonymous]
[Post]
Sub Login()
    ' Validate the submitted credentials first.

    Session.Authenticate("42", "Fredrik", "admin,editor")
    Response.Write("LOGIN")
End Sub
```

Authenticated route:

```xpscript
[Authenticated]
[Get]
Sub Account()
    Response.Write(Session.UserName)
End Sub
```

Admin route:

```xpscript
[Authenticated]
[Rule:admin]
[Get]
Sub Admin()
    Response.Write("ADMIN")
End Sub
```

Logout:

```xpscript
[Authenticated]
[Post]
Sub Logout()
    Session.SignOut()
    Response.Write("LOGOUT")
End Sub
```

`Session.Authenticate` rotates the session id after authentication to reduce session-fixation risk. `Session.SignOut` also rotates the id.

Available authentication information includes:

```text
Session.IsAuthenticated
Session.UserId
Session.UserName
Session.Rules
Session.HasRule(name)
```

## 13. Static files

Static file serving through the Kestrel CLI is disabled by default.

Enable it:

```text
xpscript web --root ./site --static-files
```

Example site:

```text
site/
  index.xps
  css/
    site.css
  js/
    app.js
  images/
    logo.png
```

The default static-file extension allowlist includes:

```text
.css .js .mjs
.png .jpg .jpeg .gif .webp .svg .ico
.woff .woff2 .ttf .otf
```

`.xps` files can never be added to the static-file allowlist.

The default maximum static file size is 32 MiB.

Override it from the CLI:

```text
xpscript web --root ./site --static-files --static-max-bytes 8388608
```

That example limits static files to 8 MiB.

## 14. Running Kestrel on another address

The safe default is loopback only:

```text
127.0.0.1:8080
```

For external access:

```text
xpscript web --root ./site --address 0.0.0.0 --port 8080 --host www.example.com
```

Host-header validation remains enabled. Add every expected external host explicitly with `--host` or `--allowed-host`.

Example with more than one host:

```text
xpscript web --root ./site \
  --address 0.0.0.0 \
  --port 8080 \
  --host www.example.com \
  --host api.example.com
```

## 15. HTTPS

Kestrel can load a certificate directly.

Store the password in an environment variable rather than putting it on the command line.

PowerShell example:

```powershell
$env:XPS_TLS_PASSWORD = "secret"
xpscript web --root .\site `
  --address 0.0.0.0 `
  --port 8443 `
  --host www.example.com `
  --https-cert .\server.pfx `
  --https-cert-password-env XPS_TLS_PASSWORD
```

Linux/macOS example:

```bash
export XPS_TLS_PASSWORD='secret'
xpscript web --root ./site \
  --address 0.0.0.0 \
  --port 8443 \
  --host www.example.com \
  --https-cert ./server.pfx \
  --https-cert-password-env XPS_TLS_PASSWORD
```

Supported protocol selections are:

```text
--protocols http1
--protocols http2
--protocols http1+2
```

The default is HTTP/1.1 plus HTTP/2.

## 16. Health, metrics and structured request logs

Enable health:

```text
xpscript web --root ./site --health
```

Endpoint:

```text
/_xps/health
```

Enable Prometheus-style metrics:

```text
xpscript web --root ./site --metrics
```

Endpoint:

```text
/_xps/metrics
```

Operational endpoints are loopback-only by default.

To expose them on the configured listener:

```text
xpscript web --root ./site --health --metrics --operational-external
```

Only expose operational endpoints when the surrounding network/proxy configuration restricts access appropriately.

Structured JSONL request logging:

```text
xpscript web --root ./site --structured-log ./logs/web.jsonl
```

Each dynamic Kestrel request receives a server-generated `X-Request-Id`. The same id is written into the structured request event for correlation.

The request id is generated by the server rather than trusted from an incoming client header.

Structured request logging intentionally excludes request paths, query strings, headers, cookies and body contents.

## 17. FastCGI

Start FastCGI over TCP:

```text
xpscript fastcgi --root /srv/xpsite --listen 127.0.0.1:9000
```

The default FastCGI listener is also loopback:

```text
127.0.0.1:9000
```

Linux/macOS can use a Unix-domain socket:

```text
xpscript fastcgi --root /srv/xpsite --unix-socket /run/xpscript/site.sock
```

The same dispatcher and `.xps` routing rules are used as with Kestrel.

Typical production deployment is:

```text
Browser
  -> nginx or another reverse proxy/web server
  -> FastCGI
  -> XPScript web dispatcher
  -> .xps route
```

Do not expose a FastCGI listener directly to an untrusted public network.

## 18. CGI and HCL Domino

XPScript includes a CGI host for environments that execute one CGI process per request, including HCL Domino.

The dedicated Domino deployment guide is:

```text
docs/hcl-domino-cgi.md
```

Typical Domino URL:

```text
https://www.example.com/xps-bin/XPScript.Web.Cgi.exe/index.xps
```

Function route:

```text
https://www.example.com/xps-bin/XPScript.Web.Cgi.exe/orders/save
```

The CGI host uses `PATH_INFO` for the XPScript route and constrains execution to `XPSCRIPT_WEB_ROOT`.

CGI is process-per-request. Do not rely on in-memory Application or Session state surviving between requests.

## 19. JSON response example

```xpscript
[Anonymous]
[Get]
Sub User()
    Dim name As String

    name = Request.QueryFirst("name")

    Response.ContentType = "application/json; charset=utf-8"
    Response.Write("{\"name\":" & Server.JsonStringEncode(name) & "}")
End Sub
```

Request:

```text
GET /user?name=Fredrik
```

Response:

```json
{"name":"Fredrik"}
```

For nested objects, arrays or non-trivial JSON, use the native XPScript JSON classes and `JsonStringify` rather than building JSON manually.

## 20. Simple HTML page example

```xpscript
[Anonymous]
[Get]
Sub Index()
    Dim name As String

    name = Request.QueryFirst("name")

    If Len(name) = 0 Then
        name = "World"
    End If

    Response.ContentType = "text/html; charset=utf-8"
    Response.Write("<!doctype html>")
    Response.Write("<html><head><title>XPScript</title></head><body>")
    Response.Write("<h1>Hello " & Server.HtmlEncode(name) & "</h1>")
    Response.Write("</body></html>")
End Sub
```

The HTML encoder is important because query-string values are untrusted.

## 21. POST API example

```xpscript
[Anonymous]
[Post]
Sub Create()
    Dim body As String

    body = Request.BodyText(65536)

    Response.StatusCode = 201
    Response.ContentType = "text/plain; charset=utf-8"
    Response.Write("Received " & CStr(Len(body)) & " characters")
End Sub
```

This route explicitly limits the body helper to 64 KiB even though the transport may permit a larger request.

## 22. Error behavior

Normal production dispatching uses generic error responses.

Typical results are:

```text
400 Bad Request
404 Not Found
401 Unauthorized
403 Forbidden
405 Method Not Allowed
500 Internal Server Error
```

Production error responses do not expose generated C#, source snippets, stack traces or physical filesystem paths.

Do not return exception messages directly to the browser from application code when they may contain secrets or internal information.

## 23. Security checklist

For a production XPScript web site:

1. Keep the web root dedicated to the application.
2. Do not put secrets in files that can be served as static content.
3. Keep `.xps` source serving disabled. The Kestrel static middleware refuses `.xps` files.
4. Use `Server.HtmlEncode` for untrusted text inserted into HTML.
5. Use the native JSON API or `Server.JsonStringEncode` for JSON string context.
6. Treat query strings, form data, headers, cookies, filenames and request bodies as untrusted.
7. Keep explicit size limits on request bodies and uploads.
8. Bind Kestrel and FastCGI to loopback unless external exposure is intentional.
9. Configure `--host` values explicitly when binding Kestrel externally.
10. Use HTTPS for authenticated traffic.
11. Keep health and metrics private unless external access is intentionally protected.
12. Rotate the session id after authentication. `Session.Authenticate` does this automatically.
13. Do not store arbitrary runtime objects in Session or Application state.
14. Do not depend on in-memory Session/Application persistence in CGI mode.
15. Put a production reverse proxy in front of FastCGI rather than exposing FastCGI itself to the Internet.

## 24. CLI quick reference

Kestrel:

```text
xpscript web --root DIR
xpscript web --root DIR --default-document home.xps
xpscript web --root DIR --address 0.0.0.0 --port 8080 --host www.example.com
xpscript web --root DIR --protocols http1+2
xpscript web --root DIR --https-cert server.pfx --https-cert-password-env XPS_TLS_PASSWORD
xpscript web --root DIR --health --metrics
xpscript web --root DIR --structured-log logs/web.jsonl
xpscript web --root DIR --static-files
xpscript web --root DIR --static-files --static-max-bytes 8388608
```

FastCGI:

```text
xpscript fastcgi --root DIR --listen 127.0.0.1:9000
xpscript fastcgi --root DIR --default-document home.xps --listen 127.0.0.1:9000
xpscript fastcgi --root DIR --unix-socket /run/xpscript/site.sock
```

CGI deployment is documented separately in `docs/hcl-domino-cgi.md`.
