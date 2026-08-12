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

- [ ] single-line `If` / inline `ElseIf` with compound comparison conditions using `And` / `Or` must use the same logical-comparison lowering as block `If ... Then` headers.
  - Discovered after the continued-header fix allowed the audit sample to execute.
  - Reproducer: `If total = 3 And total > 0 Then Print "..."`.
  - Current failure: generated/runtime expression can compare incompatible values such as `int == bool` because `RewriteLogicalComparisonCondition` currently recognizes only lines ending exactly at `Then`.
  - Regression must cover both block and single-line conditions and preserve trailing `Then statement` / `Else statement` syntax.

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
