# XPScript Evaluate callvar and return semantics TODO

(c) xpagedeveloper.com 2026

This checklist extends the main runtime TODO with the parameter-passing and return-value contract for `Evaluate`.

Status:
- `[x]` implemented and verified
- `[>]` implemented/in progress, awaiting verification
- `[ ]` not implemented

Permanent runtime gate: `Evaluate Runtime Compatibility` compiles and executes the Evaluate regression corpus plus isolation-negative fixtures on GitHub Actions.

## Evaluate ownership and cleanup

- [x] `Evaluate` is implemented only by `XPScriptEvaluateRuntime`; the obsolete `System.Data.DataTable.Compute` evaluator has been physically removed from `ExtendedCompatibilityRuntimeSource.cs`
- [x] generated calls are owned by `XPScriptEvaluatePreprocessor` before extended compatibility rewriting
- [x] obsolete evaluator-specific terminology has been removed from the legacy runtime implementation
- [x] compiler build and generated-program runtime verification through `Evaluate Runtime Compatibility`

## Evaluate signature and input bridge

- [x] extend `Evaluate` with an optional second argument named `callvar`
- [x] supported surface: `Evaluate(sourceText)` and `Evaluate(sourceText, callvar)`
- [x] `callvar` is the only explicit caller-provided variable bridge into the isolated Evaluate scope
- [x] `callvar` is restricted/read-only inside Evaluate so evaluated code cannot overwrite the caller's variable; runtime-negative: `samples/evaluate-callvar-readonly-error.xps`
- [x] evaluated code has no implicit access to caller locals; runtime-negative: `samples/evaluate-scope-error.xps`
- [>] module globals, statics, compiler internals and unrelated state are not bridged by the evaluator implementation; dedicated adversarial coverage remains to be added
- [x] mutable XPScript arrays and Lists are defensive-copied before evaluation for the verified scalar/array/List and nested-collection paths
- [x] nested mutable arrays/Lists reachable from callvar are recursively snapshotted and arbitrary mutable object types are rejected instead of being shared into Evaluate; runtime coverage: `samples/evaluate-nested-collections.xps`, `samples/evaluate-object-callvar-rejection.xps`

## Scalar callvar

- [x] scalar and Variant-contained scalar values are exposed as `callvar` with their runtime type preserved where possible; source: `samples/evaluate-callvar.xps`

## Array callvar

- [x] XPScript arrays preserve rank, bounds and element type in the Evaluate snapshot for both one-dimensional zero-based arrays and multidimensional arrays with non-zero lower bounds; source: `samples/evaluate-multidimensional-callvar.xps`
- [x] evaluated code can read indexed values through normal `callvar(index...)` syntax, including multidimensional `callvar(i, j)` access
- [x] `LBound` and `UBound` are available inside Evaluate and preserve dimension-specific bounds; sources: `samples/evaluate-array-helpers.xps`, `samples/evaluate-nested-collections.xps`, `samples/evaluate-multidimensional-callvar.xps`
- [x] explicit multidimensional and non-zero-lower-bound regression source is permanently executed by `Evaluate Runtime Compatibility`

## List callvar

- [x] List input is defined as a named-parameter transport using `callvar("tag")`; source: `samples/evaluate-callvar.xps`
- [x] List input is copied into an evaluator-private read-only snapshot rather than sharing the caller's `LSList<T>` instance for the verified List path
- [x] List snapshotting uses the type-neutral `ILSList.SnapshotEntries()` contract rather than reflection
- [x] List values are recursively snapshotted, including nested XPScript arrays and nested Lists; source: `samples/evaluate-nested-collections.xps`
- [>] cyclic/shared collection graphs use reference-identity tracking so snapshots do not recurse indefinitely and shared references remain internally consistent; explicit cyclic/shared-identity runtime coverage remains open
- [x] returning the nested List fixture produces a normal value recognized by XPScript `IsList`
- [x] runtime verification of nested List/array combinations through `Evaluate Runtime Compatibility`

## Return semantics

