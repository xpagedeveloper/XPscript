# XPScript web runtime security review

(c) xpagedeveloper.com 2026

Scope: section 18 web runtime, Kestrel, FastCGI, CGI, routing, runtime compilation, cache, Request/Response/Server/Session/Application and route authorization metadata.

## Trust model

XPScript application source is trusted server-side code in the initial implementation. HTTP, FastCGI, CGI, proxy, cookie, query, form, header and body data are untrusted.

Collectible AssemblyLoadContext provides unload/lifecycle isolation only. It is not a security sandbox. Mutually untrusted tenants require separate worker processes or stronger OS/container isolation.

## Primary security boundaries

1. Network/client to transport adapter.
2. Transport adapter to normalized XPScript request.
3. URL/path metadata to site-root resolver.
4. Source filesystem to compiler/include/preprocessor pipeline.
5. Compiler output to load context/cache.
6. Request context to XPScript route execution.
7. Session/user principal to route authorization.
8. XPScript response to transport framing.
9. One configured site to another site's cache/session/application state.

## Threats and required controls

### Path traversal and source disclosure

Threats:

- encoded or double-encoded traversal
- slash/backslash ambiguity
- Windows drive/UNC escape
- symlink/reparse-point escape
- request-controlled SCRIPT_FILENAME
- accidental static serving of .xps source

Controls:

- one canonical path resolver for every transport
- decode once, reject malformed encoding and control characters
- validate URL segments before filesystem composition
- canonical Path.GetFullPath containment check
- explicit symlink/reparse policy
- compiler output path never derived from request data
- .xps and compiler artifacts denied by static file policy
- compilation failure returns opaque 500, never source

### Request smuggling and transport ambiguity

Kestrel delegates HTTP framing to ASP.NET Core. The runtime must not implement a second HTTP parser.

FastCGI/CGI adapters normalize metadata once. Conflicting length/scheme/path metadata uses documented precedence and inconsistent impossible states are rejected rather than guessed.

FastCGI parser, if custom, requires fixed protocol version, strict record ordering, checked lengths, bounded accumulation and partial-read state machine.

### Response splitting

Controls:

- validate header name as HTTP token
- reject CR/LF/control characters in header/cookie names and values
- transport owns Content-Length/framing
- reject request-controlled raw status lines
- validate redirects

### Authentication and authorization bypass

Controls:

- route table is generated at compile time from explicit web metadata
- arbitrary functions are not routable by name alone
- HTTP method checked before route execution
- authentication checked before rules
- `[Rule:name]` requires rule, `[Rule:!name]` rejects holders of rule
- rule comparison is case-insensitive with normalized bounded strings
- principal is request-local
- session id rotates after privilege/authentication change

### Session fixation and session theft

Controls:

- cryptographically secure random session ids
- no predictable ids
- HttpOnly cookie
- Secure cookie on HTTPS
- explicit SameSite policy
- rotation API
- idle expiry
- bounded session values and total memory
- site-isolated store key
- no arbitrary CLR object serialization

### CSRF

The runtime must not imply that authentication alone prevents CSRF. Unsafe state-changing routes require an application or future runtime CSRF mechanism. Session cookie defaults should use an explicit SameSite policy. If a built-in anti-CSRF feature is added, tokens must be cryptographically random, session-bound and validated before route execution.

### XSS

`Response.Write` is raw output and does not auto-encode. HTML and URL encoding helpers are explicit. Documentation must not suggest that raw output is safe HTML.

### Compile/cache poisoning

Controls:

- cache key includes canonical site id/root source, Include graph hashes, compiler version, options and preprocessors
- cache is site-isolated
- source and dependencies are read only through validated project/root paths
- one compile task per cache key
- bounded failure backoff
- immutable compiled generations
- atomic publication only after successful compile/load

### Compile storms and resource exhaustion

Controls:

- single-flight compilation
- bounded compilation concurrency
- bounded cache entries/memory
- bounded negative cache
- bounded request body/header/query/form/multipart limits
- bounded session/application stores
- request cancellation
- Kestrel connection/time limits
- FastCGI bounded records/params/body
- graceful shutdown stops accepting new work before terminating in-flight requests

### Slow clients

Kestrel uses framework limits/timeouts. FastCGI transport applies read deadlines/cancellation and bounded partial-read state. CGI reads one bounded request body from stdin.

### Proxy spoofing

Forwarded headers are disabled unless explicit trusted proxies/networks are configured. Host filtering is enabled for externally exposed Kestrel hosts. Raw X-Forwarded-* values are never trusted merely because present.

### Source/compiler diagnostic leakage

Production response contains opaque error id only. Logs may contain normalized source file names and mapped lines but must apply existing compiler diagnostic redaction. Generated C#, temporary compilation paths, secrets and unrelated filesystem paths must not reach clients.

### Cross-request state leakage

Request/Response/context are request-local. AsyncLocal access, if used, must be set in a try/finally and cleared after execution. Application and Session stores contain only approved value types. Compiler-generated statics must not capture request objects.

### Cross-site leakage

Every cache/store/context identity includes a stable site id. No process-global dictionary may use only URL path, session id or source-relative path without site identity.

### Native and powerful language features

Trusted XPScript source may use powerful runtime features already permitted by the language, including file, process, HTTP and native interop features. Web hosting does not sandbox these features. Deployment documentation must make this trust boundary explicit.

For future untrusted hosting, execute scripts in separate least-privileged worker processes/containers with explicit filesystem, network, CPU, memory and process limits.

## Kestrel controls

Production Kestrel configuration requires explicit values for:

- bind endpoints
- allowed hosts
- maximum request body
- maximum concurrent connections/requests where supported
- request header timeout
- keep-alive timeout
- request-line/header limits where applicable
- supported protocols
- HTTPS or trusted reverse-proxy scheme
- trusted forwarded-header proxies/networks only

Loopback is the default bind for development.

## FastCGI controls

Before implementing a custom parser, repeat the maintained dependency review.

If custom parsing remains necessary:

- version must be FastCGI v1
- only required responder-role records accepted
- checked arithmetic for header/content/padding lengths
- no unchecked length-based allocation
- maximum record, params, name, value, header count and body sizes
- parser accepts arbitrary network fragmentation
- malformed/truncated ordering fails closed
- unknown management records handled according to protocol without entering request state
- fuzz corpus and deterministic regression vectors
- connection/request state removed on abort/error

## CGI controls

CGI environment variables are untrusted. CONTENT_LENGTH must parse as bounded non-negative decimal before stdin allocation/read. CGI stdout is reserved for response protocol bytes. Logs/errors use stderr.

## Security release gates

The web runtime cannot be called production-ready until automated tests cover:

- traversal encoding matrix
- symlink/reparse escapes
- source disclosure negatives
- response header/cookie CRLF injection
- route auth/method/rule enforcement
- Host and forwarded-header trust
- request/header/body limits
- session entropy/rotation/concurrency/isolation
- Application state site isolation
- compile single-flight/cache invalidation/site isolation
- compiler error redaction
- FastCGI malformed/partial/fuzz inputs if enabled
- CGI malformed environment/body lengths
- request-context isolation under concurrency
- graceful shutdown and cancellation

## Review result

Architecture is acceptable for implementation provided the initial trust boundary remains explicit: server-side XPScript source is trusted code. Kestrel must reuse ASP.NET Core. FastCGI must remain an adapter into the same runtime and must not introduce an unbounded or weakly validated parser. Production status remains blocked until the listed regression/security gates pass.