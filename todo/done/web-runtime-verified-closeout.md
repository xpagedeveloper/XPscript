# Web runtime verified closeout

(c) xpagedeveloper.com 2026

This closeout records web-runtime functionality that is already implemented and covered by repository regression workflows. The older `todo/web-runtime-server-todo.md` predates much of the current implementation and therefore contains many stale unchecked boxes.

## Architecture and execution model

Verified repository state includes:

- one shared XPScript web runtime used by Kestrel, FastCGI and CGI adapters
- trusted in-process execution as the current execution model
- root-constrained path resolution
- compiler/cache isolation from request-controlled output paths
- explicit architecture and regression-matrix documents under `docs/architecture/`
- production security review and security closeout documentation

## Routing and path security

Implemented and regression-tested behavior includes:

- `/` to the configured default document
- `/foo` and `/foo.xps` script routing
- `/folder/` directory default-document routing
- `/foo/save` procedure routing
- configurable default document with `index.xps` as the standard default
- 404 for missing scripts/routes
- no raw XPScript source disclosure on compile/runtime failure
- canonical path validation
- malformed and double-encoded traversal rejection
- `..` traversal rejection
- symlink/reparse-point containment checks
- root-constrained `Server.MapPath`
- route attributes including authentication, HTTP method and rule checks

## Kestrel

Implemented and regression-tested Kestrel behavior includes:

- .NET 10 ASP.NET Core/Kestrel hosting
- loopback-safe default binding
- configurable address and port
- HTTPS certificates
- HTTP/1.1, HTTP/2 and combined HTTP/1.1+HTTP/2 modes
- bounded request bodies
- request-line and header limits
- header and keep-alive timeouts
- minimum data-rate protections
- maximum concurrent connection setting
- request cancellation on client disconnect
- explicit trusted proxy handling
- Host validation
- graceful shutdown
- default `Server` header disabled
- configurable default security headers
- health and metrics endpoints
- static-file serving only through an explicit extension/content-type allowlist
- `.xps` source excluded from static-file serving

## FastCGI

Implemented and regression-tested FastCGI behavior includes:

- responder role over TCP
- Unix-domain sockets on supported Unix platforms
- nginx deployment coverage
- canonical script/root containment
- standard FastCGI/CGI parameter mapping
- bounded PARAMS/header/body handling
- fixed-width protocol parsing with range validation
- malformed/truncated record rejection
- partial network-read regression coverage
- keep-connection handling
- status/header/body response records
- HEAD body suppression
- parser fuzz/adversarial coverage

## CGI

Implemented CGI behavior includes:

- process-per-request CGI adapter
- standard CGI environment mapping
- bounded stdin request-body handling
- common XPScript routing and execution
- HEAD body suppression
- explicit XPScript web-root containment
- documented HCL Domino deployment using `XPScript.Web.Cgi`

## Web objects

The common runtime exposes and regression-tests:

- `Request`
- `Response`
- `Server`
- `RequestScope`
- `Application`
- `Session`
- cookie APIs

Request support includes query/header multi-values, cookies, bounded body text/binary access, form-urlencoded data, multipart uploads and cancellation state.

Response support includes status, content type, validated headers, cookies, text/binary output, redirects, clear/completion behavior and bounded response storage.

Server helpers include root-constrained mapping plus HTML, URL and JSON-string encoding helpers.

## State and authentication

Verified behavior includes:

- bounded thread-safe Application state
- bounded in-memory Session store
- cryptographically random session identifiers
- session rotation/regeneration
- authentication state and named rules
- `[Authenticated]`, `[Rule:name]` and `[Rule:!name]` route enforcement
- CSRF helper tokens using HMAC-SHA256 with a server-held secret
- session and cache metrics without exposing session identifiers

## Observability and hardening

Implemented behavior includes:

- generated request/correlation id
- `X-Request-Id` response header
- structured request telemetry
- health and Prometheus-style metrics
- generic production-facing error responses
- response-splitting validation
- bounded request, response, form, multipart, state and cache resources
- compile-cache single-flight behavior
- security headers and source-disclosure protections

## Permanent verification

The web runtime is covered by multiple dedicated GitHub Actions workflows, including Kestrel transport/HTTPS/static/state/session/metrics/CLI/shutdown, FastCGI/nginx/parser/adversarial tests, CGI tests, dispatcher/cache/security tests, CSRF tests and web security closeout gates.

The historical checklist should be reconciled against this closeout rather than interpreting every remaining unchecked box in `todo/web-runtime-server-todo.md` as missing functionality.
