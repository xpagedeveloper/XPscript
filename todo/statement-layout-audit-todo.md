# XPScript statement layout audit TODO

(c) xpagedeveloper.com 2026

Tracks parser/preprocessor/runtime regressions discovered while verifying valid one-line and multiline statement layouts.

Rules:
- Only documented or deliberately supported XPScript layouts are added as valid syntax.
- `_` is the standard explicit line-continuation mechanism unless a construct has an explicitly supported layout such as split `If` / `Then`.
- Any independent compiler/preprocessor/runtime defect discovered by this audit must remain tracked here until its regression gate passes.

## Discovered regressions

- [x] continued procedure/property headers must not receive `XPSourceLineRuntime.Set(...)` markers inside their logical declaration/parameter list.
  - Discovered by `samples/statement-layout-audit.xps`.
  - Previous diagnostic: `Unsupported parameter declaration: Call XPSourceLineRuntime.Set(...)`.
  - `SourceLineMarkerPreprocessor` now carries continuation state from the procedure/property start line.
  - Verified by the full statement-layout regression.

- [x] single-line `If` / inline `ElseIf` with compound comparison conditions using `And` / `Or` use the same logical-comparison lowering as block `If ... Then` headers.
  - Reproducer: `If total = 3 And total > 0 Then Print "..."`.
  - Previous failure compared incompatible runtime values such as `int == bool`.
  - Trailing `Then statement` / `Else statement` syntax is preserved and verified.

- [x] `While`, `Do While`, `Do Until`, `Loop While` and `Loop Until` compound comparison conditions using `And` / `Or` use precedence-safe logical-comparison lowering.
  - Previous runtime diagnostic: `Microsoft.CSharp.RuntimeBinder.RuntimeBinderException: Operator '==' cannot be applied to operands of type 'bool' and 'int'`.
  - Verified with both `And` and `Or` across supported pre-test and post-test loop forms.

## Layout coverage

- [x] `_` continuation in `Sub` / `Function` parameter lists.
- [x] `_` continuation in `Property Get/Let/Set` headers.
- [x] `_` continuation in class inheritance headers.
- [x] `_` continuation in function/sub calls and argument lists.
- [x] `_` continuation in `If` expressions including compound comparisons.
- [x] `_` continuation in `For ... To ... Step` expressions.
- [x] `_` continuation in `ForAll ... In` expressions.
- [x] `_` continuation in `While`, `Do While/Until` and `Loop While/Until` conditions.
- [x] `_` continuation in `Select Case` selector and `Case ... To ...` expressions.
- [x] `_` continuation in `With` expressions.
- [x] existing multiline native `Declare` regression remains green.
- [ ] audit compact multiple-statements-on-one-line syntax separately; do not silently treat `:` as supported until its grammar and source-line semantics are deliberately defined.

Primary regression source: `samples/statement-layout-audit.xps`.