- [x] `Return expression` immediately ends evaluation and becomes the return value from `Evaluate`; source: `samples/evaluate-xpscript.xps`
- [x] returned arrays are detached/snapshotted before leaving evaluator scope for the verified array paths
- [x] returned List snapshots are converted back into detached normal XPScript List values; the internal read-only snapshot type never escapes Evaluate
- [x] nested arrays/Lists in returned collections are recursively detached for the verified nested-collection path
- [x] `data = Evaluate(...)` receives the value from `Return`
- [x] reaching the end without `Return` yields `Nothing`/Empty (`null` internally) rather than the last expression value; source: `samples/evaluate-no-return.xps`
- [ ] distinguish `Return Nothing`, `Return Null`, and no `Return` after final XPScript Null/Nothing semantics are implemented

## Function coverage inside Evaluate

- [>] conversions: `CStr`, `CInt`, `CLng`, `CDbl`, `CSng`, `CCur`, `CByte`, `CBool`, `CDate`/`CDat`, `CVar`
- [>] inspection: `TypeName`, `DataType`, `IsArray`, `IsDate`, `IsEmpty`, `IsNull`, `IsObject`, `IsScalar`, `IsNumeric`, `LBound`, `UBound`
- [>] strings: `Len`, `Left`, `Right`, `Mid`, `LCase`, `UCase`, `Trim`, `LTrim`, `RTrim`, `FullTrim`, `StrReverse`, `Instr`, `Replace`, `Space`, `String`, `Chr`, `Asc`
- [>] math/number: `Abs`, `Int`, `Fix`, `Round`, `Sqr`, `Sgn`, `Sin`, `Cos`, `Tan`, `ATn`, `ATn2`, `ASin`, `ACos`, `Exp`, `Log`, `Fraction`, `Val`, `Str`, `Bin`, `Hex`, `Oct`
- [>] date/time: `Year`, `Month`, `Day`, `Hour`, `Minute`, `Second`, `DateValue`, `TimeValue`, `DateNumber`, `TimeNumber`, `DateAdd`, `DateDiff`, `DatePart`
- [x] representative String, math/number, Date and inspection functions are runtime-verified by `samples/evaluate-standard-functions.xps`
- [ ] continue broadening standard XPScript function coverage where functions remain side-effect free and isolation-safe

## Coercion and diagnostics alignment

- [x] dynamic `+` uses shared `XPScriptCoercion.AddVariant` for the verified String + scalar and scalar + numeric-String cases
- [x] String + scalar concatenation and scalar + numeric-String addition follow the shared forgiving XPScript coercion path; source: `samples/evaluate-coercion-diagnostics.xps`
- [x] comparison operators route through the main `LSCoreCompare.Rel` semantics for the verified Date comparison path
- [x] evaluator exceptions are normalized through the runtime error mapping for verified conversion and divide-by-zero paths
- [x] conversion/type mismatch maps to XPScript error 13
- [x] divide-by-zero maps to XPScript error 11
- [x] overflow maps to XPScript error 6; runtime assertion: `samples/evaluate-coercion-diagnostics.xps`
- [>] permission/access failures map to XPScript error 70; dedicated runtime assertion remains open
- [>] remaining evaluator/parser-specific failures map to XPScript error 5 with Evaluate context; representative arity/unknown-function failures are verified
- [x] known Evaluate functions distinguish invalid argument count from unavailable function names through `XPScriptEvaluateFunctionArityRuntime`
- [x] wrong-arity diagnostics report function name, accepted argument count/range and actual argument count; source: `samples/evaluate-function-arity-errors.xps`
- [x] unknown function names report `Function is not available inside Evaluate` rather than an arity error
- [ ] add broader parity checks against equivalent normal XPScript expressions for remaining coercion/error categories

## Isolation and security

