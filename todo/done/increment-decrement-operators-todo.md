# XPScript increment/decrement and compound-assignment operator TODO

(c) xpagedeveloper.com 2026

Status: completed and verified.

Final verification before archival:
- permanent `Increment Compound Operators` workflow runs on Windows, Ubuntu and macOS;
- positive regression covers postfix `++`/`--`, all compound operators, numeric types, numeric Variant, String coercion, empty String concatenation, scalar-to-String concatenation, overflow, divide-by-zero and exactly-once RHS evaluation;
- negative regression proves literals, arbitrary expressions, prefix operators, value-producing postfix use and invalid String numeric compound assignment fail compilation;
- negative diagnostics are required to map to the exact `.xps` source line, position and source code;
- token-like text in String literals/comments and unary `+`/`-` compatibility are regression-covered;
- Required PR Gate, .NET 10 Build, Evaluate Runtime Compatibility, Control Flow and Escaped Quote regressions remained green on the implementation head.

## Postfix increment/decrement

- [x] support `nrvar++` as shorthand for `nrvar = nrvar + 1`
- [x] support `nrvar--` as shorthand for `nrvar = nrvar - 1`
- [x] operators are standalone mutation statements on assignable simple variables
- [x] preserve the declared numeric type through XPScript assignment/coercion rules
- [x] support `Byte`, `Integer`, `Long`, `Single`, `Double`, `Currency` and numeric `Variant`
- [x] reject non-numeric variables/values with XPScript type mismatch error 13
- [x] overflow follows normal assignment semantics and maps to XPScript error 6
- [x] reject non-assignable operands such as `1++` and `(a + b)++`
- [x] parsing does not reinterpret operator text inside String literals/comments and preserves unary `+`/`-`
- [x] prefix `++variable` / `--variable` remains unsupported and is regression-rejected
- [x] postfix operators cannot be used as value-producing expressions in this scope

## Compound assignment

- [x] support `+=`
- [x] support `-=`
- [x] support `*=`
- [x] support `/=`
- [x] support `\=` with XPScript integer-division semantics
- [x] support `&=` with XPScript String concatenation semantics
- [x] preserve forgiving XPScript `+` coercion for `+=`
- [x] require numeric targets for `-=`, `*=`, `/=` and `\=` when the declared target type is known
- [x] require String/Variant-compatible targets for `&=` when the declared target type is known
- [x] allow `+=` where normal XPScript addition/coercion permits it
- [x] division by zero through `/=` and `\=` uses the long-form XPScript error semantics
- [x] overflow uses XPScript error 6 where the equivalent long-form assignment overflows
- [x] assignment back preserves normal target type/coercion behavior
- [x] right-hand expressions are evaluated exactly once
- [x] supported simple-variable left-hand targets are evaluated once; indexed/property compound targets remain outside this scope
- [x] unsupported/non-assignable left-hand operands produce source-mapped diagnostics

## Compiler/runtime implementation

- [x] postfix and compound tokens use shared source validation/lowering without changing ordinary arithmetic semantics
- [x] `++`/`--` use the existing XPScript numeric/coercion behavior
- [x] compound assignments lower through the existing XPScript operator/runtime behavior
- [x] generated code evaluates the supported target and RHS once
- [x] `/=`, `\=` and `&=` are distinguished from their ordinary operators
- [x] `++`/`--` do not alter unary signs, negative literals, comments or String literals
- [x] invalid operands return the correct `.xps` file, line, position and marked source

## Verification

- [x] positive regression covers `++` and `--` across representative numeric types
- [x] equivalence with long-form `+ 1` and `- 1` is verified
- [x] positive regression covers `+=`, `-=`, `*=`, `/=`, `\=` and `&=`
- [x] compound operators are compared against equivalent long-form assignments
- [x] negative coverage includes String, Boolean, Date, Object/non-numeric Variant, literals and expressions
- [x] overflow regression coverage is present
- [x] divide-by-zero regression coverage is present for `/=` and `\=`
- [x] `&=` is verified with empty String, normal String and scalar-to-String coercion
- [x] RHS exactly-once behavior is verified
- [x] permanent GitHub Actions gate runs on Windows, Ubuntu and macOS
- [x] syntax is documented in the operator/language documentation
- [x] exact-head regression verification completed before archival
