# AST test coverage map

The AST compiler is the production path for normal CLI and MCP compilation. The permanent Language FullTest combines focused parser/binder/emitter probes, bounded executable CLI shards, diagnostic checks and the `CompilerMcpProtocolProbe`. On 2026-11-27, the complete Language FullTest passed on Windows, Linux and macOS in GitHub Actions run `45956338657`.

## Covered by the permanent Language FullTest

- Lexer, syntax models, expression and statement parsing, binding, conversions, control flow, source spans and diagnostics.
- Module and class procedures, properties, constructors/destructors, inheritance, optional parameters, default ByRef, writable property/index copy-back and native reference lifecycles.
- Executable language/runtime fixtures split into six bounded AST CLI shards.
- Core control flow including `GoTo`, `GoSub`/`Return`, `On Error`, `Resume`, labels and lexical `With` scopes. The executable language suite includes `samples/core-language.xps`; focused GoSub cases are in `tests/ast-compile-probe`, and MCP validation/runtime diagnostics are asserted by `tests/CompilerMcpProtocolProbe/Program.cs`.
- The object-route registry, generated-source target matrix, UIList event dispatch, source mapping, CLI behavior and MCP protocol/diagnostic contracts.
- MCP `includeFiles` packages exercise ordinary `Include` resolution through all four source tools, nested virtual entry paths, repeated leading `../`, same-directory bare filenames, restricted request workspaces, rejected package traversal paths, and included-file diagnostic locations. `CompilerMcpProtocolProbe` runs in Language FullTest; absolute, parent-relative and bare-name Include runtime coverage runs in the AST CLI suite. The MCP probe also compiles and executes nested `GoSub` to guard source-mapped helper emission.

## Remaining compatibility work

- The Definition of Done remains open: audit every supported syntax and runtime behavior against the published legacy compiler, and verify consistent results from CLI and MCP. Passing the current FullTest proves the covered fixtures, not complete language compatibility.
- Expand parity fixtures where the current MCP probe validates source but does not compare executed behavior with the legacy compiler.
- Investigate XPImage end-to-end runtime behavior and lifecycle parity beyond dependency selection and current platform-specific coverage.
- Remove or explicitly justify each remaining legacy parser/transpiler route only after the relevant compatibility evidence is green.

## Platform FullTest coverage

- Browser-WASM `WebBrowserWasmBrowserSmoke` exercises the documented in-memory XPSpreadsheet byte roundtrip, verifies file-path operations are rejected at runtime, and checks UIForm/toast/button interaction in Chromium.
- Browser-WASM `WebBrowserWasmSmoke` exercises `[ServerSide]` Archive creation and download through the WASM bridge.
- Desktop `DesktopUIFormLifecycleSmoke --headless-ast-generated` exercises AST-generated blocking `UIForm.ShowDialog` through the desktop host. Platform FullTest runs this probe on Windows and Linux.
- `StateScopeObjectProbe` generates desktop source through `AstCompiler` and checks that web Application/Process state is shared at the intended scope while Session/Request state stays isolated across users, applications and requests. Platform FullTest runs it with bounded execution.

Use `todo/ast-compiler-migration-todo.md` as the authoritative migration gate and status ledger. A feature is complete only when its focused probe, permanent FullTest coverage and legacy-parity evidence all pass.
