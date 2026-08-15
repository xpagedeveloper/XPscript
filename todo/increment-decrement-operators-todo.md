# XPScript increment/decrement and compound-assignment operator TODO

(c) xpagedeveloper.com 2026

Goal: add concise assignment syntax for numeric variables and String concatenation while preserving normal XPScript coercion, overflow and diagnostics behavior.

## Postfix increment/decrement

- [ ] support `nrvar++` as shorthand for `nrvar = nrvar + 1`
- [ ] support `nrvar--` as shorthand for `nrvar = nrvar - 1`
- [ ] initially support the operators as standalone statements on assignable variables
- [ ] preserve the declared numeric type where the existing XPScript numeric assignment/coercion rules permit it
- [ ] support the normal XPScript numeric variable types (`Byte`, `Integer`, `Long`, `Single`, `Double`, `Currency` and numeric `Variant` values where runtime type is numeric)
- [ ] reject use on non-numeric variables/values with a clear XPScript diagnostic rather than implicit String/Object coercion
- [ ] define overflow behavior consistently with normal `variable = variable +/- 1` semantics and map runtime overflow to XPScript error 6 where applicable
- [ ] reject non-assignable operands such as literals and arbitrary expressions (`1++`, `(a + b)++`)
- [ ] ensure `++`/`--` parsing does not conflict with existing `+`, `-`, unary-sign or comment/tokenization behavior
- [ ] do not add prefix `++variable` / `--variable` semantics in this scope
- [ ] do not allow `variable++` / `variable--` as value-producing expressions in this scope; they are standalone mutation statements only

## Compound assignment

Implement compound assignment through the same general assignment/lowering infrastructure as `++` and `--`.

- [ ] support `variable += expression` as shorthand for `variable = variable + expression`
- [ ] support `variable -= expression` as shorthand for `variable = variable - expression`
- [ ] support `variable *= expression` as shorthand for `variable = variable * expression`
- [ ] support `variable /= expression` as shorthand for `variable = variable / expression`
- [ ] support `variable \= expression` as shorthand for `variable = variable \ expression` using XPScript integer-division semantics
- [ ] support `variable &= expression` as shorthand for `variable = variable & expression` using XPScript String concatenation semantics
- [ ] preserve existing forgiving XPScript `+` coercion behavior for `+=` rather than introducing C#/Java-specific conversion rules
- [ ] require numeric left-hand operands for `-=`, `*=`, `/=` and `\=`
- [ ] require an assignable String/Variant-compatible left-hand operand for `&=` and use normal XPScript `CStr`/concatenation behavior
- [ ] allow `+=` on numeric operands according to normal numeric/addition semantics and on values where existing XPScript `+` semantics explicitly permit String concatenation/coercion
- [ ] division by zero through `/=` or `\=` must use the same XPScript error semantics as the corresponding long-form operation
- [ ] overflow from compound assignment must use the same XPScript error 6 behavior as the equivalent long-form assignment where applicable
- [ ] assignment back to the target must preserve the target's declared type and normal assignment coercion rules
- [ ] right-hand expressions may be ordinary XPScript expressions and must be evaluated exactly once
- [ ] left-hand assignable targets must also be evaluated exactly once so future support for indexed/property targets cannot duplicate side effects
- [ ] reject unsupported/non-assignable left-hand operands with clear source-mapped diagnostics

## Examples

```xpscript
Dim counter As Integer
counter = 10
counter++
' counter is now 11
counter--
' counter is now 10

counter += 5
' counter is now 15
counter -= 3
' counter is now 12
counter *= 2
' counter is now 24
counter /= 4
' counter is now 6
counter \= 4
' counter is now 1

Dim text As String
text = "Hello"
text &= " world"
' text is now "Hello world"
```

Equivalent long forms:

```xpscript
counter = counter + 1
counter = counter - 1
counter = counter + 5
counter = counter - 3
counter = counter * 2
counter = counter / 4
counter = counter \ 4
text = text & " world"
```

Invalid examples:

```xpscript
Dim name As String
name = "test"
name++

name -= 1
```

These invalid examples must produce clear errors explaining that the selected operator requires a numeric assignable target.

## Compiler/runtime implementation

- [ ] implement a shared tokenizer/parser path for postfix `++`/`--` and compound assignment tokens without changing existing arithmetic semantics
- [ ] lower `++`/`--` through the same numeric/coercion behavior used by ordinary XPScript assignment and addition/subtraction
- [ ] lower compound assignments through the existing XPScript operator/runtime helpers rather than duplicating arithmetic/coercion behavior
- [ ] ensure generated code evaluates each target and right-hand expression exactly once
- [ ] ensure tokenization correctly distinguishes `/=` and `\=` from `/`, `\` and `=` and distinguishes `&=` from concatenation followed by comparison/assignment syntax
- [ ] ensure `++`/`--` tokens do not alter unary `+`/`-`, negative numeric literals, comments or adjacent arithmetic expressions
- [ ] emit source-mapped diagnostics with the correct `.xps` file, line and position for invalid operands

## Verification

- [ ] add positive regression samples for `++` and `--` across representative numeric types
- [ ] verify equivalence with `variable = variable + 1` and `variable = variable - 1`
- [ ] add positive regression coverage for `+=`, `-=`, `*=`, `/=`, `\=` and `&=`
- [ ] compare each compound operator against its equivalent long-form assignment in the same regression sample
- [ ] add negative regression coverage for String, Boolean, Date, Object/non-numeric Variant, literals and expressions where the selected operator is invalid
- [ ] add overflow regression coverage
- [ ] add divide-by-zero regression coverage for `/=` and `\=`
- [ ] verify `&=` with empty String, normal String and scalar-to-String coercion according to existing XPScript concatenation behavior
- [ ] verify right-hand expressions are evaluated exactly once
- [ ] add/extend a permanent GitHub Actions gate for these operators
- [ ] document the syntax in the language/operator documentation and command/reference index where appropriate
- [ ] move this TODO to `todo/done/` only after implementation, documentation and exact-head regression verification are complete
