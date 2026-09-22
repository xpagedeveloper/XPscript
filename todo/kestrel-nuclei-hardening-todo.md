# Kestrel and browser hardening TODO

Branch purpose: track security findings and remediation work discovered by the dedicated Nuclei and raw HTTP hardening workflow.

## Cross-host remediation rule

Every security finding discovered through the Kestrel hardening campaign must be assessed and remediated across Kestrel, CGI and FastCGI. A finding is not considered complete until the equivalent attack has a regression test for all applicable hosting modes, or the TODO documents why a transport is technically not applicable. Prefer fixes in the shared XPScript.Web.Runtime layer when the security invariant is transport-independent. Keep adapter-specific validation when the raw protocol/request representation differs.

The executable test harness lives on branch `ai-kestrel-nuclei-hardening`. This branch is intentionally separate so findings and remediation planning can evolve independently from the scanner implementation.

## Status legend

- [ ] Not started
- [~] Needs verification
- [x] Verified complete

## Scanner and CI baseline

- [x] Create a dedicated Ubuntu GitHub Actions workflow for Kestrel hardening.
- [x] Restrict automatic execution to branch `ai-kestrel-nuclei-hardening`.
- [x] Keep manual `workflow_dispatch` support on that branch.
- [x] Build XPScript compiler and CLI before testing.
- [x] Generate a temporary XPScript web application in CI.
- [x] Compile the generated application as a WebIIS package for compiler coverage.
- [x] Start the same generated application with standalone XPScript Kestrel.
- [x] Pin Nuclei version.
- [x] Pin nuclei-templates commit.
- [x] Add XPScript-specific Nuclei templates.
- [x] Add raw TCP HTTP probes for cases that normal HTTP clients may normalize.
- [x] Upload hardening result artifacts with 3 day retention.
- [x] Review first successful workflow execution. Run #30 completed successfully with Kestrel, CGI and FastCGI regression gates.
- [x] Verify real nginx to XPScript FastCGI topology. Run #38 completed successfully while retaining strict duplicate FastCGI parameter rejection.
- [x] Verify advanced Kestrel traversal corpus end-to-end. Run #38 completed successfully.
- [ ] Record all upstream Nuclei findings and classify each as confirmed, false positive, dependency issue, or not applicable.
- [ ] Add confirmed findings below with reproduction details.

## HTTP protocol and Kestrel boundary

- [x] Verify `Server` response header is never exposed.
- [x] Verify invalid Host headers return 400.
- [x] Verify duplicate Host headers are rejected.
- [x] Verify absolute-form request targets cannot bypass Host validation. Invalid absolute-form host rejected in run #39.
- [x] Verify TRACE is rejected and does not echo request data.
- [x] Verify TRACK is rejected. Verified in run #32.
- [x] Verify uncommon methods do not accidentally reach GET or POST handlers. Verified 405 without application-handler response in run #39.
- [x] Verify request lines larger than the configured limit are rejected.
- [x] Verify request headers larger than the configured aggregate limit are rejected.
- [x] Verify excessive header count behavior. Verified in run #32.
- [x] Verify oversized cookie headers. Verified in run #32.
- [x] Verify malformed header names. Verified in run #32.
- [x] Verify control characters in header values are rejected. Verified in run #32.
- [x] Verify bare LF request framing is rejected or safely normalized. Kestrel accepts and canonicalizes bare-LF framing before XPScript middleware. Regression requires exactly one response and no protected-content disclosure. Corrected canonicalization probe verified in run #40.
- [x] Verify malformed HTTP version tokens. Verified in run #32.
- [x] Verify conflicting Content-Length values are rejected.
- [~] Verify Content-Length plus Transfer-Encoding canonicalization cannot create request smuggling across supported deployment topologies. Standalone Kestrel CL.TE desync probe verified safe in run #31. Reverse-proxy topologies remain.
- [x] Verify duplicate Transfer-Encoding values. Verified safe in run #31.
- [x] Verify invalid chunk sizes. Verified rejected in run #31.
- [x] Verify chunk extensions and malformed chunk terminators. Malformed terminator rejected in run #31 and chunk extensions handled without ambiguous framing in run #39.
- [x] Add request smuggling regression probes for CL.TE, TE.CL and duplicate Content-Length variants. CL.TE verified in run #31, TE.CL and parser variants verified in run #32. nginx to FastCGI topology gate verified in run #38.
- [x] Verify HTTP/1.0 handling. Verified safe in run #39.
- [ ] Verify HTTP/2 behavior separately from HTTP/1.1.

