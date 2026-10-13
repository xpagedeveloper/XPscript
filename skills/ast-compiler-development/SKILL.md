---
name: ast-compiler-development
description: Extend XPScript AST compiler support for XP objects through the shared registry, binder, emitter and runtime contract.
---

# AST compiler development

Use this workflow when adding support for an XP object or AST-only language feature.

## Object support contract

1. Add the object name and detection rules to `AstObjectSupportRegistry`.
2. Define the shared semantic symbols and signatures in the binder catalog.
3. Bind declared members with stable XPScript types and source spans.
4. Emit calls and member access through existing runtime APIs; do not duplicate runtime behavior.
5. Add the runtime surface in its own source file when the object needs generated support.
   For a native source-defined object, see `AstImageSupport`: derive compiler metadata from the existing runtime, preserve native overloads and static/instance distinctions, and share native type mappings across ordinary/protected locals, parameters and Static storage. Test ownership, returned references (including Variant), ByVal/ByRef and exceptional cleanup as well as API calls.
   For a required NuGet addon, register its version and runtime-use detection in `ApplicationDependencyCatalog`. The shared SDK backend adds selected package references and native assets, and generates `Third-party-license.txt` from the restored graph. Do not add a separate package-copy or license implementation for each object; verify both use and absence through `tests/GeneratedDependencyProbe` and the permanent Runtime FullTest.
6. Keep unsupported-object diagnostics explicit until the complete runtime contract exists.

## Verification

- Add a focused AST source fixture for success and unsupported cases.
- Run AST CLI compilation and execute the generated program when applicable.
- Run MCP/CLI validation for the same source and compare structured diagnostics.
- Run the relevant compiler probe and a normal `xpscript compile`.
- Commit and push only after build and tests pass.

## Design rule

Object detection belongs in the registry. Binding, emission and runtime implementation remain separate layers so adding another XP object does not require expanding `AstExperimentalCompiler` with more object-specific regex logic.
