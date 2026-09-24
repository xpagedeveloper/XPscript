---
name: xpscript-development
description: Develop, validate and compile XPScript using the official compiler machine interface.
---

# XPScript development

Use the XPScript compiler as the source of truth. Do not infer language validity from generated C# or from another language server.

## Use MCP during the edit loop

Prefer the XPScript MCP tools for fast, non-executing development feedback:

- Use `xpscript_validate` after creating or changing XPScript. It uses the reusable warm compiler and returns structured compiler diagnostics.
- Use `xpscript_symbols` to discover public XPScript APIs instead of guessing names.
- Use `xpscript_describe` before using an unfamiliar XPScript symbol or signature.
- Use `xpscript_explain` for stable XPS diagnostic codes when a diagnostic needs semantic explanation.
- Correct compiler diagnostics and validate again until validation succeeds.

MCP validation is intentionally not a replacement for final compilation. It does not publish an executable/package and must not be treated as proof that packaging, target publishing or deployment succeeds.

## Use the CLI for authoritative full builds

Run a full `xpscript compile` when the user asks to build/package/release, before claiming a change is fully buildable, when output artifacts are required, when target-specific publishing must be verified, or after an edit sequence before handing off a change whose acceptance criteria include compilation.

Use the target/runtime options required by the project. Treat the full compile result and exit code as authoritative for artifact generation. Do not execute the resulting program unless execution was requested or is required by an explicitly requested test.

## Recommended agent loop

1. Inspect existing XPScript and project conventions.
2. Use symbol/description tools when API knowledge is uncertain.
3. Edit the XPScript source.
4. Call `xpscript_validate` and repair structured diagnostics.
5. Repeat until validation returns `result: ok`.
6. If the task requires a complete build or build verification, run the normal XPScript CLI compile with the project's target/runtime settings.
7. Report validation and full-build status separately.

Never replace compiler validation with an LLM-only judgment.
