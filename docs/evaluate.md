# XPScript Evaluate

> For compact command syntax, parameters and examples, see the [Command Reference](command-reference.md).

`Evaluate` executes XPScript source supplied as text. Values passed into `Evaluate` follow the same parameter-passing rule as Sub and Function parameters: parameters are ByRef by default, and explicit `ByVal` creates an isolated copy.

## Syntax

```xpscript
result = Evaluate(sourceText)
result = Evaluate(sourceText, value)
result = Evaluate(sourceText, ByVal value)
result = Evaluate(sourceText, value1, value2, ...)
result = Evaluate(sourceText, ByVal value1, ByVal value2, ...)
```

`sourceText` is XPScript source text. `callvar` is the explicit value bridge from the caller into the evaluator.

With exactly one supplied value, `callvar` is that value directly.

With two or more supplied values, XPScript packs the values into one zero-based `callvar` array in caller argument order. There is no separate fixed value-count limit.

## Parameter passing

A normal variable argument is ByRef. Changes made to `callvar` are written back to the caller variable.

```xpscript
Dim number As Integer
number = 10

Call Evaluate("callvar = 20", number)
' number is now 20
```

Use `ByVal` when evaluated code must receive a copy:

```xpscript
Dim number As Integer
Dim result As Variant
number = 10

result = Evaluate("callvar = 20 : Return callvar", ByVal number)
' number is still 10
' result is 20
```

The same rule applies to Sub and Function parameters. A parameter without `ByVal` is ByRef. An explicit `ByVal` parameter receives a copy.

For expressions that are not assignable caller variables, there is no caller location to write back to, so the value behaves as an input value.

## Multiple values

When two or more values are supplied, they become a zero-based `callvar` array:

```xpscript
Dim firstValue As Integer
Dim secondValue As Integer
Dim result As Variant

firstValue = 10
secondValue = 20
result = Evaluate("Return callvar(0) + callvar(1)", firstValue, secondValue)
```

Inside this call, `LBound(callvar)` is 0 and `UBound(callvar)` is 1.

Each supplied variable follows its own parameter mode. This allows mixed calls:

```xpscript
result = Evaluate(
    "callvar(0) = 100 : callvar(1) = 200 : Return callvar(0) + callvar(1)",
    firstValue,
    ByVal secondValue)
```

Here `firstValue` is written back because it is ByRef. `secondValue` remains unchanged because it is ByVal.

## Arrays and Lists

Arrays and Lists passed ByRef can be changed by evaluated code and those changes are visible to the caller.

```xpscript
Dim parameters List As Variant
parameters("price") = 100

Call Evaluate("callvar(""price"") = 125", parameters)
' parameters("price") is now 125
```

Use `ByVal` for an isolated collection snapshot:

```xpscript
Dim parameters List As Variant
Dim result As Variant
parameters("price") = 100

result = Evaluate("callvar(""price"") = 125 : Return callvar(""price"")", ByVal parameters)
' parameters("price") is still 100
' result is 125
```

Nested Lists and arrays in a ByVal argument are copied recursively. Shared child references reachable through multiple ByVal arguments preserve shared identity inside the snapshot while remaining detached from caller-owned data.

## ByVal snapshot limits

ByVal collection snapshots are bounded to prevent unbounded memory and CPU use:

- maximum nesting depth: **64**
- maximum collection elements: **100,000**
- maximum estimated payload: **16 MiB / 16,777,216 bytes**

For a multi-value Evaluate call, all ByVal values share one aggregate input snapshot budget. The generated outer `callvar` array also counts toward the element budget. String values and List tags count toward the payload budget using their UTF-8 byte length. Scalar values use the runtime's conservative size estimate.

There is no separate fixed argument-count limit. The effective limit for copied inputs is the normal Evaluate snapshot budget.

Exceeding a ByVal snapshot limit raises XPScript error 5.

## Return value

The evaluated source returns a value only through an explicit `Return expression` statement.

```xpscript
Dim result As Variant
result = Evaluate("Return 10 + 20")
Print CStr(result)
```

If execution reaches the end without `Return`, `Evaluate` returns Nothing/Empty.

Returned arrays and Lists are detached from evaluator-owned storage before control returns to the caller.

## Scope and isolation

The evaluator does not implicitly inherit caller locals, module globals or static variables. Only explicitly supplied values are available through `callvar`.

`ByVal` controls copying of supplied values. Default ByRef values intentionally retain caller write-back semantics.

Arbitrary mutable object types that are not supported by the Evaluate value contract remain rejected when a ByVal snapshot is requested.

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

`Evaluate` is not an operating-system or process security sandbox. ByVal snapshot isolation, resource budgets and diagnostic sanitization reduce specific risks, but they do not replace process isolation or operating-system sandboxing.