## Request body and resource limits

- [x] Verify request bodies above MaxRequestBodySize return 413. Oversized Content-Length request verified with 413 in run #42.
- [x] Verify chunked bodies above MaxRequestBodySize return 413. Complete 1,048,577-byte chunked request verified in run #44.
- [x] Verify in-memory request body handling never exceeds configured limits. Unknown-length chunked request exceeding the 64-byte in-memory limit verified with 413 in run #58.
- [x] Verify slow request body enforcement. Minimum request-body data-rate enforcement verified with a stalled raw socket body in run #66.
- [x] Verify request headers timeout. Partial-header socket regression verified in run #60.
- [x] Verify keep-alive timeout. Idle persistent-connection regression verified in run #60.
- [x] Verify MaxConcurrentConnections. A two-slot socket regression verified that a third active request is not processed while both configured connection slots are occupied in run #62.
- [x] Add bounded concurrency stress test that is safe for CI. 24 requests with concurrency bounded to 8 verified in run #64.
- [x] Keep destructive DoS templates out of normal branch CI. The normal upstream Nuclei profile explicitly excludes `dos`, `fuzz`, and `intrusive` tags.
- [x] Create a separate opt-in stress profile for expensive resource exhaustion tests. Added manual-only `kestrel-stress-hardening.yml`; the stress profile raises the bounded concurrency regression to 256 requests with at most 32 client operations in flight.

## Path traversal and static files

- [x] Verify `/assets/%2e%2e/...` traversal cannot escape the assets directory.
- [x] Verify double encoded traversal cannot escape the assets directory.
- [x] Verify mixed slash traversal.
- [x] Verify backslash traversal.
- [x] Verify overlong and repeated dot segments. Advanced traversal corpus verified in run #38.
- [x] Verify encoded slash and encoded backslash behavior.
- [x] Verify null byte variants. Encoded and double-encoded null suffix probes verified in run #38.
- [x] Verify Unicode separator and normalization variants. Full-width dot, Unicode division-slash and overlong UTF-8 separator probes verified in run #38.
- [x] Verify Windows drive-style path input is rejected. Verified in run #38.
- [x] Verify UNC-style path input is rejected. Verified in run #38.
- [x] Verify direct `.xps` source disclosure is impossible.
- [x] Verify application log directories are never served through static files. Runtime rejects log directories inside the web root and external-log reachability regressions are verified in run #80.
- [x] Verify configuration files are never served through static files. Direct `/config.json` disclosure is rejected in run #72.
- [x] Verify secrets files are never served through static files. Direct `/secret.txt` disclosure is rejected in run #72.
- [x] Verify symlink inside `assets` cannot resolve outside the web root. File symlink escape regression verified in run #76.
- [x] Verify symlinked static files outside `assets` cannot escape the web root when static files are enabled. Canonical root enforcement and file symlink regression verified in run #76.
- [x] Verify directory symlinks and nested symlinks. Direct directory symlink escape verified in run #76 and nested directory symlink escape verified in run #78.
- [x] Verify static file MIME allowlist cannot be bypassed. Encoded extension, double-encoded, hidden-path, extra-path and `.xps` case variants verified in run #78.
- [x] Verify `.xps` can never be added to the static MIME allowlist. Option validation regression verified in run #74.
- [x] Verify oversized static files are rejected. A file exceeding `MaxStaticFileBytes` is rejected in run #72.
- [x] Verify static cache headers cannot be injected with CRLF. `StaticCacheControl` validation regression verified in run #74.

## Host, proxy and forwarded headers

