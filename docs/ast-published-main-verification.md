# AST verification against published main

The published main binary is a compatibility baseline alongside the branch's legacy compiler and experimental AST compiler. Do not rebuild or replace it while collecting comparison results.

## Baseline recorded on 2026-10-08

- Executable: `publish/xpscript/win-x64/xpscript.exe` in the original checkout.
- Reported version: `0.9.3 Beta`, build `2026-10-08`.
- SHA-256: `DDDB44347DD55800E38F158988A671F053CB45A4C02CF1F712D69870E637FA27`.
- Fixture: `tests/ast-compile-probe/optional-byref-compatibility.xps`.

| Compiler | Compilation | Execution |
| --- | --- | --- |
| Published main baseline | Succeeded | Failed: `'long' does not contain a definition for 'Value'` |
| Corrected branch legacy compiler | Succeeded | `OPTIONAL_BYREF_COMPATIBILITY_OK` |
| Experimental AST compiler | Succeeded | `OPTIONAL_BYREF_COMPATIBILITY_OK` |

The fixture exercises supplied ByRef aliases, repeated omitted numeric defaults, mutation of an explicit optional argument, and string/Boolean defaults. The branch fixes a baseline defect by allocating fresh scalar reference storage for literal defaults and preserving it through generic ByRef lowering. MCP validation accepts the same fixture; MCP validation alone does not prove runtime execution.

Compile the fixture with the preserved published executable using `compile <fixture> -o <baseline-output> --runtime=false --single-file=false`, then execute the generated output and record its exit status and text. The branch's `tests/ast-compile-probe/run-ast-cli.ps1` executes the same fixture through legacy and AST paths before broader regressions. Repeat this comparison for subsequent migrated behavior and record the published executable's hash whenever the baseline changes.

These results verify this fixture only. Full Optional semantics, shared AST machine integration and the broader migration remain open in `todo/ast-compiler-migration-todo.md`.

## Explicit Optional slots

`tests/ast-compile-probe/optional-omitted-slots.xps` passed compilation and execution against the preserved published main binary, producing `OPTIONAL_OMITTED_SLOTS_OK`. Experimental AST execution also passed. The fixture covers leading, middle, trailing and all-empty positions, omitted trailing arguments, and a required parameter following an Optional parameter. Required positions still need supplied arguments; focused AST binder tests retain XPS2004 and the full call span for invalid omissions, argument types, arity and ByRef modes.

## Default evaluation order

The optional-default-order.xps fixture matches published main with the trace EVAL_FIRST, EVAL_LAST, EVAL_BODY, 123; EVAL_LAST, EVAL_BODY, 943; EVAL_FIRST, EVAL_BODY, 128. Before correction AST produced 193 for the first case. Default evaluators now run in argument position, preserving side effects before later supplied expressions. Main accepts executable function-call defaults, including nested omitted defaults.

Saved follow-up fixtures: optional-nested-supplied-call.xps passes branch legacy/AST but published main still fails compilation. optional-bare-procedure-argument.xps is rejected by both published main and the branch AST/MCP surfaces with XPS2003; the branch no longer falls through to InvalidCastException.
The baseline SHA-256 was rechecked during this audit and remains unchanged. Production MCP validation accepts the default-order fixture; AST machine exposure remains pending.

## Nested supplied Optional arguments

Published main still rejects the expanded optional-nested-supplied-call.xps fixture with missing-argument and invalid closing-parenthesis errors. Token-based compatibility preprocessing replaces the flat regex; branch legacy and AST cover nested calls to the same Optional procedure, inner argument commas and multiple calls on a line. Expected output is 123, 1333, 246 and the unchanged string Choose(, Provided(),).

The saved print-parentheses-literal.xps follow-up originally exposed an AST-only mismatch: the file-output regex captured a comma inside the literal and routed it to the AstPrintFile placeholder. The baseline hash remains unchanged.

The print-parentheses-literal.xps follow-up is fixed: published main and AST both print Choose(, Provided(),). The AST file-output rewrite now uses lexer tokens and no longer treats literal commas as file syntax.

The reserved-compiler-identifier.xps fixture now produces the same reserved-identifier rejection through AST CLI and MCP as the legacy rule for `__xps` user names. The published executable remains unchanged.

For `unsupported-goto.xps`, published main compiles and runs with output `done`. AST now emits real GoTo transfers. GoSub has been excluded from AST support by user decision; see below.

This GoTo comparison was repeated after removing GoSub: both preserved published main (framework-dependent output) and AST compiled and executed the fixture with exit code 0 and output `done`.

The broader audit identified additional AST-only semantic loss: Resume/On Error and implicit member statements are rewritten to no-op calls, while XPImage operations are replaced with object construction. These are recorded as separate open parity tasks rather than treated as equivalent behavior.

The original `gosub-parity-audit.xps` comparison produced `worker` then `after` with published main and only `after` with AST. AST now explicitly rejects GoSub instead of silently discarding it.

## Nested GoSub continuations

During the 2026-10-08 investigation, a temporary six-call GoSub fixture (ElseIf, While, post-test Do and Select, followed by a nested GoTo) failed compilation with the preserved published executable: CS0159 reported missing `__ls_gosub_return_*` labels and CS0163 reported case fall-through. Flat AST branches passed that fixture and an expanded ten-call version. The user subsequently chose to remove GoSub support and use Sub/Function calls instead. The final implementation removes the experimental GoSub stack and continuations, explicitly rejects GoSub and retains flat branches for procedure-scoped GoTo. The temporary GoSub execution fixture was replaced by a permanent rejection fixture. The published executable's SHA-256 remains unchanged.

## Static procedure-local lifetime

`static-local-baseline.xps` calls the same Sub twice. Published main compiles and executes it with output `1`, `2`; AST previously produced `1`, `1` because preprocessing changed Static to Dim. Structural scalar Static binding and persistent fields now produce `1`, `2` with exit code 0. The expanded `static-local-lifetime.xps` also passes AST execution with independent procedures/overloads, string/Boolean defaults and ByRef mutation. Published main rejects that expanded fixture's numeric overload calls with XPS2003 (it selects the String overload); this limits the baseline comparison to the minimal fixture, not the expanded overload regression. Static collections, objects and explicit initialization remain open.

## ForAll List aliases

`forall-list-alias.xps` now compiles and executes with matching output in published main and AST: `a`, `b`, `11`, `12`, `one:BLUE`, `two:GREEN`, `a`, `a`, `b`, `b`, `12`, `13`, `5`, `1`, `2`, `2`. It exercises typed write-through aliases, ListTag, nested aliases and insertion during snapshot iteration. Before this fix AST passed KeyValuePair values into arithmetic and threw a RuntimeBinderException.

A separate ByRef alias audit remains open: calling a Long ByRef Sub with the alias is rejected by AST-generated C# because Value is a property. Published main compiles that audit but prints the unchanged value `1`, losing the intended mutation to `11`. This is distinct from assignment directly to a List alias, which matches the baseline regression above. Published main also rejects reuse of the same alias name in nested loops; the permanent comparison fixture uses different names.
