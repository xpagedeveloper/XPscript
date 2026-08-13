# XPScript parameterless procedure header TODO

(c) xpagedeveloper.com 2026

Regression discovered by the cross-platform filesystem portability gate while compiling `samples/filesystem-portability-semantics.xps`.

## Bug

- [x] accept parameterless `Sub` declarations without an empty `()` parameter list.
  - Reproducer: `Sub Initialize` followed by a normal body and `End Sub`.
  - Previous diagnostic: `Unsupported module/class declaration: Sub Initialize` at line 4, position 1.
  - `Sub Initialize` is now canonicalized to the same parameterless declaration semantics as `Sub Initialize()`.
- [x] parameterless `Function Name As Type` without `()` is equivalent to `Function Name() As Type`.
- [x] module-level procedures, class methods, `Sub New` and `Sub Delete` use the same optional-empty-parentheses rule where applicable.
- [x] entry-point discovery accepts `Sub Main` as well as `Sub Main()` through line-count-preserving canonicalization before downstream transpilation.
- [x] declaration shorthand does not change call syntax; regression calls the canonicalized procedures/functions normally and existing overload resolution remains downstream of the normalized declaration form.
- [ ] add a dedicated negative regression for malformed/non-empty parameter declarations to prove the canonicalizer never hides parameter syntax errors.
- [x] permanent positive regression coverage: `samples/parameterless-procedure-headers.xps`; manual gate: `Parameterless Procedure Header Compatibility`.

The filesystem fixture remains `Sub Initialize`; the original compiler regression is not hidden by changing it to `Sub Initialize()`.
