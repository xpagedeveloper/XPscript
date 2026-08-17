# XPScript web runtime execution model

(c) xpagedeveloper.com 2026

Status: Accepted for the initial production web runtime.

## Decision

Use in-process compiled execution for trusted XPScript web applications. Keep Request, Response, Server, Session, Application and Cookie transport-neutral so an isolated worker-process mode can be added later without changing the public web API.

Do not describe the initial runtime as a sandbox for untrusted code.

## Alternatives reviewed

### In-process compiled execution

Selected for the initial runtime.

Benefits:
- Lowest request overhead.
- Reuses the existing compiler/runtime pipeline.
- Straightforward request-local context and shared application/session services.
- Kestrel, FastCGI and CGI can use the same request handler.

Risks:
- Application code runs with host-process permissions.
- A script/runtime defect can affect the process.
- CPU and memory isolation are process-wide.

### Collectible AssemblyLoadContext

May be used internally for compiled-unit lifecycle and unloading.

It is not a security boundary and must never be presented as sandboxing.

### External worker process

Provides stronger failure and resource isolation and is the preferred basis for future less-trusted or multi-tenant execution.

It adds IPC, lifecycle, deployment and state-management complexity, so it is not required for the first trusted-code runtime.

### Hybrid model

Long-term preferred direction if different applications require different trust levels. Trusted applications may run in-process while less-trusted applications run in isolated workers.

## Trust boundary

Initial hosting assumes:
- XPScript source is trusted server-side application code.
- Administrators control source roots and runtime configuration.
- HTTP, FastCGI and CGI request data is always untrusted.
- Request, Response and principal state is request-local.
- Session and Application state is isolated per configured application/site.
- Compilation caches are isolated by canonical site/root identity.

## Shared execution pipeline

All transports use the same pipeline:

1. Normalize and validate the request.
2. Resolve the canonical source path inside the configured root.
3. Resolve Include dependencies.
4. Run configured preprocessors in deterministic order.
5. Parse to AST.
6. Compile using the shared compiler pipeline.
7. Create an immutable compiled unit.
8. Cache it with bounded, site-specific keys.
9. Execute using request-local XpsWebContext.
10. Translate XpsWebResponse back to the transport.

A transport adapter must never contain a separate XPScript parser/compiler.

## Runtime requirements

The initial runtime must maintain:
- Request-local Request and Response objects.
- Request-local principal and session binding.
- Site-local Application state.
- Site/root-specific compilation-cache keys.
- Bounded request bodies and compilation/cache growth.
- Client-disconnect cancellation propagation.
- Production errors without raw source, generated C# or filesystem-path disclosure.

Recompilation publishes a new immutable compiled unit atomically. In-flight requests may finish using the previous unit.

## Multi-tenant hosting

The initial in-process model is not sufficient isolation for unrelated untrusted customer code.

Future multi-tenant hosting must either explicitly share one trusted OS security boundary or use isolated workers or another reviewed isolation mechanism. Source roots, caches, sessions, Application state, temporary files and configuration remain tenant-isolated in every mode.

## Security work required before production release

- Path traversal and symlink/reparse-point regression tests.
- Host and forwarded-header trust tests.
- Body, header, timeout and slow-client boundary tests.
- Response-splitting tests.
- Session fixation and cookie-policy tests.
- Cache cross-site isolation tests.
- Compile-storm and resource-exhaustion tests.
- Malformed FastCGI framing tests when applicable.
- Production error-disclosure tests.

## Future worker contract

A future worker mode should use an XPScript-owned versioned protocol carrying normalized request and response data, not transport objects such as HttpContext. This preserves one public XPScript web object model across Kestrel, FastCGI and CGI.
