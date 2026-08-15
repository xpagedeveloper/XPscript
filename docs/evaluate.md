# XPScript Evaluate

> For compact command syntax, parameters and examples, see the [Command Reference](command-reference.md).

`Evaluate` executes XPScript source supplied as text inside an isolated evaluator scope.

## Syntax

```xpscript
result = Evaluate(sourceText)
result = Evaluate(sourceText, callvar)
result = Evaluate(sourceText, value1, value2, ...)
```

`sourceText` is XPScript source text. `callvar` is the only explicit value bridge from the caller into the evaluator.

With exactly one supplied value, existing behavior is unchanged and `callvar` is that value directly.

With two or more supplied values, XPScript packs the values into one zero-based `callvar` array in caller argument order. There is no separate fixed value-count limit. The effective limit is determined by the normal Evaluate snapshot budgets: nesting depth 64, 100,000 total collection elements and 16 MiB estimated payload.

Example:

```xpscript
result = Evaluate("Return callvar(0) + callvar(7)", 1, 2, 3, 4, 5, 6, 7, 8)
```

Inside that Evaluate call, `LBound(callvar)` is 0 and `UBound(callvar)` is 7.

### Evaluate limitations

`Evaluate` is intentionally restricted. The limits are part of the public runtime contract and should be considered when deciding whether a workload belongs inside `Evaluate`:

- only the supported side-effect-free Evaluate function subset is available; arbitrary XPScript runtime APIs are not automatically exposed,
- `callvar` is the only caller-to-evaluator data bridge and is read-only inside the evaluator,
- caller locals, module globals and static variables are not implicitly available,
- arbitrary mutable object references are rejected instead of being shared into the evaluator,
- arrays and Lists crossing the boundary are defensively snapshotted,
- each input or return snapshot allows at most **64 nested collection levels**,
- each snapshot allows at most **100,000 collection elements** in total,
- each snapshot allows at most **16 MiB (16,777,216 bytes)** of estimated payload,
- String values and List tags count against the payload budget using their UTF-8 byte length,
- exceeding a snapshot limit raises XPScript error 5,
- `Evaluate` is not an operating-system/process security sandbox and must not be treated as one for arbitrary hostile code.

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

## Passing multiple values with callvar

When two or more values are supplied directly to Evaluate, they become a zero-based `callvar` array:

```xpscript
Dim result As Variant
result = Evaluate("Return callvar(0) + callvar(1) + callvar(2)", 10, 20, 30)
```

The same snapshot budget applies to the complete generated array and all nested values. The number of supplied values is therefore limited by the array snapshot budgets rather than by a fixed argument count.

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

The evaluator does not implicitly inherit the caller's local variables, module globals or static variables. Only explicit callvar input is transferred into the evaluator.

Mutable arrays and Lists passed through `callvar` are defensively copied. Arbitrary mutable object references are rejected rather than shared by reference.

Returned arrays and Lists are detached from evaluator-owned storage before control returns to the caller.

## Collection limits

To prevent unbounded memory and CPU use while copying `callvar`, each input or returned collection snapshot currently has these limits:

- maximum nesting depth: **64**
- maximum collection elements: **100,000**
- maximum estimated payload: **16 MiB / 16,777,216 bytes**

The element budget is cumulative across the complete generated multi-value callvar array and all nested arrays/Lists in one snapshot operation. String values and List tags count toward the payload budget using their UTF-8 byte length; scalar values use the runtime's conservative size estimate. Exceeding a limit raises XPScript error 5 before the evaluator is allowed to continue with an unbounded snapshot.

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

`Evaluate` supports a deliberately bounded subset of side-effect-free XPScript functions. Current groups include conversions, inspection, string, math and date functions.

A known function called with the wrong number of arguments reports an argument-count diagnostic. A function that is not exposed inside `Evaluate` reports that it is unavailable.

## Coercion and comparisons

Evaluator arithmetic and comparison behavior reuses the main XPScript runtime semantics where implemented. This includes forgiving dynamic `+`, numeric string coercion and Date/numeric/String comparison paths.

For explicit concatenation, prefer `&` when string concatenation is the intended operation.

## Security guidance

Use `Evaluate` for controlled expressions or small pieces of XPScript source whose origin and purpose are understood by the host application.

`Evaluate` is **not** intended to be a security boundary for arbitrary hostile code. Snapshot isolation, resource budgets and diagnostic sanitization reduce several risks, but they do not replace process isolation or operating-system sandboxing.
