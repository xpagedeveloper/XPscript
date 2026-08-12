# XPScript Web Runtime / Server TODO

(c) xpagedeveloper.com 2026

> **Priority / sequencing:** This is a future major feature and must be implemented only after the existing compiler/language/runtime TODOs are complete and stable. Do not start implementation directly from this document. Perform a dedicated architecture/security review first, refine this specification, then create an implementation plan and regression matrix before writing production code.

> **Dependency policy:** Follow `todo/development-guidelines.md`. Prefer existing .NET/ASP.NET Core functionality and vetted, maintained NuGet packages where they safely satisfy requirements. In particular, investigate suitable maintained FastCGI packages before writing a custom protocol parser.

## Goal

Allow `.xps` files to run as server-side web applications in two hosting modes while sharing one XPScript web runtime:

1. **Standalone Kestrel mode** — XPScript starts a local ASP.NET Core/Kestrel web server.
2. **FastCGI mode** — XPScript runs as a FastCGI application server/worker that can be used behind nginx and other FastCGI-capable web servers, similar to PHP-FPM at the deployment level.

Both modes must expose the same XPScript web objects, request semantics, routing, runtime compilation, cache behavior, error handling and security model.

Initial conceptual command examples:

```text
xpscript web --root /srv/xpsite --port 8080
xpscript web --root C:\Sites\Example --port 8080

xpscript fastcgi --root /srv/xpsite --listen 127.0.0.1:9000
xpscript fastcgi --root /srv/xpsite --unix-socket /run/xpscript/site.sock
```

Exact public CLI syntax must be finalized during the architecture phase.

---

## 1. Architecture study required before implementation

- [ ] Review this entire design after the core XPScript TODO backlog is complete.
- [ ] Produce a separate architecture decision record comparing:
  - [ ] in-process compiled delegates
  - [ ] collectible `AssemblyLoadContext` loaded assemblies
  - [ ] external worker-process execution
  - [ ] hybrid trusted/untrusted execution modes
- [ ] Decide which execution model is safe enough for production web hosting.
- [ ] Define the trust boundary: XPScript application source should initially be treated as trusted server-side application code unless an isolated worker/sandbox model is explicitly implemented.
- [ ] Perform threat modeling before implementation: path traversal, request smuggling, response splitting, code injection, source disclosure, cache poisoning, session fixation, CSRF, XSS helper misuse, denial of service, oversized input, slow clients, malformed FastCGI records, symlink escapes, compile storms and resource exhaustion.
- [ ] Perform an OWASP-oriented security design review before production release.
- [ ] Define whether multi-tenant hosting is supported. If so, each site/tenant must have hard isolation boundaries for source root, cache, sessions, Application state, temp files and configuration.
- [ ] Do not expose an internet-facing administrative/compiler endpoint by default.

---

## 2. Common request pipeline

The target runtime compilation model is:

```text
HTTP / FastCGI request
        ↓
normalize + validate request
        ↓
resolve virtual path inside configured root
        ↓
resolve directory index (`index.xps`)
        ↓
resolve complete Include graph
        ↓
run configured source preprocessors in defined order
        ↓
parse
        ↓
AST
        ↓
compile
        ↓
.NET assembly / executable delegate
        ↓
bounded compilation cache
        ↓
create per-request web context
        ↓
execute XPScript entry point
        ↓
Response
```

- [ ] Integrate with `todo/include-source-files-todo.md`.
- [ ] Include expansion must finish before configurable source preprocessors run.
- [ ] Integrate with `todo/source-preprocessor-pipeline-todo.md`.
- [ ] Do not maintain a separate web-only parser/compiler implementation.
- [ ] Normal CLI compile, direct-script execution and web runtime should share as much compiler pipeline code as possible.
- [ ] Compiler diagnostics must retain original file/line mappings through Include and preprocessing.

---

## 3. URL-to-script routing

