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
- Avoid abandoned or unmaintained packages for security-sensitive functionality.
- For security-critical parsers/protocols, prefer well-tested framework/package implementations over hand-written byte parsing when an appropriate implementation exists.
- If custom implementation is still required, document why an existing framework/NuGet option was unsuitable and add focused negative, fuzz/adversarial and boundary regression tests where applicable.

## Web-specific rule

- Kestrel mode must use ASP.NET Core/Kestrel rather than a custom HTTP server/parser.
- Before implementing FastCGI framing/parser code, investigate maintained NuGet libraries that support the required responder/server role, bounded parsing and the target platforms. Use one if it meets the security, licensing, maintenance and compatibility requirements.

## Verification

A TODO item is marked `[x]` only after its applicable build/runtime/security regression gates pass. If testing exposes an unrelated compiler/runtime/language defect, add that defect to the appropriate TODO rather than permanently working around it in the sample.