- [x] Verify untrusted X-Forwarded-For is ignored when no known proxy is configured. Runtime regression verified in run #45.
- [x] Verify untrusted X-Forwarded-Proto is ignored when no known proxy is configured. Runtime regression verified in run #45.
- [x] Verify untrusted X-Forwarded-Host is ignored when no known proxy is configured. Runtime regression verified in run #45.
- [x] Verify only configured KnownProxies can influence forwarded headers. Trusted-proxy runtime regression verified in run #48.
- [x] Verify ForwardLimit=1 prevents chained spoofing. Multi-hop forwarded-header regression verified in run #48.
- [x] Verify forwarded Host cannot bypass AllowedHosts. Trusted forwarded host outside the allowlist returned 400 in run #48.
- [x] Verify IIS out-of-process mode preserves the external scheme correctly. IIS loopback forwarding preserves external HTTPS as Request.Scheme=https; verified in run #54.
- [x] Verify IIS out-of-process mode does not weaken Host validation unexpectedly. Host allowlist is enforced in IIS out-of-process mode and invalid Host returned 400 in run #51.
- [x] Add a Windows IIS hardening workflow after standalone Kestrel coverage stabilizes. Windows WebIIS compile/deployment-contract gate verified in run #56.

## Cookies and sessions

- [x] Verify correlation cookie has HttpOnly. Kestrel, CGI and FastCGI regressions verified through run #88.
- [x] Verify correlation cookie uses Secure on HTTPS. FastCGI HTTPS regression verified in run #88; Kestrel trusted-proxy HTTPS remains covered separately below.
- [x] Verify correlation cookie omits Secure on HTTP by design. Kestrel, CGI and FastCGI regressions verified through run #88.
- [x] Verify correlation cookie SameSite value. SameSite=Lax verified across Kestrel, CGI and FastCGI through run #88.
- [x] Verify correlation cookie lifetime. 30-day Max-Age verified across Kestrel, CGI and FastCGI through run #88.
- [x] Verify raw correlation cookie value is never logged. Shared runtime regression verified in run #93.
- [x] Verify logs contain only the hashed session correlation identifier. Shared runtime regression verified in run #93.
- [x] Verify two clients receive independent correlation identifiers. Shared runtime regression verified in run #93.
- [x] Verify session fixation resistance. Correlation cookies are server-authenticated with HMAC and forged client values are rotated; shared runtime regression verified in run #93.
- [x] Verify malformed cookies do not break request isolation. Shared runtime regression verified in run #100.
- [x] Verify duplicate cookie name behavior. CGI and FastCGI deterministically retain the first duplicate cookie value; verified in run #103.
- [x] Verify oversized cookie input fails safely. 16 KiB malformed correlation-cookie input is safely rotated; verified in run #100.
- [x] Verify session cookies cannot be injected through response headers. Shared response validation rejects CR/LF/NUL header injection and invalid cookie names/values; verified in run #105.
- [x] Verify Secure session cookie configuration behaves correctly behind trusted reverse proxies. Trusted KnownProxy and simulated IIS out-of-process forwarded HTTPS both emit Secure correlation cookies; verified in run #107.

## Security headers

- [x] Verify X-Content-Type-Options is present by default.
- [x] Verify X-Frame-Options is present by default.
- [x] Verify Referrer-Policy is present by default.
- [x] Provide Content-Security-Policy by default for generated UI/browser applications. Shared runtime HTML defaults and the Browser-WASM-specific policy are covered by regression tests; verified in run #126.
- [x] Provide Permissions-Policy by default. Camera, microphone and geolocation are disabled unless an application explicitly overrides the header; shared runtime regression verified in run #126.
- [x] Verify application response headers cannot inject CRLF. Shared response validation and configurable default-header validation reject CR/LF; verified through run #109.
- [x] Verify forbidden hop-by-hop headers cannot be configured as default security headers. Content-Length, Transfer-Encoding, Connection, Keep-Alive and Upgrade are rejected; verified in run #109.
- [x] Verify Server cannot be reintroduced through configurable default headers. Server is rejected from DefaultSecurityHeaders; verified in run #109.

## Health and metrics