- [x] no shared static callvar dictionary; every verified invocation owns its evaluator instance
- [x] `Dim callvar` is rejected by the evaluator; runtime-negative: `samples/evaluate-dim-callvar-error.xps`
- [x] assignment to `callvar` is rejected; source: `samples/evaluate-callvar-readonly-error.xps`
- [x] caller local variables remain inaccessible unless explicitly passed; source: `samples/evaluate-scope-error.xps`
- [x] arrays/Lists are defensive-copied before execution for the verified callvar/nested collection paths
- [x] arbitrary mutable objects are rejected rather than bridged by reference; runtime fixture: `samples/evaluate-object-callvar-rejection.xps`
- [x] collection nesting is capped at 64 levels to prevent unbounded recursive snapshot work; exact runtime boundary: `samples/evaluate-snapshot-depth-64.xps` accepted and `samples/evaluate-snapshot-depth-65.xps` rejected with XPScript error 5
- [x] collection snapshots enforce a total budget of 100000 collection elements by rejecting an over-budget fixture with controlled XPScript error 5
- [x] collection snapshots enforce a 16 MiB estimated payload budget by rejecting an over-budget fixture with controlled XPScript error 5; exact boundary source: `samples/evaluate-collection-payload-boundary.xps`
- [>] XPScript array element counts are checked before allocating the snapshot array and the exact 100000/100001 in-boundary/out-of-boundary pair is verified by `samples/evaluate-collection-element-boundary.xps`; equivalent exact CLR-array boundary coverage remains open
- [>] List entries are budgeted incrementally before copying and are not first materialized into an unbounded temporary array; dedicated stress verification remains open
- [x] budget violations produce controlled XPScript error 5 diagnostics instead of continuing snapshot allocation; sources: `samples/evaluate-collection-element-budget.xps`, `samples/evaluate-collection-payload-budget.xps`
- [x] exact budget boundary behavior is runtime-verified: 100000 elements accepted / 100001 rejected and 16777216 payload bytes accepted / 16777217 rejected; sources: `samples/evaluate-collection-element-boundary.xps`, `samples/evaluate-collection-payload-boundary.xps`
- [ ] ensure nested Evaluate invocations receive independent snapshots when nested Evaluate syntax is added
- [ ] add concurrent-thread isolation tests
- [x] exceptions crossing the verified Evaluate boundary are routed through `XPScriptEvaluateSemanticsRuntime.Sanitize`
- [x] type/conversion diagnostics use stable descriptions that do not echo secret input values for the verified sanitization path
- [>] only allowlisted structural parser/API diagnostics retain detail; broader adversarial coverage remains open
- [x] invalid numeric-literal diagnostics do not echo the literal text; runtime fixture: `samples/evaluate-diagnostic-edge-cases.xps`
- [x] retained structural diagnostics are length-limited to 512 characters plus ellipsis; runtime fixture: `samples/evaluate-diagnostic-edge-cases.xps`
- [x] secret callvar payload is absent from `Error$` in `samples/evaluate-diagnostic-sanitization.xps`
- [x] secret callvar payload is also rejected from compiler structured output and generated-process stdout/stderr before either stream is written to CI logs; permanent gate: `Evaluate Runtime Compatibility`

## Memory and lifetime

- [>] evaluator instance owns callvar snapshot; references become collectible after Evaluate returns/fails
- [x] returned arrays and Lists are detached from evaluator-owned storage for the verified collection paths
- [>] snapshot traversal tracks object identity only for the lifetime of one Evaluate snapshot operation; lifetime/stress coverage remains open
- [>] input and return snapshot budgets are invocation-local and are not stored in static/global state; concurrent verification remains open
- [ ] deterministic disposal rules if disposable/native-resource objects are ever allowed through callvar
- [ ] ensure no future evaluator cache stores arbitrary callvar data

## Documentation and examples

- [>] `Evaluate(sourceText)` and `Evaluate(sourceText, callvar)` documented in `docs/evaluate.md`
- [>] documentation examples intentionally reuse existing source fixtures under `samples/` rather than introducing unverified duplicate example programs
- [>] scalar, array, List, error, security and budget samples are linked directly from `docs/evaluate.md`
- [x] source regression coverage is executable under the permanent `Evaluate Runtime Compatibility` workflow
- [ ] add negative concurrency/isolation tests

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
