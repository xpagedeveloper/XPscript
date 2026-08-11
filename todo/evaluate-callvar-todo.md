# XPScript Evaluate callvar and return semantics TODO

(c) xpagedeveloper.com 2026

This checklist extends the main runtime TODO with the parameter-passing and return-value contract for `Evaluate`.

Status:
- `[x]` implemented and verified
- `[>]` implemented/in progress, awaiting verification
- `[ ]` not implemented

## Evaluate ownership and cleanup

- [>] `Evaluate` is implemented only by `XPScriptEvaluateRuntime`; the obsolete `System.Data.DataTable.Compute` evaluator has been physically removed from `ExtendedCompatibilityRuntimeSource.cs`
- [>] generated calls are expected to be owned by `XPScriptEvaluatePreprocessor` before extended compatibility rewriting
- [>] obsolete evaluator-specific terminology has been removed from the legacy runtime implementation
- [ ] runtime/build verification after execution is re-enabled

## Evaluate signature and input bridge

- [>] extend `Evaluate` with an optional second argument named `callvar`
- [>] supported surface: `Evaluate(sourceText)` and `Evaluate(sourceText, callvar)`
- [>] `callvar` is the only explicit caller-provided variable bridge into the isolated Evaluate scope
- [>] `callvar` is restricted/read-only inside Evaluate so evaluated code cannot overwrite the caller's variable
- [>] evaluated code has no implicit access to caller locals, module globals, statics, compiler internals or unrelated variables
- [>] mutable XPScript arrays and Lists are defensive-copied before evaluation
- [>] nested mutable arrays/Lists reachable from callvar are recursively snapshotted; arbitrary mutable object types are rejected instead of being shared into Evaluate

## Scalar callvar

- [>] scalar and Variant-contained scalar values are exposed as `callvar` with their runtime type preserved where possible

## Array callvar

- [>] XPScript arrays preserve rank, bounds and element type in the Evaluate snapshot
- [>] evaluated code can read indexed values through normal `callvar(index...)` syntax
- [>] `LBound` and `UBound` are available inside Evaluate
- [ ] add explicit multidimensional and non-zero-lower-bound regression sources when test execution is re-enabled

## List callvar

- [>] List input is defined as a named-parameter transport using `callvar("tag")`
- [>] List input is copied into an evaluator-private read-only snapshot rather than sharing the caller's `LSList<T>` instance
- [>] List snapshotting uses the type-neutral `ILSList.SnapshotEntries()` contract rather than reflection
- [>] List values are recursively snapshotted, including nested XPScript arrays and nested Lists
- [>] cyclic/shared collection graphs use reference-identity tracking so snapshots do not recurse indefinitely and shared references remain internally consistent
- [>] source: `samples/evaluate-nested-collections.xps`
- [ ] runtime verification of nested list/array combinations when execution is re-enabled

## Return semantics

- [>] `Return expression` immediately ends evaluation and becomes the return value from `Evaluate`
- [>] returned arrays are detached/snapshotted before leaving evaluator scope
- [>] returned List snapshots are converted back into detached normal `LSList<object?>` XPScript List values; the internal read-only snapshot type never escapes Evaluate
- [>] nested arrays/Lists in returned collections are recursively detached
- [>] `data = Evaluate(...)` receives the value from `Return`
- [>] reaching the end without `Return` now yields `Nothing`/Empty (`null` internally) rather than the last expression value
- [ ] distinguish `Return Nothing`, `Return Null`, and no `Return` after final XPScript Null/Nothing semantics are implemented

## Function coverage inside Evaluate

