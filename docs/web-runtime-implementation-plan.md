# XPScript web runtime implementation plan

(c) xpagedeveloper.com 2026

This plan implements section 18 without splitting language semantics between Kestrel, FastCGI and CGI.

## Phase 1, architecture and transport-neutral core

Deliverables:

- architecture decision and trust boundary
- threat model/security review
- `XPScript.Web.Runtime` project
- request/response/context/principal models
- shared path resolver and routing precedence
- response-header injection protection
- route authorization result model
- cross-platform core smoke/security gate

Exit criteria:

- solution builds on Windows, Ubuntu and macOS
- routing/traversal/response/auth regression passes on all three

## Phase 2, Kestrel transport

Deliverables:

- `XPScript.Web.Kestrel` .NET 10 host
- `xpscript web` command integration
- loopback default bind
- explicit address/port
- request-body, connection and timeout limits
- allowed-host validation
- trusted-proxy forwarded-header configuration
- HTTPS configuration hooks
- request cancellation and graceful shutdown
- adapter from ASP.NET Core `HttpContext` to `XpsWebRequest` and from `XpsWebResponse` back to ASP.NET Core

Exit criteria:

- loopback integration tests issue real HTTP requests
- request/response multi-value headers and body limits verified
- forwarded headers ignored unless proxy is trusted
- Host filtering regression verified
- Windows/Linux/macOS gate green

## Phase 3, web compiler target and route metadata

Deliverables:

- reuse normal Include/preprocessor/compiler pipeline
- web compilation target emits loadable assembly rather than console-only executable
- deterministic `WebMain` entry contract
- compile-time route metadata for exported route functions
- `[Anonymous]`, `[Authenticated]`, HTTP-method and `[Rule:...]` preprocessing/validation
- production diagnostic redaction
- no raw `.xps` serving

Exit criteria:

- root/index/direct/function routes execute real XPScript
- unauthorized route body cannot execute
- source/stack/generated C# never returned in production responses

## Phase 4, bounded compilation cache

Deliverables:

- canonical site/source/include/compiler/preprocessor cache identity
- content hashing and dependency invalidation
- single-flight compilation
- bounded cache and failure backoff
- immutable generations
- collectible AssemblyLoadContext lifecycle
- concurrent request-safe atomic replacement

Exit criteria:

- Include change invalidates cache
- concurrent first requests compile once
- failed source does not create a compile storm
- site ids cannot share cache entries
- old assembly can unload after active requests release it

## Phase 5, Request/Response/Server object surface

Deliverables:

- XPScript bindings for Request, Response and Server
- query, headers, cookies, bounded body access
- form-urlencoded support
- safe redirect and headers
- HTML/URL encoding helpers as explicit helpers
- safe `Server.MapPath` constrained to site root

Multipart upload is a separate bounded feature and may follow after core form support.

Exit criteria:

- object API compatibility examples and negative security tests green

## Phase 6, Session and Application

Deliverables:

- site-isolated bounded in-memory stores behind interfaces
- cryptographically random session id
- HttpOnly/Secure/SameSite cookie policy
- rotation, timeout, abandon
- restricted safe value model
- concurrency semantics
- principal/rule population hooks

Exit criteria:

- fixation/rotation tests
- cross-site isolation tests
- same-session concurrency tests
- bounded-memory tests

## Phase 7, CGI adapter

Deliverables:

- one-request CGI adapter using environment/stdin/stdout
- same common request model and dispatcher
- HCL Domino deployment documentation
- strict CONTENT_LENGTH and environment normalization
- stdout protocol isolation, stderr logging

Exit criteria:

- simulated CGI environment integration tests on supported OS
- no compiler/log noise on stdout

## Phase 8, FastCGI adapter

Before coding, repeat maintained-package review.

If no package satisfies requirements:

- implement responder-only state machine behind transport interface
- TCP listener first
- Unix socket on Linux/macOS later in same phase
- bounded PARAMS/body/header limits
- partial-read state machine
- checked length arithmetic
- request abort/cleanup
- nginx integration fixture
- fuzz/adversarial corpus

Exit criteria:

- malformed and fragmented records fail safely
- nginx `fastcgi_pass` integration works
- no request can select source outside site root
- parser fuzz/boundary regression green

## Phase 9, hardening and production documentation

Deliverables:

- complete OWASP-oriented review
- deployment docs for Kestrel, reverse proxy, CGI and FastCGI
- security boundaries and trusted-source warning
- all numeric limits documented
- logs/metrics/health behavior
- performance/concurrency soak tests
- cleanup/graceful shutdown tests

Only after this phase may section 18 be described as production-ready.