- [ ] `--root` defines the only default document/source root for the site.
- [ ] `/foo.xps` maps to `<root>/foo.xps` when allowed.
- [ ] `/folder/` maps to `<root>/folder/index.xps` by default.
- [ ] `/` maps to `<root>/index.xps`.
- [ ] Make the default document name configurable later, while keeping `index.xps` as the standard default.
- [ ] Define behavior for `/folder` versus `/folder/` and redirects consistently.
- [ ] Return 404 when the resolved XPScript file does not exist.
- [ ] Never return raw `.xps` source to the browser merely because compilation failed.
- [ ] Decide separately whether static files are served by XPScript or should normally be served by nginx/Kestrel static-file middleware.
- [ ] If static file serving is added, create a separate allowlist/configuration and MIME mapping policy.

### Path security

- [ ] Percent-decode and normalize request paths using one well-defined canonicalization procedure.
- [ ] Reject malformed encodings and ambiguous path representations.
- [ ] Reject `..` traversal and any normalized path that escapes the configured root.
- [ ] Resolve full/canonical filesystem paths before access.
- [ ] Account for Windows drive/UNC semantics and Linux/macOS filesystem semantics.
- [ ] Define and test symlink/reparse-point behavior so a file below the apparent root cannot resolve outside the allowed root unless explicitly configured.
- [ ] Never concatenate untrusted URL paths directly into filesystem paths.
- [ ] Do not allow request-controlled compiler output paths.

---

## 4. Standalone Kestrel mode

Target command concept:

```text
xpscript web --root <directory> --port <port>
```

- [ ] Start ASP.NET Core/Kestrel using .NET 10-compatible hosting APIs.
- [ ] `--port` configures the listening port.
- [ ] Define default bind address; safest development default should be loopback unless an external bind is explicitly requested.
- [ ] Support explicit bind address/interface configuration.
- [ ] Support HTTPS endpoints/configuration in production mode.
- [ ] Support graceful shutdown.
- [ ] Define maximum concurrent connections and requests.
- [ ] Configure bounded request-body size.
- [ ] Configure request-header size/time limits.
- [ ] Configure keep-alive/header timeouts.
- [ ] Configure minimum data-rate or equivalent slow-client protections where supported.
- [ ] Support request cancellation when the client disconnects.
- [ ] Define HTTP/1.1, HTTP/2 and optionally HTTP/3 support as separate compatibility targets.
- [ ] When behind a reverse proxy, trust forwarded headers only from explicitly configured trusted proxies/networks.
- [ ] Never blindly trust arbitrary `X-Forwarded-For`, `X-Forwarded-Proto` or `X-Forwarded-Host` values.
- [ ] Validate Host values against configured hosts when exposed beyond loopback.

---

## 5. FastCGI hosting mode

FastCGI must be a distinct transport adapter using the same internal XPScript web request context as Kestrel.

- [ ] Implement FastCGI responder role required for normal web requests.
- [ ] Support TCP listener such as `127.0.0.1:9000`.
- [ ] Support Unix-domain socket on Linux/macOS where appropriate.
- [ ] Investigate Windows FastCGI transport/deployment requirements separately.
- [ ] Support nginx `fastcgi_pass` deployment.
- [ ] Correctly consume standard CGI/FastCGI parameters such as:
  - [ ] `SCRIPT_FILENAME`
  - [ ] `SCRIPT_NAME`
  - [ ] `PATH_INFO`
  - [ ] `QUERY_STRING`
  - [ ] `REQUEST_METHOD`
  - [ ] `CONTENT_TYPE`
  - [ ] `CONTENT_LENGTH`
  - [ ] `SERVER_NAME`
  - [ ] `SERVER_PORT`
  - [ ] `SERVER_PROTOCOL`
  - [ ] `REMOTE_ADDR`
  - [ ] HTTPS/scheme information
  - [ ] HTTP request headers
