# XPScript increment/decrement operator TODO

(c) xpagedeveloper.com 2026

Goal: add postfix increment/decrement syntax for numeric variables.

## Language behavior

- [ ] support `nrvar++` as shorthand for `nrvar = nrvar + 1`
- [ ] support `nrvar--` as shorthand for `nrvar = nrvar - 1`
- [ ] initially support the operators as standalone statements on assignable variables
- [ ] preserve the declared numeric type where the existing XPScript numeric assignment/coercion rules permit it
- [ ] support the normal XPScript numeric variable types (`Byte`, `Integer`, `Long`, `Single`, `Double`, `Currency` and numeric `Variant` values where runtime type is numeric)
- [ ] reject use on non-numeric variables/values with a clear XPScript diagnostic rather than implicit string/object coercion
- [ ] define overflow behavior consistently with normal `variable = variable +/- 1` semantics and map runtime overflow to XPScript error 6 where applicable
- [ ] reject non-assignable operands such as literals and arbitrary expressions (`1++`, `(a + b)++`)
- [ ] ensure `++`/`--` parsing does not conflict with existing `+`, `-`, unary-sign or comment/tokenization behavior

## Examples

```xpscript
Dim counter As Integer
counter = 10
counter++
' counter is now 11
counter--
' counter is now 10
```

Equivalent long form:

```xpscript
counter = counter + 1
counter = counter - 1
```

Invalid example:

```xpscript
Dim name As String
name = "test"
name++
```

The invalid example must produce a clear error explaining that `++`/`--` requires a numeric variable.

## Compiler/runtime implementation

- [ ] tokenize/parse postfix `++` and `--` without changing existing arithmetic semantics
- [ ] lower the operations through the same numeric/coercion behavior used by ordinary XPScript assignment and addition/subtraction
- [ ] ensure generated code evaluates the target exactly once
- [ ] emit source-mapped diagnostics with the correct `.xps` file, line and position for invalid operands

## Verification

- [ ] add positive regression samples for `++` and `--` across representative numeric types
- [ ] verify equivalence with `variable = variable + 1` and `variable = variable - 1`
- [ ] add negative regression coverage for String, Boolean, Date, Object/non-numeric Variant, literals and expressions
- [ ] add overflow regression coverage
- [ ] add/extend a permanent GitHub Actions gate for these operators
- [ ] document the syntax in the language/operator documentation and command/reference index where appropriate
- [ ] move this TODO to `todo/done/` only after implementation, documentation and exact-head regression verification are complete
