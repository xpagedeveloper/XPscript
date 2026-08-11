# XPScript Evaluate callvar and return semantics TODO

(c) xpagedeveloper.com 2026

This checklist extends the main runtime TODO with the parameter-passing and return-value contract for `Evaluate`.

Status:
- `[x]` implemented and verified
- `[>]` implemented/in progress, awaiting verification
- `[ ]` not implemented

## Evaluate signature and input bridge

- [>] extend `Evaluate` with an optional second argument named `callvar`
- [>] supported surface: `Evaluate(sourceText)` and `Evaluate(sourceText, callvar)`
- [>] `callvar` is the only explicit caller-provided variable bridge into the isolated Evaluate scope
- [>] `callvar` is restricted/read-only inside Evaluate so evaluated code cannot overwrite the caller's variable
- [>] evaluated code has no implicit access to caller locals, module globals, statics, compiler internals or unrelated variables
- [>] mutable XPScript arrays are defensive-copied before evaluation
- [ ] broaden defensive-copy support to all future mutable object/value types accepted by Evaluate

## Scalar callvar

- [>] scalar and Variant-contained scalar values are exposed as `callvar` with their runtime type preserved where possible

## Array callvar

- [>] XPScript arrays preserve rank, bounds and element type in the Evaluate snapshot
- [>] evaluated code can read indexed values through normal `callvar(index...)` syntax
- [>] `LBound` and `UBound` are available inside Evaluate
- [ ] add explicit multidimensional and non-zero-lower-bound regression sources when test execution is re-enabled

## List callvar

- [>] List input is defined as a named-parameter transport using `callvar("tag")`
- [>] source/runtime implementation must use an isolated snapshot/read-only representation
- [ ] add nested list/array regression coverage when execution is re-enabled

## Return semantics

- [>] `Return expression` immediately ends evaluation and becomes the return value from `Evaluate`
- [>] returned arrays are detached/snapshotted before leaving evaluator scope
- [>] `data = Evaluate(...)` receives the value from `Return`
- [>] reaching the end without `Return` now yields `Nothing`/Empty (`null` internally) rather than the last expression value
- [ ] distinguish `Return Nothing`, `Return Null`, and no `Return` after final XPScript Null/Nothing semantics are implemented
- [ ] detach/copy future mutable List/object return values where required

## Function coverage inside Evaluate

- [>] `TypeName`
- [>] `LBound`
- [>] `UBound`
- [>] scalar conversions, `Len`, `LCase`, `UCase`, `Trim`, `Abs`, `Round`
- [ ] continue broadening standard XPScript function coverage
- [ ] align every evaluator diagnostic/coercion edge case with the main compiler/runtime

## Isolation and security

- [>] no shared static callvar dictionary; every invocation owns its evaluator instance
- [>] `Dim callvar` is rejected
- [>] assignment to `callvar` is rejected
- [>] caller variables remain inaccessible unless explicitly passed
- [>] arrays are defensive-copied before execution
- [ ] ensure nested Evaluate invocations receive independent snapshots when nested Evaluate syntax is added
- [ ] add concurrent-thread isolation tests
- [ ] collection-size/depth limits for untrusted input
- [ ] sanitize future diagnostics so parameter values/secrets are not automatically echoed

## Memory and lifetime

- [>] evaluator instance owns callvar snapshot; references become collectible after Evaluate returns/fails
- [>] returned arrays are detached from evaluator-owned storage
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