- [ ] Define canonical precedence when proxy/FastCGI variables disagree.
- [ ] Do not trust a client-derived `SCRIPT_FILENAME` until it has been canonicalized and checked against the configured XPScript root.
- [ ] Support FastCGI keep-connection semantics only after protocol handling is robust.
- [ ] Correctly return status, headers and response body using FastCGI records.

### FastCGI parser safety

- [ ] Implement protocol parsing with explicit fixed-width integer decoding and strict bounds checks.
- [ ] Avoid `unsafe` code and unmanaged pointer arithmetic unless a later security-reviewed implementation absolutely requires it.
- [ ] Never allocate directly from an untrusted declared length without configured upper bounds.
- [ ] Validate every record type, version, request id, content length and padding length before consuming buffers.
- [ ] Reject truncated, overlapping, malformed or unexpectedly ordered records cleanly.
- [ ] Bound accumulated PARAMS size, header count, header/value length and request body size.
- [ ] Prevent integer overflow when adding lengths or calculating buffer offsets.
- [ ] Prefer `Span<T>`/`ReadOnlySpan<T>` and checked arithmetic where useful, with explicit range validation before slicing.
- [ ] Fuzz the FastCGI parser with malformed records before production release.
- [ ] Add regression tests for partial network reads; never assume one socket read contains one complete FastCGI record.

---

## 6. XPScript web objects

The first public web object model should contain:

```text
Request
Response
Server
Session
Application
Cookie
```

The exact API must be frozen only after examples and compatibility tests have been written.

### 6.1 Request

Candidate read-only/request-scoped members:

- [ ] `Request.Method`
- [ ] `Request.Path`
- [ ] `Request.PathInfo`
- [ ] `Request.QueryString`
- [ ] query-value access API
- [ ] `Request.Headers`
- [ ] `Request.ContentType`
- [ ] `Request.ContentLength`
- [ ] `Request.Body`
- [ ] bounded body text reading
- [ ] bounded binary body reading
- [ ] `Request.Host`
- [ ] `Request.Scheme`
- [ ] `Request.RemoteAddress`
- [ ] `Request.Protocol`
- [ ] `Request.Cookies`
- [ ] form-urlencoded parsing
- [ ] multipart/form-data parsing only with strict limits and safe temporary-file handling
- [ ] uploaded-file abstraction if multipart upload is implemented
- [ ] request cancellation/disconnect state where useful

Security requirements:

- [ ] Treat every Request value as untrusted input.
- [ ] Preserve multiple header/query values rather than silently joining values where doing so changes semantics.
- [ ] Apply configurable limits for header count, query length, form fields and body size.
- [ ] Do not automatically deserialize arbitrary request bodies into executable/runtime types.

### 6.2 Response

Candidate members:

- [ ] `Response.StatusCode`
- [ ] `Response.ContentType`
- [ ] `Response.Headers`
- [ ] `Response.Cookies`
- [ ] `Response.Write(value)`
- [ ] `Response.WriteBinary(value)` if required
- [ ] `Response.Redirect(url [, status])`
- [ ] `Response.Clear()` semantics
- [ ] `Response.Flush()` semantics only if transport-safe streaming is intentionally supported
- [ ] response-completed state

Security requirements:

- [ ] Reject CR/LF injection in response header names and values.
- [ ] Validate header names using HTTP token rules.
- [ ] Prevent conflicting/unsafe `Content-Length` handling.
- [ ] Avoid exposing transport-specific hop-by-hop headers directly unless explicitly supported.
- [ ] Provide HTML encoding helpers separately; `Response.Write` must not misleadingly claim to make arbitrary text safe HTML.

### 6.3 Server

Candidate mostly read-only members:

- [ ] configured root path
- [ ] current hosting mode (`Kestrel` / `FastCGI`)
- [ ] server address/port where meaningful
- [ ] server start time
- [ ] runtime/compiler version
- [ ] safe path-mapping helper that cannot escape root
- [ ] URL/HTML encoding helpers if appropriate

