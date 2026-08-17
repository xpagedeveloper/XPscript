# XPScript web runtime security closeout

(c) xpagedeveloper.com 2026

## Review baseline

This closeout reviews the implemented XPScript web runtime against the OWASP Top 10 2025 risk categories and OWASP ASVS 5.0.0 design themes.

The trust boundary remains explicit: XPScript application source is trusted server-side code. HTTP, proxy, CGI and FastCGI inputs are untrusted. Collectible AssemblyLoadContext provides lifecycle isolation, not a sandbox for hostile source.

The architecture review is documented in `docs/web-runtime-security-review.md`. Production limits are documented in `docs/web-runtime-production-limits.md`.

## Verified control map

| OWASP Top 10 2025 area | Implemented XPScript control |
|---|---|
| Broken Access Control | canonical site-root routing, explicit route method/auth/rule policies, request-local principal |
| Security Misconfiguration | loopback defaults, Host allowlist, trusted-proxy allowlist, disabled operational endpoints by default, documented bounded limits |
| Software Supply Chain Failures | .NET/ASP.NET Core platform primitives are preferred; the custom FastCGI parser has no protocol dependency and is covered by deterministic malformed-input regression |
| Cryptographic Failures | 256-bit cryptographic session identifiers, rotation support, Secure session cookies on HTTPS, explicit SameSite |
| Injection | strict header/cookie validation, canonical path resolution, no request-controlled compiler output path, bounded FastCGI parsing |
| Insecure Design | architecture ADR/review, explicit trusted-source boundary, site-isolated cache/state and bounded resource model |
| Authentication Failures | authenticated route metadata, strong server-side session identifiers, session rotation and invalidation |
| Software or Data Integrity Failures | immutable compiled generations, Include/dependency fingerprinting and site/configuration-scoped cache identity |
| Security Logging and Alerting Failures | health, Prometheus metrics and privacy-preserving JSONL request events |
| Mishandling of Exceptional Conditions | generic client errors, compile failure backoff, parser fail-closed behavior and graceful shutdown regression |

## Session cache protection

A response that sets a cookie now enforces `Cache-Control: no-store`. `Response.Complete()` reasserts the directive when `Set-Cookie` is present, so later application code cannot accidentally turn a session-bearing response into a cacheable response.

The focused security regression verifies this behavior for new sessions, rotated sessions and abandoned sessions. It also verifies response-header, cookie-value and cookie-path injection rejection and transport-owned `Content-Length` protection.

## Existing permanent evidence

The web runtime has permanent cross-platform gates for:

- runtime core routing/path/header security
- Kestrel transport and proxy/Host behavior
- compiler target and collectible assembly loading
- bounded dispatcher/cache behavior and Include invalidation
- Request/Response/Server runtime objects
- Session/Application state
- CGI including HCL Domino PATH_INFO mode
- FastCGI TCP, Unix socket, malformed/fragmented protocol input and nginx integration
- public web/FastCGI CLI commands
- 2,000-request concurrency soak
- health, metrics and structured telemetry privacy
- Kestrel/FastCGI graceful shutdown and cleanup
- focused session/cache/header/cookie security closeout

Applicable gates execute on Windows, Ubuntu and macOS.

## Deliberate non-features and remaining detailed checklist work

The security review does not convert optional or unimplemented features into claims:

- hostile/untrusted XPScript source is not sandboxed
- built-in multipart upload handling is not part of the initial Request API
- static-file serving is not enabled by the XPScript runtime by default
- `Response.Flush` streaming semantics are not exposed
- CSRF protection remains an application/deployment responsibility; SameSite is not represented as a complete CSRF defense
- CSP and application-specific browser response policy remain application/deployment policy
- in-memory Session/Application are process-local and require a designed shared provider for non-affine multi-node deployments
- direct Kestrel HTTPS configuration and explicit protocol/slow-client policy remain separate detailed checklist items until their own regression gates pass

## Release rule

No unchecked detailed item in `todo/web-runtime-server-todo.md` is considered implemented merely because this security review exists. The short section 18 entry in `todo/runtime-reference-todo.md` may record the architecture/security review and shared-runtime implementation as verified, while the historical sequencing constraint and any detailed feature gaps remain explicit.