- [x] Verify health endpoint is local-only by default. Local access remains available while trusted-proxy requests with a non-loopback effective client are hidden with 404; verified in run #113.
- [x] Verify metrics endpoint is local-only by default. Local access remains available while trusted-proxy requests with a non-loopback effective client are hidden with 404; verified in run #113.
- [x] Verify non-GET and non-HEAD methods receive 405. POST is rejected with 405 and Allow: GET, HEAD while HEAD returns no body; verified through run #111.
- [x] Verify operational endpoints do not leak secrets, tokens, paths, environment variables or source. Health and metrics payload regressions reject authorization/bearer/cookie data, common secret/environment markers, stack traces, source locations and filesystem paths; covered by the Kestrel hardening suite through run #126.
- [x] Verify operational endpoints remain protected behind reverse proxy configurations. Trusted forwarded client addresses are evaluated after proxy processing and external clients receive 404; verified in run #113.
- [x] Test `--operational-external` separately and document the security implications. Explicit external mode (`OperationalEndpointsLocalOnly = false`) is opt-in and exposes bounded health/metrics payloads to external clients, including through trusted proxies; payload leakage guards remain enforced. Prefer the default local-only mode or `OperationalAllowedNetworks` CIDR allowlists when external monitoring access is required; covered by the Kestrel hardening suite through run #126.

## Routing and runtime behavior

- [x] Add explicit JSON parser hardening route to the generated test application so malformed, deeply nested, duplicate-key and oversized JSON can be tested at the XPScript API layer. Real Kestrel HTTP probes verified in run #139.
- [x] Verify malformed JSON fails without stack traces or filesystem paths. REST integration regression returns 400 and rejects stack trace/source-path leakage; real Kestrel HTTP regression also verified in run #139.
- [x] Verify deeply nested JSON is bounded. REST integration regression rejects JSON deeper than the parser limit with 400; real Kestrel HTTP regression also verified in run #139.
- [x] Verify oversized JSON is bounded by MaxRequestBodySize. REST request-body parsing rejects payloads above the configured 4 MiB JSON limit; real Kestrel HTTP regression accepts bounded 400/413 or an early transport close and verifies the host remains healthy; verified in run #139.
- [x] Verify duplicate JSON property behavior is deterministic and documented. System.Text.Json last-value-wins behavior is asserted through the XPScript REST binding pipeline and real Kestrel HTTP route; verified in run #139.

- [ ] Verify malformed UTF-8 paths fail safely.
- [ ] Verify non-ASCII paths and query strings.
- [ ] Verify Unicode normalization does not create route aliases that bypass authorization.
- [x] Verify encoded control characters in route paths. Encoded NUL path rejected before application handler in run #42.
- [ ] Verify query parser behavior with duplicate keys.
- [x] Verify extreme query-string sizes. Oversized query/request-line probe verified with 414 in run #42.
- [ ] Verify form parser abuse cases.
- [x] Verify JSON parser malformed and deeply nested input. Covered through the REST integration pipeline and real Kestrel HTTP route; verified in run #139. CGI/FastCGI adapter equivalence still requires explicit cross-host regression or documented N/A before the broader cross-host hardening item is considered complete.
- [ ] Verify multipart parser malformed boundaries and oversized fields.
- [ ] Verify unexpected methods cannot reach a route with the wrong method attribute.
- [ ] Verify error responses never expose stack traces.
- [ ] Verify compilation/runtime errors never disclose filesystem paths in production responses.
- [ ] Verify response splitting attempts are rejected.
- [ ] Verify open redirect behavior where application APIs construct redirects.
- [ ] Verify reflected input examples are HTML encoded where required.
- [ ] Add XSS probes against generated browser/UIForm output where user input is reflected.

## Browser and WASM hardening

- [ ] Add a test application that exercises browser WebAssembly HTTP calls.
- [ ] Verify CSRF retry token handling.
- [ ] Verify CSRF token cannot be reused across unrelated sessions.
- [ ] Verify CSRF token length and malformed token handling.
- [ ] Verify browser-generated requests do not leak bearer tokens to unintended origins.
- [ ] Verify credential storage and cookie behavior across HTTP and HTTPS.
- [ ] Verify browser routes and server routes enforce the same authorization policy.