Do not expose arbitrary process-control or unrestricted filesystem escape helpers through `Server` by default.

### 6.4 Session

- [ ] Define opt-in session support; do not require sessions for every request.
- [ ] Generate session identifiers using a cryptographically secure random generator.
- [ ] Session id must have enough entropy to prevent guessing.
- [ ] Store session id in a configurable cookie.
- [ ] Default cookie should support `HttpOnly`, `Secure` when HTTPS is active, and an explicit SameSite policy.
- [ ] Provide session id rotation to mitigate session fixation after authentication/privilege changes.
- [ ] `Session.Get`, `Set`, `Remove`, `Clear`, `Abandon` or equivalent API.
- [ ] Define timeout/idle expiration.
- [ ] Initial single-server store may be in-memory, but store interface must allow later distributed implementations.
- [ ] Never use unsynchronized mutable global dictionaries for concurrent session access.
- [ ] Define locking/version semantics for two simultaneous requests using the same session.
- [ ] Bound per-session data size and total session-memory use.
- [ ] Do not serialize arbitrary CLR objects from untrusted session input.

### 6.5 Application

Application is shared state for one configured site/application, not global state shared across unrelated sites.

- [ ] `Application.Get`, `Set`, `Remove`, `Clear` or equivalent API.
- [ ] Define thread-safe/concurrent semantics.
- [ ] Provide atomic operations or explicit locking API only if necessary and carefully designed.
- [ ] Isolate Application state by site/root/application id.
- [ ] Bound memory usage.
- [ ] Define lifecycle during config reload/server restart.
- [ ] Do not place Request/Response/context objects in Application state.

### 6.6 Cookie

- [ ] cookie name/value
- [ ] Path
- [ ] Domain with validation
- [ ] Expires / MaxAge
- [ ] Secure
- [ ] HttpOnly
- [ ] SameSite
- [ ] deletion semantics
- [ ] reject control characters and invalid cookie names/values
- [ ] do not permit response-splitting through cookies

---

## 7. Script entry point and execution context

- [ ] Decide whether a web `.xps` file executes top-level code, `Sub Main()`, a dedicated `Sub WebMain()`, or another explicit convention.
- [ ] Prefer one deterministic convention and document it clearly.
- [ ] Inject/access Request/Response/Server/Session/Application through runtime context, not uncontrolled global mutable statics.
- [ ] Context must be request-local, including async/thread transitions if async execution is later supported.
- [ ] Do not allow one request to observe another request's Request/Response objects.
- [ ] Ensure compiler-generated statics do not accidentally turn request locals into cross-request global data.
- [ ] Define behavior when script returns without writing a response.
- [ ] Define behavior for uncaught XPScript runtime errors.
- [ ] Production error pages must not expose source code, stack traces, filesystem paths, secrets or generated C#.
- [ ] Development diagnostics may expose richer information only when explicitly enabled and never by default on public interfaces.

---

## 8. Runtime compilation and cache

Target model:

```text
index.xps
   ↓
Include expansion
   ↓
configured preprocessors
   ↓
parse
   ↓
AST
   ↓
compile
   ↓
.NET assembly / delegate
   ↓
cache
   ↓
execute
```

### Cache key

- [ ] Cache by canonical root/source identity, not only URL text.
- [ ] Include compiler version in cache identity.
- [ ] Include target/runtime/code-generation options in cache identity.
- [ ] Include configured preprocessor identities + versions + ordering in cache identity.
- [ ] Include the root source and every included source dependency in invalidation/hash calculation.
- [ ] Include relevant project/reference/native dependency configuration.
- [ ] Prevent one site/tenant from receiving another site's cached executable.

### Invalidation

- [ ] Invalidate when root `.xps` changes.
- [ ] Invalidate when any included `.xps` changes.
- [ ] Invalidate when a referenced managed/native dependency changes where relevant.
- [ ] Invalidate when compiler/preprocessor configuration changes.
- [ ] Make invalidation race-safe while requests are running.
- [ ] Existing in-flight requests may finish on an immutable old compiled unit while new requests switch atomically to the new unit.

