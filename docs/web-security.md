# XPScript web security guidance

This document describes production security controls for applications hosted with `xpscript web`, FastCGI, or CGI. It complements `docs/web-runtime.md` and `docs/web-host-config.md`.

## Production baseline

Use HTTPS at the public edge. Bind directly to HTTPS with Kestrel or terminate TLS at a trusted reverse proxy. Do not expose a plain HTTP listener to an untrusted network when credentials, sessions, or private data are used.

Keep operational endpoints local unless an authenticated monitoring layer protects them. The default local-only behavior for health and metrics is safer than exposing them publicly.

Configure an explicit Host allowlist for externally reachable Kestrel deployments. Configure trusted proxies explicitly before relying on forwarded client address, host, or scheme information.

Run the XPScript process with the minimum OS permissions required by the application. In-process XPScript code has the privileges of the hosting process and is not a security sandbox.

## Recommended response security headers

Set security headers at the application or reverse proxy. The exact policy depends on the application, so XPScript does not inject a universal Content-Security-Policy automatically.

A reasonable starting point for an HTML application is:

```text
X-Content-Type-Options: nosniff
Referrer-Policy: strict-origin-when-cross-origin
Content-Security-Policy: default-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'
Permissions-Policy: camera=(), microphone=(), geolocation=()
```

For sites that must be embedded in frames, adjust `frame-ancestors` deliberately. Do not copy a CSP containing `unsafe-inline` or broad wildcard sources unless the application requires them and the risk has been reviewed.

When HTTPS is permanent, configure HSTS at the TLS endpoint:

```text
Strict-Transport-Security: max-age=31536000; includeSubDomains
```

Do not enable `includeSubDomains` until every affected subdomain is HTTPS-capable. HSTS should normally be configured by the public Kestrel endpoint, IIS, nginx, or another TLS reverse proxy.

Static files served by the built-in Kestrel static-file support already receive `X-Content-Type-Options: nosniff`.

## Sessions and cookies

Enable sessions only when needed:

```text
xpscript web --root ./site --sessions --session-secure --session-same-site Lax
```

For production HTTPS applications, use Secure session cookies. Prefer `SameSite=Lax` or `Strict` unless a cross-site workflow genuinely requires `None`. `SameSite=None` cookies must be Secure in modern browsers.

Do not store passwords, bearer tokens, private keys, or large objects in session state unless the application design explicitly requires it. Never write session IDs or complete Cookie/Set-Cookie headers to application logs.

Rotate the session ID after authentication or another privilege transition using the Session API.

## CSRF protection

SameSite cookies reduce CSRF exposure but do not replace explicit CSRF protection for state-changing browser requests.

For HTML forms and browser APIs that use cookie-based authentication:

1. Use GET and HEAD only for read-only operations.
2. Require POST or another state-changing HTTP method for mutations.
3. Generate a cryptographically random CSRF token when the user session is established.
4. Store the expected token in session state.
5. Include the token in each state-changing form or request, for example as a hidden form field or a custom request header.
6. Compare the submitted token with the session token before performing the mutation.
7. Reject missing or mismatched tokens with HTTP 403.
8. Rotate the CSRF token when the authenticated session is rotated when practical.

Do not put CSRF tokens in URLs. URLs are commonly stored in browser history, access logs, analytics, and Referer headers.

For JSON APIs called by browser JavaScript, a custom header such as `X-CSRF-Token` is preferable to a query-string token. Validate the request Content-Type and Origin/Referer where appropriate as additional defense-in-depth controls.

Bearer-token APIs that do not authenticate through ambient browser cookies have a different CSRF threat model. Do not add a CSRF mechanism blindly when authentication credentials are already supplied explicitly per request.

## Output encoding

Encode data for the context where it is inserted.

Use HTML encoding for untrusted text inserted into HTML text or attribute contexts. Use URL encoding for URL components. Use `Server.JsonStringEncode` for string data inserted into JSON. Do not assume HTML encoding is safe for JavaScript, CSS, URLs, or JSON.

Prefer structured JSON generation over string concatenation for API responses.

## File paths and uploads

Treat filenames, URL paths, upload names, and multipart metadata as untrusted input.

Use the XPScript web path APIs rather than concatenating user input into filesystem paths. The runtime rejects lexical traversal and web-root escapes through symlinks/reparse points.

Apply application-specific upload type validation. A Content-Type header and filename extension are claims from the client, not proof of file contents. Store uploaded files outside executable/script locations when possible.

## Reverse proxy deployment

When IIS, nginx, or another proxy fronts Kestrel:

- Keep Kestrel on a private or loopback listener where possible.
- Configure only known proxy addresses as trusted proxies.
- Validate Host values at the public edge and in XPScript.
- Set request and body limits at both layers.
- Terminate TLS with a maintained certificate and modern protocol configuration.
- Do not trust arbitrary client-supplied `X-Forwarded-*` headers.

For FastCGI, restrict the TCP listener or Unix socket so untrusted clients cannot connect directly. A Unix socket should have filesystem permissions limited to the web server and XPScript service accounts.

## Error handling and logging

Production HTTP errors must remain generic. Do not return generated C#, stack traces, filesystem paths, secrets, or compiler internals to remote clients.

Structured request logging intentionally avoids Authorization, Cookie, Set-Cookie, passwords, tokens, and full request bodies. Keep the same rule in application logs.

Use the server-generated `X-Request-Id` to correlate a client-visible request with server-side events. Do not accept an untrusted client request ID as the authoritative server correlation ID.

## Deployment checklist

Before exposing an XPScript application to an untrusted network, verify:

- HTTPS is enforced at the public endpoint.
- Host allowlisting is configured.
- Trusted proxy addresses are explicit.
- Request, upload, response, and static-file limits fit the application.
- Session cookies use the intended Secure and SameSite policy.
- State-changing cookie-authenticated browser requests use CSRF protection.
- CSP and other response security headers match the application.
- Health and metrics endpoints are not unintentionally public.
- The process account has minimal filesystem and network privileges.
- Secrets come from protected environment/configuration mechanisms and are not committed to the repository.
- Structured logs do not contain credentials or session identifiers.
- Production error responses do not expose diagnostics.
