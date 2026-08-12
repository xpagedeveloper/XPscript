# XPScript statement layout audit TODO

(c) xpagedeveloper.com 2026

Tracks parser/preprocessor/runtime regressions discovered while verifying valid one-line and multiline statement layouts.

Rules:
- Only documented or deliberately supported XPScript layouts are added as valid syntax.
- `_` is the standard explicit line-continuation mechanism unless a construct has an explicitly supported layout such as split `If` / `Then`.
- Any independent compiler/preprocessor/runtime defect discovered by this audit must remain tracked here until its regression gate passes.

## Discovered regressions

- [>] continued procedure/property headers must not receive `XPSourceLineRuntime.Set(...)` markers inside their logical declaration/parameter list.
  - Discovered by `samples/statement-layout-audit.xps`.
  - Previous diagnostic: `Unsupported parameter declaration: Call XPSourceLineRuntime.Set(...)`.
  - Implementation changed `SourceLineMarkerPreprocessor` to carry continuation state from the procedure/property start line.
  - Mark `[x]` only after the full statement-layout regression passes.

- [>] single-line `If` / inline `ElseIf` with compound comparison conditions using `And` / `Or` must use the same logical-comparison lowering as block `If ... Then` headers.
  - Discovered after the continued-header fix allowed the audit sample to execute.
  - Reproducer: `If total = 3 And total > 0 Then Print "..."`.
  - Previous failure: generated/runtime expression compared incompatible values such as `int == bool` because `RewriteLogicalComparisonCondition` recognized only lines ending exactly at `Then`.
  - Implementation now preserves and lowers trailing `Then statement` / `Else statement` syntax.
  - Mark `[x]` only after the full statement-layout regression passes.

- [ ] `While`, `Do While` and `Do Until` compound comparison conditions using `And` / `Or` must use the same precedence-safe logical-comparison lowering as `If`.
  - Discovered by the same broad layout regression after the single-line `If` fix.
  - Reproducer includes `While i < 2 And total = 3` after `_` continuation normalization.
  - Current runtime diagnostic: `Microsoft.CSharp.RuntimeBinder.RuntimeBinderException: Operator '==' cannot be applied to operands of type 'bool' and 'int'`.
  - Audit trailing `Loop While` / `Loop Until` too if those forms are supported by the compiler.
  - Regression must cover `And` and `Or` and all supported pre-test/post-test loop condition layouts.

## Layout coverage

- [ ] `_` continuation in `Sub` / `Function` parameter lists.
- [ ] `_` continuation in `Property Get/Let/Set` headers.
- [ ] `_` continuation in class inheritance headers.
- [ ] `_` continuation in function/sub calls and argument lists.
- [ ] `_` continuation in `If` expressions including compound comparisons.
- [ ] `_` continuation in `For ... To ... Step` expressions.
- [ ] `_` continuation in `While` and `Do While/Until` conditions.
- [ ] `_` continuation in `Select Case` selector and `Case ... To ...` expressions.
- [ ] `_` continuation in `With` expressions.
- [ ] verify existing multiline native `Declare` regression remains green.
- [ ] audit compact multiple-statements-on-one-line syntax separately; do not silently treat `:` as supported until its grammar and source-line semantics are deliberately defined.

Primary regression source: `samples/statement-layout-audit.xps`.