### Compile-storm protection

- [ ] Only one compilation for the same cache key/version may run at a time (`single-flight` behavior).
- [ ] Concurrent requests for a cold/stale script should wait on/share that compilation rather than compile the same source N times.
- [ ] Bound total concurrent compilations globally and per site.
- [ ] Apply compile timeout/cancellation semantics.
- [ ] A failed compilation must not replace a previously valid cached version unless explicitly configured.
- [ ] Decide whether production can optionally keep serving last-known-good code after a new source revision fails compilation.

### Cache resource limits

- [ ] Bounded number/size of compiled entries.
- [ ] LRU/TTL or equivalent eviction strategy.
- [ ] No unbounded dictionary keyed by arbitrary URLs/query strings.
- [ ] Cache key must exclude request query/body data unless code generation genuinely depends on it (normally it must not).
- [ ] Expose cache metrics: hit, miss, compile count, compile duration, eviction, failure.

### Assembly lifetime

- [ ] Investigate collectible `AssemblyLoadContext` if generated assemblies are loaded dynamically.
- [ ] Verify old compiled revisions can actually be unloaded and do not leak delegates, static events, threads or runtime contexts.
- [ ] Stress-test repeated edit/recompile cycles for memory growth.
- [ ] Do not call `GC.Collect()` as normal cache management behavior.

---

## 9. Execution isolation and denial-of-service boundaries

- [ ] Decide whether web scripts execute in-process or in worker processes.
- [ ] Explicitly document that in-process XPScript has the privileges of the hosting process.
- [ ] If untrusted/customer-supplied scripts are ever supported, require process/container/OS-level isolation rather than claiming managed code alone is a sandbox.
- [ ] Bound request execution time where possible.
- [ ] Because arbitrary synchronous managed code cannot be safely force-aborted in-process, investigate worker-process isolation for hard execution deadlines.
- [ ] Bound stdout/log output and Response size where appropriate.
- [ ] Bound file upload sizes and temporary storage.
- [ ] Bound HTTP client usage from scripts to reduce SSRF/resource abuse if hosting untrusted code is ever contemplated.
- [ ] Apply server-level rate limiting/concurrency controls.
- [ ] Protect compilation endpoints/cache misses from intentional compile storms.

---

## 10. Memory/buffer safety coding rules

Even though the implementation is primarily managed .NET, all parsers and network-facing code must be written as if input is hostile.

- [ ] No `unsafe` blocks, raw pointers or manual unmanaged buffers in protocol/request parsing without a separately reviewed justification.
- [ ] Never trust network-provided lengths, offsets, counts or indexes.
- [ ] Use checked integer arithmetic where lengths/offsets are combined.
- [ ] Validate ranges before slicing arrays, spans or buffers.
- [ ] Bound all request, header, FastCGI PARAMS, body, upload, response and compiler-input sizes.
- [ ] Handle partial reads/writes correctly.
- [ ] Do not allocate an attacker-specified size before validating it against configured limits.
- [ ] Prefer pooled buffers only when lifetime/clearing rules are correct; secrets must not leak between requests through reused buffers.
- [ ] Return pooled buffers in `finally` paths.
- [ ] Fuzz network/protocol parsers.
- [ ] Add malformed-input tests designed to trigger integer overflow, out-of-range slicing, excessive allocation and parser state confusion.

---

## 11. HTTP security requirements

