# Kestrel and browser hardening TODO

Branch purpose: track security findings and remediation work discovered by the dedicated Nuclei and raw HTTP hardening workflow.

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
- [ ] Review first successful workflow execution.
- [ ] Record all upstream Nuclei findings and classify each as confirmed, false positive, dependency issue, or not applicable.
- [ ] Add confirmed findings below with reproduction details.

## HTTP protocol and Kestrel boundary

- [~] Verify `Server` response header is never exposed.
- [~] Verify invalid Host headers return 400.
- [~] Verify duplicate Host headers are rejected.
- [~] Verify absolute-form request targets cannot bypass Host validation.
- [~] Verify TRACE is rejected and does not echo request data.
- [ ] Verify TRACK is rejected.
- [ ] Verify uncommon methods do not accidentally reach GET or POST handlers.
- [~] Verify request lines larger than the configured limit are rejected.
- [~] Verify request headers larger than the configured aggregate limit are rejected.
- [ ] Verify excessive header count behavior.
- [ ] Verify oversized cookie headers.
- [ ] Verify malformed header names.
- [ ] Verify control characters in header values are rejected.
- [ ] Verify bare LF request framing is rejected or safely normalized.
- [ ] Verify malformed HTTP version tokens.
- [~] Verify conflicting Content-Length values are rejected.
- [x] Fix Content-Length plus Transfer-Encoding conflicts being accepted by standalone Kestrel.
- [ ] Verify duplicate Transfer-Encoding values.
- [ ] Verify invalid chunk sizes.
- [ ] Verify chunk extensions and malformed chunk terminators.
- [ ] Add request smuggling regression probes for CL.TE, TE.CL and duplicate Content-Length variants.
- [ ] Verify HTTP/1.0 handling.
- [ ] Verify HTTP/2 behavior separately from HTTP/1.1.

## Request body and resource limits

- [ ] Verify request bodies above MaxRequestBodySize return 413.
- [ ] Verify chunked bodies above MaxRequestBodySize return 413.
- [ ] Verify in-memory request body handling never exceeds configured limits.
- [ ] Verify slow request body enforcement.
- [ ] Verify request headers timeout.
- [ ] Verify keep-alive timeout.
- [ ] Verify MaxConcurrentConnections.
- [ ] Add bounded concurrency stress test that is safe for CI.
- [ ] Keep destructive DoS templates out of normal branch CI.
- [ ] Create a separate opt-in stress profile for expensive resource exhaustion tests.

## Path traversal and static files

- [~] Verify `/assets/%2e%2e/...` traversal cannot escape the assets directory.
- [~] Verify double encoded traversal cannot escape the assets directory.
- [ ] Verify mixed slash traversal.
- [ ] Verify backslash traversal.
- [ ] Verify overlong and repeated dot segments.
- [ ] Verify encoded slash and encoded backslash behavior.
- [ ] Verify null byte variants.
- [ ] Verify Unicode separator and normalization variants.
- [ ] Verify Windows drive-style path input is rejected.
- [ ] Verify UNC-style path input is rejected.
- [~] Verify direct `.xps` source disclosure is impossible.
- [ ] Verify application log directories are never served through static files.
- [ ] Verify configuration files are never served through static files.
- [ ] Verify secrets files are never served through static files.
- [ ] Verify symlink inside `assets` cannot resolve outside the web root.
- [ ] Verify symlinked static files outside `assets` cannot escape the web root when static files are enabled.
- [ ] Verify directory symlinks and nested symlinks.
- [ ] Verify static file MIME allowlist cannot be bypassed.
- [ ] Verify `.xps` can never be added to the static MIME allowlist.
- [ ] Verify oversized static files are rejected.
- [ ] Verify static cache headers cannot be injected with CRLF.

## Host, proxy and forwarded headers

- [ ] Verify untrusted X-Forwarded-For is ignored when no known proxy is configured.
- [ ] Verify untrusted X-Forwarded-Proto is ignored when no known proxy is configured.
- [ ] Verify untrusted X-Forwarded-Host is ignored when no known proxy is configured.
- [ ] Verify only configured KnownProxies can influence forwarded headers.
- [ ] Verify ForwardLimit=1 prevents chained spoofing.
- [ ] Verify forwarded Host cannot bypass AllowedHosts.
- [ ] Verify IIS out-of-process mode preserves the external scheme correctly.
- [ ] Verify IIS out-of-process mode does not weaken Host validation unexpectedly.
- [ ] Add a Windows IIS hardening workflow after standalone Kestrel coverage stabilizes.