- [>] conversions: `CStr`, `CInt`, `CLng`, `CDbl`, `CSng`, `CCur`, `CByte`, `CBool`, `CDate`/`CDat`, `CVar`
- [>] inspection: `TypeName`, `DataType`, `IsArray`, `IsDate`, `IsEmpty`, `IsNull`, `IsObject`, `IsScalar`, `IsNumeric`, `LBound`, `UBound`
- [>] strings: `Len`, `Left`, `Right`, `Mid`, `LCase`, `UCase`, `Trim`, `LTrim`, `RTrim`, `FullTrim`, `StrReverse`, `Instr`, `Replace`, `Space`, `String`, `Chr`, `Asc`
- [>] math/number: `Abs`, `Int`, `Fix`, `Round`, `Sqr`, `Sgn`, `Sin`, `Cos`, `Tan`, `ATn`, `ATn2`, `ASin`, `ACos`, `Exp`, `Log`, `Fraction`, `Val`, `Str`, `Bin`, `Hex`, `Oct`
- [>] date/time: `Year`, `Month`, `Day`, `Hour`, `Minute`, `Second`, `DateValue`, `TimeValue`, `DateNumber`, `TimeNumber`, `DateAdd`, `DateDiff`, `DatePart`
- [>] source: `samples/evaluate-standard-functions.xps`
- [ ] continue broadening standard XPScript function coverage where functions remain side-effect free and isolation-safe

## Coercion and diagnostics alignment

- [>] dynamic `+` uses shared `XPScriptCoercion.AddVariant` instead of evaluator-only coercion logic
- [>] String + scalar concatenation and scalar + numeric-String addition follow the shared forgiving XPScript coercion path
- [>] comparison operators route through the main `LSCoreCompare.Rel` semantics, including numeric and Date comparison behavior
- [>] evaluator exceptions are normalized through the same runtime error mapping used by normal XPScript execution
- [>] conversion/type mismatch maps to XPScript error 13
- [>] divide-by-zero maps to XPScript error 11
- [>] overflow maps to XPScript error 6
- [>] permission/access failures map to XPScript error 70
- [>] remaining evaluator/parser-specific failures map to XPScript error 5 with Evaluate context
- [>] source: `samples/evaluate-coercion-diagnostics.xps`
- [ ] verify coercion/error parity against equivalent normal XPScript expressions when execution is re-enabled
- [ ] improve wrong-function-arity diagnostics so known functions distinguish invalid argument count from unavailable function names

## Isolation and security

- [>] no shared static callvar dictionary; every invocation owns its evaluator instance
- [>] `Dim callvar` is rejected
- [>] assignment to `callvar` is rejected
- [>] caller variables remain inaccessible unless explicitly passed
- [>] arrays/Lists are defensive-copied before execution
- [>] arbitrary mutable objects are rejected rather than bridged by reference
- [>] collection nesting is capped at 64 levels to prevent unbounded recursive snapshot work
- [ ] add a total collection element/byte budget for untrusted Evaluate inputs
- [ ] ensure nested Evaluate invocations receive independent snapshots when nested Evaluate syntax is added
- [ ] add concurrent-thread isolation tests
- [ ] sanitize future diagnostics so parameter values/secrets are not automatically echoed

## Memory and lifetime

- [>] evaluator instance owns callvar snapshot; references become collectible after Evaluate returns/fails
- [>] returned arrays and Lists are detached from evaluator-owned storage
- [>] snapshot traversal tracks object identity only for the lifetime of one Evaluate snapshot operation
- [ ] deterministic disposal rules if disposable/native-resource objects are ever allowed through callvar
- [ ] ensure no future evaluator cache stores arbitrary callvar data

## Documentation and examples

- [ ] document `Evaluate(sourceText)` and `Evaluate(sourceText, callvar)` under `docs/`
- [ ] reusable scalar/array/list examples under `examples/`
- [>] source regression coverage exists under `samples/`
- [ ] negative concurrency/isolation tests when execution is re-enabled

## Contract summary

```xpscript
' Scalar
result = Evaluate("Return callvar * 2", number)

' Positional array
values = Array(10, 20)
result = Evaluate("Return callvar(0) + callvar(1)", values)

' Named List
Dim parameters List As Variant
parameters("x") = 10
parameters("y") = 20
result = Evaluate("Return callvar(\"x\") + callvar(\"y\")", parameters)
```

`callvar` is the restricted input channel and `Return` is the explicit output channel. Evaluate never implicitly shares the caller's variable namespace.
