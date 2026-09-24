# XPScript Nuclei hardening coverage

This directory contains XPScript-specific negative security regression templates. A template should report a finding only when a security invariant is violated.

## Covered by custom Nuclei templates

- XPScript source disclosure
- static asset path traversal
- HTTP Server header disclosure
- standalone Kestrel Host allowlist bypass
- duplicate Host acceptance
- Content-Length plus Transfer-Encoding acceptance
- conflicting duplicate Content-Length acceptance
- client-controlled X-Request-Id trust
- correlation cookie missing HttpOnly or SameSite=Lax

## Covered upstream and not duplicated here

The pinned ProjectDiscovery corpus already contains generic checks for:

- missing HTTP security headers
- HTTP TRACE exposure
- generic Host header injection
- CL.TE and TE.CL request smuggling

The workflow runs selected upstream templates in addition to this directory.

## Keep as raw-socket or unit tests

These checks remain outside Nuclei because the existing test form is more precise or maintainable:

- oversized request line limits
- oversized aggregate request headers
- socket-close and parser edge behavior
- slow-body and timing-sensitive transport behavior
- internal response-header CRLF validation
- cookie value/path/domain validation
- transport-owned Content-Length protection
- session-id rotation and in-memory session semantics
- telemetry content and log correlation internals

Large protocol-boundary payloads should stay in the Python raw HTTP harness instead of embedding multi-kilobyte strings in YAML.

Internal API invariants should stay as unit/smoke tests unless a real HTTP route can naturally exercise the behavior.

## Candidate future templates

Add custom Nuclei templates when the hardening fixture exposes stable observable behavior for:

- untrusted X-Forwarded-For handling
- dotfile exposure
- unknown static extension exposure
- static file maximum-size fail-closed behavior
- session cookie flags over HTTP and HTTPS
- health and metrics external exposure when operationalExternal is disabled
- malformed and double-encoded Unicode/control-character paths
- browser-WASM cache or framework asset disclosure
- protected log-directory disclosure

Before adding a template, prefer an existing upstream ProjectDiscovery template when it tests the same invariant without losing XPScript-specific coverage.