- [ ] Reject request-header injection/invalid control characters.
- [ ] Prevent response splitting.
- [ ] Define duplicate `Content-Length` / `Transfer-Encoding` handling through the hosting transport rather than implementing ambiguous custom parsing.
- [ ] Do not reimplement Kestrel's HTTP parser in standalone mode.
- [ ] Validate trusted-proxy configuration before using forwarded client information.
- [ ] Host allowlist support.
- [ ] Security headers documentation and configurable defaults.
- [ ] Cookie security defaults.
- [ ] CSRF guidance/helpers for state-changing browser applications.
- [ ] HTML/URL/JSON encoding helpers that make output context explicit.
- [ ] Do not automatically disable TLS certificate validation for script HTTP clients.
- [ ] Secret values must never be printed in normal compile/runtime diagnostics.

---

## 12. Concurrency model

- [ ] Multiple requests must execute concurrently.
- [ ] Request/Response/Cookie context is per request.
- [ ] Session state follows explicit concurrency semantics.
- [ ] Application state is thread-safe.
- [ ] Compilation cache is thread-safe.
- [ ] Compile invalidation cannot dispose/unload code still executing in another request.
- [ ] File watchers/cache invalidators must handle duplicate/coalesced filesystem events.
- [ ] Avoid static mutable state that crosses unrelated web applications.
- [ ] Stress-test hundreds/thousands of concurrent requests and simultaneous source updates.

---

## 13. Configuration

Future configuration needs evaluation for:

- [ ] root directory
- [ ] port / bind address
- [ ] Kestrel vs FastCGI mode
- [ ] FastCGI TCP address / Unix socket
- [ ] allowed hostnames
- [ ] HTTPS certificate configuration
- [ ] trusted proxies
- [ ] request/header/body limits
- [ ] execution timeout policy
- [ ] compile concurrency
- [ ] compile cache limits/TTL
- [ ] session enable/disable and timeout
- [ ] environment (`Development` / `Production`)
- [ ] static-file behavior
- [ ] default document (`index.xps`)
- [ ] Include roots/security policy
- [ ] ordered preprocessor chain
- [ ] logging level

Configuration precedence (CLI/config/env) must be explicitly defined rather than accidental.

---

## 14. Logging and observability

- [ ] Structured request logs without logging secrets by default.
- [ ] Correlation/request id.
- [ ] Compile diagnostics correlated to source revision/cache key.
- [ ] Metrics for request count/duration/status codes.
- [ ] Metrics for active requests/connections.
- [ ] Cache hit/miss/compile metrics.
- [ ] Session-store metrics without exposing session ids.
- [ ] FastCGI protocol errors.
- [ ] Avoid logging Authorization, Cookie, Set-Cookie, passwords, tokens or full sensitive request bodies by default.

---

## 15. Development workflow / hot reload behavior

- [ ] Source changes should invalidate only affected script dependency graphs.
- [ ] Next request compiles changed code once and atomically publishes it to cache after success.
- [ ] Define optional eager/precompile mode for production startup/deployment.
- [ ] Provide a command to precompile/validate all reachable `.xps` files before deployment.
- [ ] Compile errors should have a production-safe HTTP response while retaining full diagnostics in server logs.
- [ ] Development mode may display source diagnostics only after explicit opt-in.

---

## 16. Example XPScript API — design sketch only

This is not final syntax; it exists to drive the API design review.

```vb
Sub Main()
    Response.ContentType = "text/html; charset=utf-8"

    Dim name As String
    name = Request.Query("name")

    If name = "" Then
        name = "World"
    End If

    Response.Write("<h1>Hello " & HtmlEncode(name) & "</h1>")
End Sub
```

Session concept:

```vb
Dim count As Integer
count = CInt(Session.Get("count"))
count = count + 1
Call Session.Set("count", count)
Response.Write("Visits: " & CStr(count))
```

Cookie concept:

```vb
Dim cookie As New Cookie
cookie.Name = "theme"
cookie.Value = "dark"
cookie.HttpOnly = True
cookie.Secure = True
cookie.SameSite = "Lax"
Call Response.Cookies.Add(cookie)
```

All examples must be reconsidered after the object API has been finalized.

---

## 17. Kestrel regression matrix

