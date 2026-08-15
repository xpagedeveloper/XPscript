# XPScript Evaluate

> For compact command syntax, parameters and examples, see the [Command Reference](command-reference.md).

`Evaluate` executes XPScript source supplied as text inside an isolated evaluator scope.

## Syntax

```xpscript
result = Evaluate(sourceText)
result = Evaluate(sourceText, callvar)
```

`sourceText` is XPScript source text. `callvar` is the only explicit value bridge from the caller into the evaluator.

## Return value

The evaluated source returns a value only through an explicit `Return expression` statement.

```xpscript
Dim result As Variant
result = Evaluate("Return 10 + 20")
Print CStr(result)
```

If execution reaches the end without `Return`, `Evaluate` returns Nothing/Empty.

## Passing one value with callvar

Scalar values can be supplied directly:

```xpscript
Dim number As Integer
Dim result As Variant

number = 21
result = Evaluate("Return callvar * 2", number)
Print CStr(result)
```

`callvar` is read-only inside the evaluator. Evaluated source cannot assign to it or declare another local named `callvar`.

## Passing positional values with an array

```xpscript
Dim values As Variant
Dim result As Variant

values = Array(10, 20, 30)
result = Evaluate("Return callvar(0) + callvar(1) + callvar(2)", values)
```

XPScript arrays are copied before evaluation. Rank, bounds and element type are preserved where supported. Changes inside the evaluator cannot mutate the caller's array.

## Passing named values with a List

A List is the recommended way to supply several named parameters.

```xpscript
Dim parameters List As Variant
Dim result As Variant

parameters("price") = 125.5
parameters("quantity") = 4

result = Evaluate("Return callvar(""price"") * callvar(""quantity"")", parameters)
Print CStr(result)
```

Lists are copied into an evaluator-private read-only snapshot. Nested Lists and arrays are copied recursively.

## Isolation model

The evaluator does not implicitly inherit the caller's local variables, module globals or static variables. Only the optional `callvar` value is transferred into the evaluator.

Mutable arrays and Lists passed through `callvar` are defensively copied. Arbitrary mutable object references are rejected rather than shared by reference.

Returned arrays and Lists are detached from evaluator-owned storage before control returns to the caller.

## Collection limits

To prevent unbounded memory and CPU use while copying `callvar`, each input or returned collection snapshot currently has these limits:

- maximum nesting depth: 64
- maximum collection elements: 100,000
- maximum estimated payload: 16 MiB

String values and List tags count toward the payload budget using their UTF-8 byte length. Exceeding a limit raises XPScript error 5.

These are runtime safety limits, not a guarantee that `Evaluate` is a complete security sandbox.

## Diagnostics and secrets

Errors produced by `Evaluate` are sanitized before they leave the evaluator. Error messages may contain structural information such as function names, expected argument counts and parser locations, but should not echo `callvar` values or large string payloads.

Important error numbers include:

- 5: invalid procedure call / evaluator syntax or policy error
- 6: overflow
- 9: subscript or List-tag lookup error
- 11: division by zero
- 13: type mismatch
- 70: access or permission error

Do not depend on an error message containing a secret or input value for troubleshooting.

## Function availability

`Evaluate` supports a deliberately bounded subset of side-effect-free XPScript functions. Current groups include:

- conversions such as `CStr`, `CInt`, `CLng`, `CDbl`, `CDate`
- inspection such as `TypeName`, `DataType`, `IsArray`, `IsDate`, `LBound`, `UBound`
- string functions such as `Len`, `Left`, `Right`, `Mid`, `Instr`, `Replace`, `Trim`
- math functions such as `Abs`, `Round`, `Sqr`, `Sin`, `Cos`, `Log`
- date functions such as `Year`, `Month`, `DateAdd`, `DateDiff`, `DatePart`

A known function called with the wrong number of arguments reports an argument-count diagnostic. A function that is not exposed inside `Evaluate` reports that it is unavailable.

## Coercion and comparisons

Evaluator arithmetic and comparison behavior reuses the main XPScript runtime semantics where implemented. This includes forgiving dynamic `+`, numeric string coercion and Date/numeric/String comparison paths.

For explicit concatenation, prefer `&` when string concatenation is the intended operation.

## Security guidance

Use `Evaluate` for controlled expressions or small pieces of XPScript source whose origin and purpose are understood by the host application.

`Evaluate` is **not** intended to be a security boundary for arbitrary hostile code. Snapshot isolation, resource budgets and diagnostic sanitization reduce several risks, but they do not replace process isolation or operating-system sandboxing.

Recommended practices:

1. Prefer application-defined expression templates over arbitrary user-authored source.
2. Pass only the minimum required values through `callvar`.
3. Do not pass credentials unless the evaluated expression genuinely needs them.
4. Prefer a List with explicitly named parameters for complex input.
5. Treat evaluator errors as diagnostics, not as a channel for returning data.
6. Keep resource limits enabled.
7. If untrusted users must execute programmable code, run that workload in a separately isolated process/container with OS-level restrictions.

## Samples used by this documentation

The documentation intentionally uses source fixtures that already exist under `samples/`:

- [samples/evaluate-xpscript.xps](../samples/evaluate-xpscript.xps) — basic evaluation and explicit Return
- [samples/evaluate-callvar.xps](../samples/evaluate-callvar.xps) — scalar/array/List input through callvar
- [samples/evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps) — TypeName, LBound and UBound
- [samples/evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) — supported side-effect-free functions
- [samples/evaluate-nested-collections.xps](../samples/evaluate-nested-collections.xps) — nested List/array snapshots
- [samples/evaluate-no-return.xps](../samples/evaluate-no-return.xps) — no-Return behavior
- [samples/evaluate-callvar-readonly-error.xps](../samples/evaluate-callvar-readonly-error.xps) — read-only callvar enforcement
- [samples/evaluate-scope-error.xps](../samples/evaluate-scope-error.xps) — caller-scope isolation
- [samples/evaluate-coercion-diagnostics.xps](../samples/evaluate-coercion-diagnostics.xps) — coercion/error semantics
- [samples/evaluate-function-arity-errors.xps](../samples/evaluate-function-arity-errors.xps) — wrong-arity diagnostics
- [samples/evaluate-collection-element-budget.xps](../samples/evaluate-collection-element-budget.xps) — element budget
- [samples/evaluate-collection-payload-budget.xps](../samples/evaluate-collection-payload-budget.xps) — payload budget
- [samples/evaluate-diagnostic-sanitization.xps](../samples/evaluate-diagnostic-sanitization.xps) — secret-safe diagnostics