## Cookies and sessions

- [ ] Verify correlation cookie has HttpOnly.
- [ ] Verify correlation cookie uses Secure on HTTPS.
- [ ] Verify correlation cookie omits Secure on HTTP by design.
- [ ] Verify correlation cookie SameSite value.
- [ ] Verify correlation cookie lifetime.
- [ ] Verify raw correlation cookie value is never logged.
- [ ] Verify logs contain only the hashed session correlation identifier.
- [ ] Verify two clients receive independent correlation identifiers.
- [ ] Verify session fixation resistance.
- [ ] Verify malformed cookies do not break request isolation.
- [ ] Verify duplicate cookie name behavior.
- [ ] Verify oversized cookie input fails safely.
- [ ] Verify session cookies cannot be injected through response headers.
- [ ] Verify Secure session cookie configuration behaves correctly behind trusted reverse proxies.

## Security headers

- [~] Verify X-Content-Type-Options is present by default.
- [~] Verify X-Frame-Options is present by default.
- [~] Verify Referrer-Policy is present by default.
- [ ] Decide whether Content-Security-Policy should be provided by default for generated UI/browser applications.
- [ ] Decide whether Permissions-Policy should be provided by default.
- [ ] Verify application response headers cannot inject CRLF.
- [ ] Verify forbidden hop-by-hop headers cannot be configured as default security headers.
- [ ] Verify Server cannot be reintroduced through configurable default headers.

## Health and metrics

- [ ] Verify health endpoint is local-only by default.
- [ ] Verify metrics endpoint is local-only by default.
- [ ] Verify non-GET and non-HEAD methods receive 405.
- [ ] Verify operational endpoints do not leak secrets, tokens, paths, environment variables or source.
- [ ] Verify operational endpoints remain protected behind reverse proxy configurations.
- [ ] Test `--operational-external` separately and document the security implications.

## Routing and runtime behavior

- [ ] Verify malformed UTF-8 paths fail safely.
- [ ] Verify non-ASCII paths and query strings.
- [ ] Verify Unicode normalization does not create route aliases that bypass authorization.
- [ ] Verify encoded control characters in route paths.
- [ ] Verify query parser behavior with duplicate keys.
- [ ] Verify extreme query-string sizes.
- [ ] Verify form parser abuse cases.
- [ ] Verify JSON parser malformed and deeply nested input.
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
- [ ] Review upstream `http/exposures` findings.
- [ ] Add applicable generic fuzzing templates in a controlled phase.
- [ ] Add applicable generic vulnerability templates in a controlled phase.
- [ ] Add ASP.NET Core and Kestrel-specific CVE templates when relevant.
- [ ] Maintain an explicit allowlist for confirmed false positives.
- [ ] Never silently ignore a new high or critical finding.
- [ ] Store scanner revision, template revision and finding metadata in artifacts.
- [ ] Add a scheduled manual review process for updating pinned scanner and template versions.

## Confirmed findings

### CL.TE request framing accepted by standalone Kestrel

- Template or probe ID: `content-length-transfer-encoding-conflict-rejected`
- Severity: High
- Affected component: standalone XPScript Kestrel HTTP boundary
- Reproduction request: HTTP/1.1 POST with both `Content-Length: 4` and `Transfer-Encoding: chunked`, followed by a zero-length chunk
- Observed response: HTTP 200
- Expected response: HTTP 400 or connection rejection before application dispatch
- Root cause: ASP.NET Core exposed both framing headers to the XPScript middleware pipeline and the adapter did not explicitly fail closed before dispatch.
- Proposed fix: implemented an early Kestrel middleware guard that returns HTTP 400 and closes the connection when both Content-Length and Transfer-Encoding are present.
- Regression test: existing raw-socket probe is reproducible and currently fails. Keep it as the authoritative transport-level regression.
- Nuclei note: the custom `xpscript-cl-te-framing` template did not report this condition in the same run, so Nuclei is not currently reproducing the exact raw-socket framing behavior.
- Fix commit or pull request: `ca1971945059d15472b2f96264cbe0b2e8f92c11` on `ai-kestrel-nuclei-hardening`, pending CI verification



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
