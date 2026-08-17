# XPScript web runtime regression matrix

(c) xpagedeveloper.com 2026

This matrix defines the minimum regression gates for Kestrel, FastCGI and CGI work. A TODO item should be marked complete only when its applicable gates pass.

## Common runtime

| Area | Required coverage |
| --- | --- |
| Compiler reuse | Web execution uses the shared parser/compiler pipeline and does not fork language semantics. |
| Include diagnostics | Errors retain original include filename, line and position. |
| Request isolation | Concurrent requests cannot observe another request's Request, Response or principal. |
| Application isolation | Separate configured sites cannot read or mutate each other's Application state. |
| Session isolation | Session data is scoped to the correct session/site and concurrent access follows defined semantics. |
| Error handling | Production errors never disclose source, generated C#, secrets or filesystem paths. |
| Cancellation | Client disconnect cancellation reaches the runtime handler. |
| Resource limits | Body, header, cache and compilation limits reject or throttle excess input predictably. |

## Routing and filesystem security

Required tests:

- `/` resolves the configured default document.
- `/foo.xps` resolves only inside the configured root.
- Extensionless routing follows the documented mapping policy.
- Missing sources return 404 without exposing `.xps` content.
- Percent-encoded traversal is rejected.
- Double-encoded or ambiguous traversal is rejected.
- Backslash traversal is rejected on all platforms.
- Windows drive, UNC and reparse-point escapes are rejected.
- Linux/macOS symlink escapes are rejected according to policy.
- Static files remain opt-in.
- `.xps` can never be served as a static file.
- Static extension allowlist and maximum file-size limits are enforced.

## Kestrel

Run on Windows, Ubuntu and macOS where the feature is portable.

Required tests:

- Default bind is loopback.
- Explicit bind address and port work.
- HTTPS certificate configuration works and invalid certificate configuration fails closed.
- HTTP/1.1 works.
- HTTP/2 configuration is accepted where supported by the test environment.
- Request body limit returns 413 when exceeded.
- Request-line and aggregate-header limits are applied.
- Header and keep-alive timeouts are configured.
- Minimum data-rate protections can be configured or disabled according to policy.
- Host allowlist rejects unexpected Host values.
- Forwarded headers are ignored unless a trusted proxy is configured.
- Trusted proxy forwarding accepts only the configured hop limit.
- Graceful shutdown is observable by the runtime.
- Request cancellation is propagated.
- Static serving is disabled by default.
- Static serving works only for allowed extensions when enabled.
- Health and metrics endpoints remain local-only by default when enabled.

## FastCGI

Run transport-level tests on every supported platform.

Required tests:

- Valid responder request lifecycle.
- PARAMS split across multiple records.
- STDIN split across partial network reads.
- Empty PARAMS and STDIN terminators.
- CONTENT_LENGTH and configured request-body limits.
- Malformed version, type, request id, content length and padding rejection.
- Truncated records.
- Oversized PARAMS/header counts and values.
- Integer-overflow boundary inputs.
- SCRIPT_FILENAME canonicalization remains inside configured root.
- PATH_INFO and SCRIPT_NAME precedence follows documented policy.
- Status, headers and body encode correctly into response records.
- Keep-connection behavior only when explicitly supported.

If a custom FastCGI parser is used, add fuzz/adversarial input coverage before production release.

## CGI

Required tests:

- CGI environment variables map to the same common request model as FastCGI/Kestrel.
- Request body reads are bounded by configured limits.
- SCRIPT_FILENAME and PATH_INFO cannot escape the configured root.
- Response status and headers use valid CGI output formatting.
- CR/LF response splitting is rejected.
- Runtime and compiler diagnostics match other transports.

## Session and cookies

Required tests:

- Session identifiers are generated with a cryptographically secure random source.
- Session ids rotate after authentication/privilege transitions when requested.
- HttpOnly is enabled by default for session cookies.
- Secure is enabled when HTTPS policy requires it.
- SameSite policy is explicit.
- Abandon invalidates the current session.
- Session memory limits are enforced.
- Invalid cookie names/control characters are rejected.
- Cookie output cannot inject response headers.

## Response security

Required tests:

- Header names reject invalid token characters.
- Header values reject CR/LF.
- Content-Length cannot conflict with runtime-owned response framing.
- Hop-by-hop headers follow the documented policy.
- Redirect values cannot cause header injection.

## Cache and recompilation

Required tests:

- Root source changes invalidate compiled units.
- Include changes invalidate dependent compiled units.
- Compiler/preprocessor configuration changes invalidate cache entries.
- Cache keys include site/root identity.
- Two sites with identical relative paths cannot share compiled units accidentally.
- Concurrent first requests compile once per cache key.
- In-flight requests can finish on an immutable old compiled unit while new requests use the replacement.
- Bounded cache eviction does not break active requests.

## CI gate

For changes to common web runtime behavior, require build and relevant regression suites on Windows, Ubuntu and macOS.

Transport-specific exceptions must be documented as platform restrictions. A green build alone does not complete a security-sensitive TODO. The applicable regression gate must also pass.
