# XPScript development guidelines

(c) xpagedeveloper.com 2026

These rules apply to future compiler, runtime, tooling, web and UI implementation work.

## Reuse before reimplementation

- Prefer existing .NET / BCL / ASP.NET Core functionality when it already solves the requirement safely and portably.
- Before implementing a substantial protocol, parser, codec, networking component, file-format handler, cryptographic helper, UI abstraction or similar infrastructure from scratch, investigate whether a mature NuGet package already provides the required functionality.
- Prefer a suitable established NuGet package over custom low-level code when it reduces implementation risk, security exposure and maintenance burden without compromising the XPScript public API.
- Do not add a dependency only because one exists. Review maintenance activity, supported .NET versions, Windows/Linux/macOS support where relevant, license compatibility, published security advisories/CVEs, dependency tree, API stability and project health.
- Keep third-party packages behind XPScript-owned interfaces where practical so implementation packages can be upgraded or replaced without breaking the language/runtime API.
- Pin/centrally manage package versions and update them intentionally.
- Include dependency vulnerability/update checks in CI or release maintenance where practical so adopted NuGet packages do not silently become stale security liabilities.
- Avoid abandoned or unmaintained packages for security-sensitive functionality.
- For security-critical parsers/protocols, prefer well-tested framework/package implementations over hand-written byte parsing when an appropriate implementation exists.
- If custom implementation is still required, document why an existing framework/NuGet option was unsuitable and add focused negative, fuzz/adversarial and boundary regression tests where applicable.

## Documentation of limits and restrictions

- Every public command, function, runtime object or compiler option with a fixed limit or important platform/security restriction must document that restriction close to the command in `docs/`.
- Numeric limits must state the actual value and unit, for example bytes, MiB, element count, dimensions, nesting depth, file-handle range or timeout units.
- Where a command has several related limits, include a clear `Limitations`/`Limits` subsection and repeat the most important limit directly under the affected command when practical.
- Document what happens when the limit is exceeded, including the XPScript error code when it is part of the public contract.
- Distinguish XPScript runtime limits from operating-system/framework limits and from application-level policy.
- Platform restrictions such as Windows-only behavior must be documented as limitations rather than left implicit in implementation code.
- Security/resource limits must not exist only in TODO files or source constants; user-facing documentation must be updated in the same feature work.
- When a limit changes, update its regression/boundary test and documentation in the same pull request whenever practical.

## Web-specific rule

- Kestrel mode must use ASP.NET Core/Kestrel rather than a custom HTTP server/parser.
- Before implementing FastCGI framing/parser code, investigate maintained NuGet libraries that support the required responder/server role, bounded parsing and the target platforms. Use one if it meets the security, licensing, maintenance and compatibility requirements.

## Verification

A TODO item is marked `[x]` only after its applicable build/runtime/security regression gates pass. If testing exposes an unrelated compiler/runtime/language defect, add that defect to the appropriate TODO rather than permanently working around it in the sample.
