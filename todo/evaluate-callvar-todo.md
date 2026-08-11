# XPScript Evaluate callvar and return semantics TODO

(c) xpagedeveloper.com 2026

This checklist extends the main runtime TODO with the parameter-passing and return-value contract for `Evaluate`.

Status:
- `[x]` implemented and verified
- `[>]` implemented/in progress, awaiting verification
- `[ ]` not implemented

## Evaluate signature and input bridge

- [ ] extend `Evaluate` with an optional second argument named `callvar`
- [ ] supported surface: `Evaluate(sourceText)` and `Evaluate(sourceText, callvar)`
- [ ] `callvar` is the only explicit caller-provided variable bridge into the isolated Evaluate scope
- [ ] `callvar` must be restricted/read-only by default inside Evaluate so evaluated code cannot overwrite the caller's variable or mutate unrelated caller state
- [ ] evaluated code must not gain implicit access to caller locals, module globals, statics, compiler internals, runtime internals or unrelated variables
- [ ] document whether mutable object references passed through `callvar` are copied, wrapped read-only, or rejected; default design should avoid allowing evaluated code to mutate caller-owned state unintentionally

## Scalar callvar

- [ ] when `callvar` contains a single scalar value, expose it inside Evaluate as `callvar` with the same XPScript type
- [ ] preserve scalar types where possible: String, Boolean, Byte, Integer, Long, Single, Double, Currency, Date, Variant and supported object/value types
- [ ] example:

```xpscript
Dim inputValue As Integer
Dim data As Variant

inputValue = 21
data = Evaluate("Return callvar * 2", inputValue)
Print data
```

Expected result: `42`.

## Variant containing a scalar

- [ ] when a Variant contains one scalar value, `callvar` resolves to the contained value while preserving the contained runtime type
- [ ] `TypeName(callvar)` and conversion behavior inside Evaluate should reflect the contained value, not merely the fact that the caller variable was declared Variant

## Array callvar

- [ ] when `callvar` contains an XPScript array, expose the array in the Evaluate scope without flattening away its bounds or element types
- [ ] evaluated code can use normal array syntax, `LBound`, `UBound` and indexed reads
- [ ] preserve multidimensional arrays where supported
- [ ] define whether Evaluate receives a defensive copy of arrays; preferred security model is a copy/read-only view so Evaluate cannot overwrite caller-owned array contents
- [ ] allow Evaluate code to derive multiple input parameters from an array by reading its elements
- [ ] example:

```xpscript
Dim args As Variant
Dim data As Variant

args = Array(10, 20, 30)
data = Evaluate("Return callvar(0) + callvar(1) + callvar(2)", args)
```

Expected result: `60`.

## List callvar

- [ ] when `callvar` contains an XPScript List, expose the list inside Evaluate with its tags/keys and values intact
- [ ] evaluated code can read list entries using normal XPScript list syntax
- [ ] preserve value types stored in list entries
- [ ] preferred security model is a defensive copy/read-only representation so evaluated code cannot modify the original caller list
- [ ] a list acts as a convenient named-parameter package for Evaluate
- [ ] example conceptual call:

```xpscript
Dim parameters List As Variant
Dim data As Variant

parameters("price") = 125.5
parameters("quantity") = 4
parameters("customer") = "Fredrik"

data = Evaluate("Return callvar(\"price\") * callvar(\"quantity\")", parameters)
```

Expected result: `502`.

## Multiple logical parameters

- [ ] explicitly document that Evaluate still receives one physical `callvar` argument, but an Array or List can carry many logical parameters
- [ ] List is the preferred named-parameter transport when parameter names matter
- [ ] Array is the preferred ordered/indexed transport when positional values are sufficient
- [ ] nested Variant/List/Array values should remain accessible where the normal XPScript type system supports them
- [ ] define recursion/depth and collection-size limits for untrusted Evaluate input as part of the security review

## Return semantics

- [ ] `Return expression` inside evaluated XPScript immediately ends evaluation and becomes the return value from `Evaluate`
- [ ] the returned value preserves its XPScript runtime type where possible
- [ ] support returning String, Boolean, numeric types, Currency, Date, Variant, arrays, lists and approved value/object types
- [ ] `data = Evaluate(...)` assigns the value supplied by the evaluated `Return` statement to `data`
- [ ] example:

```xpscript
Dim parameters List As Variant
Dim data As Variant

parameters("first") = 20
parameters("second") = 22

data = Evaluate("Return callvar(\"first\") + callvar(\"second\")", parameters)
Print data
```

Expected result: `42`.

- [ ] if evaluated code reaches the end without `Return`, define the result as `Nothing`/Empty Variant rather than leaking an internal evaluator value
- [ ] distinguish `Return Nothing`, `Return Null`, and no `Return` according to final XPScript `Nothing`/`Null` semantics
- [ ] returning an array or list must not expose mutable evaluator-internal storage after the Evaluate scope is destroyed; copy/detach returned collections where necessary

## Type assignment after Evaluate

- [ ] normal assignment/coercion rules apply to the result of Evaluate
- [ ] `Dim data As Integer : data = Evaluate(...)` should coerce or reject using the same rules as a normal XPScript assignment
- [ ] invalid return-type assignment must produce a normal XPScript diagnostic/runtime error rather than a .NET exception
- [ ] Variant receives the returned value without unnecessary conversion

## Isolation and security

- [ ] `callvar` must never be implemented as a shared static dictionary that could leak values between concurrent Evaluate calls
- [ ] each Evaluate invocation gets a unique isolated scope/context
- [ ] nested Evaluate calls get independent `callvar` scopes
- [ ] concurrent threads/tasks cannot read or overwrite another Evaluate invocation's `callvar`
- [ ] evaluated code cannot obtain a direct reference to compiler-generated variable stores
- [ ] validate and reserve internal names so user code cannot shadow the evaluator's internal callvar storage
- [ ] defensive-copy mutable arrays/lists before execution unless explicit reference-sharing semantics are later designed and documented
- [ ] sanitize diagnostics so secret values passed through `callvar` are not automatically included in error messages
- [ ] include `callvar` in the security/isolation review and memory/lifetime review

## Memory and lifetime

- [ ] release evaluator references to `callvar` immediately after Evaluate completes or fails
- [ ] returned values remain alive according to normal caller references
- [ ] temporary defensive copies become GC-eligible after evaluation
- [ ] deterministic disposal rules are required if approved disposable/native-resource objects can ever be passed through `callvar`
- [ ] do not let evaluator caches retain arbitrary `callvar` values

## Documentation and examples

- [ ] document both overloads: `Evaluate(sourceText)` and `Evaluate(sourceText, callvar)`
- [ ] document scalar input
- [ ] document Variant input
- [ ] document Array parameter package
- [ ] document List named-parameter package
- [ ] document `Return` and assignment of the result, e.g. `data = Evaluate(...)`
- [ ] add reusable examples under `examples/`
- [ ] add negative examples proving caller variables cannot be accessed unless supplied through `callvar`
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
result = Evaluate("Return callvar(\"x\") + callvar(\"y\")", parameters)
```

The key design rule is that `callvar` is an explicit restricted input channel and `Return` is the explicit output channel. Evaluate must not implicitly share the caller's variable namespace.
