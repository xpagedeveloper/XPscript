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
- [>] `callvar` is restricted/read-only inside Evaluate; direct assignment and redeclaration are rejected
- [>] evaluated code has no implicit access to caller locals, module globals, statics, compiler internals, runtime internals or unrelated variables
- [>] mutable arrays/lists passed through `callvar` are snapshotted before evaluation; caller-owned collection storage is not directly exposed
- [ ] decide final policy for arbitrary mutable object references supplied through `callvar`; current implementation treats explicitly supplied non-collection objects as opaque references

## Scalar callvar

- [>] a single scalar value is exposed inside Evaluate as `callvar` with its runtime XPScript type preserved where possible
- [>] String, Boolean, Byte, Integer, Long, Single, Double, Currency and Date scalar values are supported by the current snapshot model
- [>] source: `samples/evaluate-callvar.xps`

```xpscript
Dim inputValue As Integer
Dim data As Variant

inputValue = 21
data = Evaluate("Return callvar * 2", inputValue)
Print data
```

Expected result: `42`.

## Variant containing a scalar

- [>] when a Variant contains one scalar value, `callvar` resolves to the contained runtime value rather than being flattened to String
- [ ] expand evaluator inspection functions so `TypeName(callvar)` can be regression-tested directly inside Evaluate

## Array callvar

- [>] XPScript arrays are copied with element type, allocation state, rank, lower bounds and upper bounds preserved
- [>] multidimensional XPScript arrays are copied recursively by dimension
- [>] `callvar(index)` reads array values from the isolated snapshot
- [>] caller-owned XPScript array storage is not modified by Evaluate
- [>] CLR arrays supplied through Variant are cloned and exposed with zero-based indexing
- [ ] expose `LBound(callvar)` and `UBound(callvar)` inside the evaluator expression function whitelist
- [ ] define collection-size/depth limits for untrusted inputs as part of security review

```xpscript
Dim args As Variant
Dim data As Variant

args = Array(10, 20, 30)
data = Evaluate("Return callvar(0) + callvar(1) + callvar(2)", args)
```

Expected result: `60`.

## List callvar

- [>] XPScript List input is cloned into a separate same-typed List instance before evaluation
- [>] list tags and entry runtime values are preserved by the snapshot operation
- [>] `callvar("tag")` reads values from the isolated List snapshot
- [>] a List can therefore be used as a named-parameter package
- [ ] nested mutable values inside List entries need explicit deep-copy policy beyond current Array/List handling

```xpscript
Dim parameters List As Variant
Dim data As Variant

parameters("price") = 125.5
parameters("quantity") = 4
parameters("customer") = "Fredrik"

data = Evaluate("Return callvar(""price"") * callvar(""quantity"")", parameters)
```

Expected result: `502`.

## Multiple logical parameters

- [>] Evaluate still receives one physical `callvar` argument; Array/List carries multiple logical parameters
- [>] List is the named-parameter transport
- [>] Array is the ordered/indexed transport
- [ ] nested Variant/List/Array traversal beyond one `callvar(index/tag)` level
- [ ] define recursion/depth and collection-size limits for untrusted Evaluate input

## Return semantics

- [>] `Return expression` immediately ends evaluation and becomes the `Evaluate` return value
- [>] scalar return values preserve their runtime type where possible
- [>] arrays/lists returned through the restricted return path are detached/cloned so evaluator-owned collection storage is not exposed
- [>] `data = Evaluate(...)` receives the value from `Return expression`
- [ ] change end-of-source-without-`Return` to explicit Empty/Nothing result; current evaluator still returns its last evaluated expression
- [ ] distinguish `Return Nothing`, `Return Null`, and no `Return` according to final XPScript `Nothing`/`Null` semantics
- [ ] finalize support/policy for returning arbitrary object references

## Type assignment after Evaluate

- [>] the main caller assignment pipeline remains responsible for assigning/coercing the object returned by Evaluate
- [ ] verify typed caller assignments use exactly the same coercion/diagnostic behavior as ordinary assignments
- [ ] invalid return-type assignment must surface an XPScript error rather than a raw .NET exception
- [ ] regression source for Integer/String/Date/Array/List return assignments

## Isolation and security

- [>] `callvar` is stored per Evaluator instance, not in a shared static variable dictionary
- [>] each Evaluate invocation receives its own local variable dictionary and `RestrictedCallVar` context
- [>] `Dim callvar` is rejected
- [>] direct `callvar = ...` assignment is rejected; negative source: `samples/evaluate-callvar-readonly-error.xps`
- [>] caller locals remain inaccessible unless explicitly supplied through `callvar`
- [>] arrays/lists are defensively copied before execution
- [ ] nested Evaluate implementation and isolation test
- [ ] concurrent Evaluate isolation test
- [ ] ensure diagnostics never serialize secret `callvar` values
- [ ] reject or wrap unsafe/disposable/native object references passed via `callvar`

## Memory and lifetime

- [>] evaluator and `callvar` are ordinary per-invocation objects with no static parameter cache
- [>] temporary Array/List snapshots become GC-eligible after evaluation when no returned reference retains them
- [>] returned Array/List values are cloned/detached on the return path
- [ ] explicitly clear evaluator references after completion/failure if profiling shows lifetime extension
- [ ] deterministic disposal rules if disposable/native-resource objects are ever approved as `callvar`
- [ ] memory/lifetime stress tests when execution is re-enabled

## Documentation and examples

- [ ] document both overloads under `docs/`
- [ ] document scalar and Variant input
- [ ] document Array positional parameters
- [ ] document List named parameters
- [ ] document `Return` and assignment of result
- [ ] add reusable user-facing versions under `examples/`
- [>] regression source: `samples/evaluate-callvar.xps`
- [>] negative read-only source: `samples/evaluate-callvar-readonly-error.xps`
- [ ] add negative examples proving unrelated caller variables are unavailable
- [ ] add concurrency/isolation regression tests when test execution is re-enabled

## Proposed contract summary

```xpscript
' One scalar parameter
result = Evaluate("Return callvar * 2", number)

' Multiple positional parameters
values = Array(10, 20)
result = Evaluate("Return callvar(0) + callvar(1)", values)

' Multiple named parameters
Dim parameters List As Variant
parameters("x") = 10
parameters("y") = 20
result = Evaluate("Return callvar(""x"") + callvar(""y"")", parameters)
```

The key design rule is that `callvar` is an explicit restricted input channel and `Return` is the explicit output channel. Evaluate must not implicitly share the caller's variable namespace.
