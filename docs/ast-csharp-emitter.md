# Bound C# emitter

Phase 8 begins with an experimental method-body emitter. It consumes bound nodes and does not parse XPscript text. Production CLI and machine compilation continue using their existing shared compiler path.

`BoundExpressionEmitter` emits literal CLR types explicitly, escapes C# strings with Roslyn, and preserves Null as `System.DBNull.Value`. Empty and Nothing retain their CLR null representation. It emits bound conversions, using existing `XPScriptRuntime` conversion functions for Variant to String, Long, Double and Boolean. Other Variant targets are rejected until their lowering is implemented.

`BoundStatementEmitter` supports ordinary assignment, calls, return, If/ElseIf/Else, For, ForAll, While, pre-test/post-test Do While/Until and Select Case value, range and relational clauses. For loops use `XPScriptRuntime.Range`; ForAll uses `LSForAllRuntime.Enumerate`; Select Case uses the existing `LSCoreCompare` helpers. Variant conditions use the existing generated `XPScriptNullRuntime.ConditionValue` helper. Indexed property assignments are still pending lowering.

`BoundMethodEmitter` wraps an already emitted body in a deterministic static C# method declaration and emits typed parameters with `ref` for ByRef symbols. It is the first integration boundary for a future declaration and compilation-unit emitter; it does not parse names, infer types or select runtime helpers.

The caller currently supplies declarations, runtime helpers, entry points and the surrounding C# compilation unit. This emitter is not connected to production compilation. Bound nodes now retain syntax spans, and `EmitWithSourceMap` produces `#line` directives plus generated-to-source mapping records. Target-specific generation and complete runtime integration remain open.

`tests/ast-emission` checks selected generated C# snapshots and compiles and executes generated code with Roslyn. It verifies boxed numeric types, string escaping, Null/Empty, widening and nested control flow. The probe runs in the existing language FullTest runner and the temporary AST branch workflow. This branch currently keeps FullTest jobs in `.github/workflows/fulltest.yml`; the four separate workflow files described in repository guidance are not present here.

`tests/ast-compile-probe` now drives an XPscript `Sub Main` through `DeclarationParser`, `StatementBinder` and `BoundMethodEmitter`, then compiles and invokes the generated C# with Roslyn. It is the first executable end-to-end AST compilation path and runs before the language FullTest.
