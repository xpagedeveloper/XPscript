# XPScript parameterless procedure header TODO

(c) xpagedeveloper.com 2026

Regression discovered by the cross-platform filesystem portability gate while compiling `samples/filesystem-portability-semantics.xps`.

## Bug

- [ ] accept parameterless `Sub` declarations without an empty `()` parameter list.
  - Reproducer: `Sub Initialize` followed by a normal body and `End Sub`.
  - Actual diagnostic: `Unsupported module/class declaration: Sub Initialize` at line 4, position 1.
  - Expected: `Sub Initialize` is equivalent to `Sub Initialize()` and compiles as a parameterless procedure.
- [ ] verify parameterless `Function Name As Type` without `()` is equivalent to `Function Name() As Type`.
- [ ] verify module-level procedures, class methods, `Sub New` and `Sub Delete` use the same optional-empty-parentheses rule where applicable.
- [ ] verify entry-point discovery accepts `Sub Main` as well as `Sub Main()`.
- [ ] calls remain unambiguous: declaration syntax without `()` must not change normal call syntax or overload resolution.
- [ ] preserve clear diagnostics for malformed/non-empty parameter declarations.
- [ ] add permanent regression coverage before marking these items `[x]`.

The filesystem fixture must remain as `Sub Initialize`; do not hide this compiler regression by changing it to `Sub Initialize()`.