## Nuclei corpus management

- [ ] Review upstream `http/misconfiguration` findings.
- [x] Add explicit generic web-boundary Nuclei profile for CRLF injection, PUT enablement, TRACE, Host-header injection, web.config, Git metadata/credentials and .DS_Store exposure.
- [x] Add curated ASP.NET, IIS and .NET Nuclei profile for debug mode, ASP.NET Core development environment, launchSettings.json, ELMAH, Trace.axd, Microsoft runtime errors, NuGet.config, IIS short-name behavior and IIS version disclosure.
- [x] Add curated JSON security Nuclei profile for appsettings.json, credentials.json, auth.json, JWK/JWKS exposure, Swagger/OpenAPI exposure and generic sensitive config JSON disclosure.
- [ ] Review upstream `http/exposures` findings.
- [ ] Add applicable generic fuzzing templates in a controlled phase.
- [ ] Add applicable generic vulnerability templates in a controlled phase.
- [ ] Add ASP.NET Core and Kestrel-specific CVE templates when relevant.
- [ ] Maintain an explicit allowlist for confirmed false positives.
- [ ] Never silently ignore a new high or critical finding.
- [ ] Store scanner revision, template revision and finding metadata in artifacts.
- [ ] Add a scheduled manual review process for updating pinned scanner and template versions.

## Confirmed findings

### Encoded traversal can reach normalized operational routes

- Template or probe ID: `operational-route-traversal-not-reachable`
- Severity: High
- Affected component: standalone XPScript Kestrel path boundary
- Reproduction request: `GET /assets/%2e%2e/_xps/metrics`
- Observed response: HTTP 200 with metrics body
- Expected response: HTTP 400/404 without reaching the operational route
- Root cause: Kestrel normalizes encoded dot segments before XPScript route middleware evaluates `Request.Path`.
- Proposed fix: inspect `IHttpRequestFeature.RawTarget` before route dispatch and reject encoded dot-segment traversal, including double encoding.
- Regression test: raw HTTP probe plus `xpscript-protected-path-traversal` Nuclei template.
- Fix commits: Kestrel `4c5805f250c18023621575c9c0c85745972baf22`, CGI `3dcb69703389fdb1809d5c370cc4b97211aff2a8`, FastCGI `4d5086da58d39794e64e4bde59903ce810a79e93`, with regression coverage in `e2610d8f98981ea89dbd0ce9656b9694b256e38a` and `ede77748e950642ecd2a78360faf80bbd8691ffd`. Verified by successful hardening run #30.


### CL.TE request framing accepted and canonicalized by standalone Kestrel

- Template or probe ID: `content-length-transfer-encoding-conflict-rejected`
- Severity: Needs topology-specific verification
- Affected component: standalone XPScript Kestrel HTTP boundary
- Reproduction request: HTTP/1.1 POST with both `Content-Length: 4` and `Transfer-Encoding: chunked`, followed by a zero-length chunk
- Observed response: HTTP 200
- Expected response: either rejection or unambiguous single-request canonicalization with no cross-request desynchronization
- Root cause: Kestrel accepts the request and canonicalizes framing before the XPScript middleware layer. The original Content-Length header is not available to the adapter after parsing, so application middleware cannot reliably reject the raw CL+TE combination.
- Proposed fix: test for actual desynchronization/smuggling rather than status-code rejection. Verify standalone Kestrel and IIS-to-Kestrel separately. Keep raw framing tests authoritative.
- Regression test: raw-socket probe now accepts Kestrel canonicalization only when it produces one response and no protected-content disclosure. Add a dedicated multi-request desynchronization probe next.
- Nuclei note: the custom `xpscript-cl-te-framing` template did not report this condition in the same run, so Nuclei is not currently reproducing the exact raw-socket framing behavior.
- Fix commit or pull request: superseded by topology-specific verification. The attempted middleware rejection could not observe the raw Content-Length after Kestrel parsing.



For each finding record:

- Template or probe ID
- Severity
- Affected component
- Reproduction request
- Observed response
- Expected response
- Root cause
- Proposed fix
- Regression test
- Commit or pull request fixing it
