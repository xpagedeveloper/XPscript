# CSRF protection

XPScript has built-in CSRF protection for browser requests that use Session or other cookies. The protection applies before REST binding and before the target XPScript route is invoked.

CSRF protection applies to unsafe HTTP methods:

- `POST`
- `PUT`
- `PATCH`
- `DELETE`

A request that fails CSRF validation receives HTTP 403 with `application/problem+json`. The route procedure is not executed.

## UIForm

Server-rendered UIForm forms are protected automatically. XPScript adds a hidden field named `__xps_csrf` when an active Session is available.

Normal UIForm code does not need to create or validate the token manually:

```xpscript
[Anonymous]
[Get]
[Post]
Sub Index()
    Dim form As New UIForm("Customer")
    Call form.AddTextField("name", "Name")
    Call form.AddEmailField("email", "Email")
    Call form.ShowDialog()
End Sub
```

When the form is posted with the session cookie, the dispatcher validates the token before the route runs.

## Manually rendered HTML forms

For a form that you render yourself, obtain the current session-bound token from `Server.CsrfToken()` and submit it as `__xps_csrf`.

```xpscript
Dim token As String
token = Server.CsrfToken()

Response.Write("<form method=\"post\">")
Response.Write("<input type=\"hidden\" name=\"__xps_csrf\" value=\"" & Server.HtmlEncode(token) & "\">")
Response.Write("<input type=\"text\" name=\"name\">")
Response.Write("<button type=\"submit\">Save</button>")
Response.Write("</form>")
```

`Server.CsrfToken()` requires an active Session. Do not generate your own CSRF token and do not store a fixed token in source code or configuration.

## Custom browser REST requests

For browser code that sends an unsafe request using the session cookie, send the token in the `X-XPS-CSRF-Token` header.

A server-rendered page can expose the token to its own same-origin JavaScript through a meta element:

```xpscript
Dim token As String
token = Server.CsrfToken()
Response.Write("<meta name=\"xps-csrf-token\" content=\"" & Server.HtmlEncode(token) & "\">")
```

The browser request can then send it:

```javascript
const token = document.querySelector('meta[name="xps-csrf-token"]').content;

await fetch('/api/customer/42', {
  method: 'PATCH',
  credentials: 'same-origin',
  headers: {
    'Content-Type': 'application/json',
    'X-XPS-CSRF-Token': token
  },
  body: JSON.stringify({ name: 'Example' })
});
```

The header name is `X-XPS-CSRF-Token`. The hidden form field name is `__xps_csrf`.

## Browser WebAssembly

The XPScript browser-WASM `XPHttpClient` handles the CSRF token flow automatically for unsafe same-origin requests.

The first unsafe request can receive HTTP 403 together with a fresh `X-XPS-CSRF-Token` response header. The browser-WASM client retains that token for the request flow and retries the request once with the token header. The Session cookie remains HttpOnly and is not read by WebAssembly code.

Normal browser-WASM code therefore uses the ordinary HTTP API:

```xpscript
Dim http As New XPHttpClient
Dim payload As New XPJsonObject
Dim result As XPHttpResponse

Call payload.Set("name", "Example")
Set result = http.PatchJson("/api/customer/42", payload)
```

Do not add a second CSRF implementation around the built-in browser-WASM client unless you intentionally replace the XPScript HTTP transport.

## REST API and bearer tokens

CSRF protects browser credentials that are attached automatically, especially cookies. A bearer-only API request with no cookies does not require a CSRF token.

For example, a non-browser API client using only:

```text
Authorization: Bearer <token>
```

is not rejected for missing CSRF when no cookies are present.

If a request includes session cookies, treat it as cookie-authenticated browser traffic and send the CSRF token for unsafe methods.

## Token properties

The XPScript token is bound to the active Session and site. The runtime derives it using HMAC-SHA-256 and a per-runtime secret. Validation uses a fixed-time comparison.

Applications should treat the token as opaque. Do not parse it, modify it or depend on its encoded length or internal format.

## Failure response

Missing or invalid tokens return HTTP 403. XPScript returns Problem Details and does not invoke the target route.

The response also uses `Cache-Control: no-store`. For browser-WASM challenge handling, XPScript can return a fresh token in the `X-XPS-CSRF-Token` response header.

## XSS interaction

CSRF tokens do not protect against XSS. If an attacker can execute JavaScript in your origin, that script can usually read a token exposed to browser JavaScript and make same-origin requests.

Keep dynamic HTML encoded with `Server.HtmlEncode()`. Prefer UIForm controls, JSON response helpers and DOM text APIs instead of concatenating untrusted HTML.

XPScript also emits web security headers including `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy` and a Content Security Policy for HTML responses.

## Practical rules

1. Use UIForm normally. Its CSRF field is automatic.
2. For manually rendered HTML forms, include `Server.CsrfToken()` as `__xps_csrf`.
3. For custom same-origin JavaScript using Session cookies, send `X-XPS-CSRF-Token`.
4. For XPScript browser-WASM `XPHttpClient`, use the normal methods. Challenge/retry is automatic.
5. Bearer-only requests without cookies do not need CSRF.
6. Never disable authorization, role checks or input validation because CSRF protection is enabled.
