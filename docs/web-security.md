# Web security

XPScript web applications use layered protection for browser requests.

## CSRF protection

Unsafe methods are POST, PUT, PATCH and DELETE.

For cookie/session-based browser requests, XPScript validates a session-bound HMAC CSRF token before REST binding and before the XPScript route procedure executes.

Server-rendered UIForm forms automatically receive a hidden field:

```html
<input type="hidden" name="__xps_csrf" value="...">
```

Applications do not need to add this field manually when UIForm renders the form.

For programmatic browser requests, the token can be sent in:

```text
X-XPS-CSRF-Token: <token>
```

A missing or invalid token returns HTTP 403 and the route does not execute.

Bearer-only API calls without browser cookies are not forced through CSRF validation. CSRF protects browser cookie/session authentication, not Authorization bearer tokens.

### Browser WebAssembly

The browser-wasm HttpClient has automatic CSRF challenge handling. On the first unsafe same-origin request without a token, the server returns HTTP 403 with a fresh `X-XPS-CSRF-Token` response header. The generated browser-wasm HttpClient retries the request once with that token.

The session cookie remains HttpOnly. WebAssembly code never reads the session cookie.

Applications can still explicitly set `X-XPS-CSRF-Token` when needed. Explicit headers are not overwritten.

CSRF tokens are bound to the server site instance and session id. Session id rotation invalidates previously issued tokens.

## XSS protection

XPScript applies several layers:

- UIForm values and labels are HTML encoded before being inserted into generated HTML.
- Browser WebAssembly UI rendering uses DOM APIs such as `textContent` for labels and option text instead of injecting user strings through `innerHTML`.
- HTML responses receive `Content-Security-Policy`, `X-Content-Type-Options: nosniff`, `Referrer-Policy`, `X-Frame-Options` and a restrictive `Permissions-Policy` unless the application already supplied the header.
- `Server.HtmlEncode(value)` is available for dynamic values written into HTML.
- `Server.JsonStringEncode(value)` is available for values placed into JSON/string contexts.

`Response.Write()` remains a raw response writer because applications need to generate HTML. Do not concatenate untrusted request data directly into raw HTML.

Use:

```xpscript
Response.Write("<p>" & Server.HtmlEncode(Request.QueryFirst("name")) & "</p>")
```

instead of:

```xpscript
Response.Write("<p>" & Request.QueryFirst("name") & "</p>")
```

RichTextField stores HTML intentionally. Stored rich-text HTML must be treated as untrusted when rendered outside the editor and should be passed through an application allow-list sanitizer before raw rendering.

## Server-side script injection protection

XPScript does not compile request bodies, query-string values or form fields as XPScript source code.

`Evaluate` uses a restricted expression evaluator. It does not expose `Shell`, `CreateObject`, `GetObject`, compiler APIs, file execution APIs or arbitrary method invocation. Attempts to call unsupported functions fail with a runtime error.

Dynamic Evaluate input is limited to 32768 characters to reduce parser and resource-exhaustion abuse.

Do not build XPScript source files from request data and invoke the compiler manually from host application code. Keep code and request data separate.

## Security behavior

CSRF validation happens before REST parameter binding and before the route procedure executes. A rejected request therefore cannot trigger route-side writes or database operations.

The web security regression suite verifies valid, missing and modified CSRF tokens, bearer-token compatibility, security headers, HTML encoding and blocked server-side Evaluate access to Shell.