- [ ] root `/` executes `index.xps`
- [ ] directory `/folder/` executes `/folder/index.xps`
- [ ] direct `.xps` route
- [ ] missing script -> 404 without source disclosure
- [ ] GET/query values
- [ ] POST body
- [ ] headers/cookies
- [ ] response status/headers/body
- [ ] sessions
- [ ] Application concurrency
- [ ] source edit invalidates cache
- [ ] included-file edit invalidates parent script cache
- [ ] preprocessor version/order invalidates cache
- [ ] simultaneous cold requests compile once
- [ ] compile failure does not poison unrelated cache entries
- [ ] traversal/encoded traversal rejected
- [ ] symlink escape rejected according to policy
- [ ] Host validation
- [ ] trusted/untrusted forwarded header behavior
- [ ] oversized body rejected
- [ ] slow/aborted request behavior
- [ ] Windows/Linux/macOS verification

---

## 18. FastCGI regression matrix

- [ ] nginx -> XPScript FastCGI GET
- [ ] POST body
- [ ] query string
- [ ] request headers
- [ ] cookies
- [ ] status/content-type/custom response headers
- [ ] `index.xps` mapping
- [ ] TCP transport
- [ ] Unix socket transport on supported OSes
- [ ] keep-connection behavior
- [ ] partial record reads
- [ ] multiple PARAMS records
- [ ] multiple STDIN records
- [ ] empty STDIN terminator
- [ ] malformed record version/type
- [ ] invalid length/padding
- [ ] oversized PARAMS/body rejected before dangerous allocation
- [ ] invalid/malicious `SCRIPT_FILENAME` cannot escape root
- [ ] interrupted client/request cleanup
- [ ] fuzz corpus regression

---

## 19. Performance acceptance criteria to define before implementation

- [ ] Measure cold compile latency separately from cached-request latency.
- [ ] Cached execution must not invoke the compiler again when source/dependencies are unchanged.
- [ ] Benchmark cache hit throughput.
- [ ] Benchmark simultaneous requests to one cached script.
- [ ] Benchmark many independent scripts.
- [ ] Benchmark source-change/recompile behavior under load.
- [ ] Measure memory after thousands of recompiles to detect assembly/cache leaks.
- [ ] Measure session/Application memory limits.
- [ ] Define production SLO/targets only after measured baselines exist.

---

## 20. Documentation required before production-ready status

- [ ] architecture and trust model
- [ ] Kestrel installation/startup
- [ ] nginx FastCGI configuration example
- [ ] HTTPS/reverse-proxy configuration
- [ ] root/index routing
- [ ] Request API
- [ ] Response API
- [ ] Server API
- [ ] Session API
- [ ] Application API
- [ ] Cookie API
- [ ] runtime compilation/cache behavior
- [ ] Include/preprocessor interaction
- [ ] deployment/precompile workflow
- [ ] secure configuration checklist
- [ ] limits/timeouts
- [ ] diagnostics/logging
- [ ] migration/versioning rules for future web-runtime API changes

---

## Research notes informing the later design

- ASP.NET Core/Kestrel provides configurable connection/request limits, body-size limits, keep-alive/header timeouts and endpoint binding. Use those mechanisms rather than building a custom HTTP parser for standalone mode.
- Kestrel can bind TCP endpoints and Unix-domain sockets. For nginx deployments using HTTP reverse proxying, a Unix socket is also a possible transport, but this is separate from true FastCGI support.
- nginx FastCGI supports upstreams over TCP or Unix-domain sockets and supplies script/request information through FastCGI parameters such as `SCRIPT_FILENAME`, `QUERY_STRING`, `REQUEST_METHOD`, `CONTENT_TYPE` and `CONTENT_LENGTH`.
- Reverse-proxy client/scheme/host information is security-sensitive. Forwarded headers must only be trusted from explicitly configured proxy addresses/networks.

These notes are design inputs, not implementation approval. Re-check current .NET/nginx documentation when this TODO becomes active.
