from pathlib import Path

# Update TODO while preserving the complete file.
p = Path('todo/runtime-reference-todo.md')
text = p.read_text(encoding='utf-8')
old = '''- [ ] support all valid `If` statement layouts consistently:
  - [ ] single-line `If condition Then statement`
  - [ ] single-line branches such as `If condition Then statement Else statement` and applicable `ElseIf condition Then statement` forms
  - [ ] `If condition Then` followed by statement(s) and `End If` on a later line
  - [ ] fully multiline block form with `If`, `Then`, body and `End If` on separate lines
  - [ ] ensure Date/comparison lowering and other preprocessors preserve single-line `If ... Then ...` syntax instead of producing `Unsupported statement` diagnostics; regression discovered by `examples/date-comparisons.xps` testing
'''
new = '''- [x] support all valid `If` statement layouts consistently; source: `samples/if-layouts.xps`; permanent manual gate: `Control Flow and Error Handling Compatibility`:
  - [x] single-line `If condition Then statement`
  - [x] single-line branches such as `If condition Then statement Else statement` and block `ElseIf condition Then statement` forms
  - [x] `If condition Then` followed by statement(s) and `End If` on a later line
  - [x] split `If condition` / `Then` and `ElseIf condition` / `Then` forms while preserving physical source line count
  - [x] fully multiline block form with `If`, `Then`, body and `End If` on separate lines
  - [x] Date/comparison lowering and other preprocessors preserve single-line `If ... Then ...` syntax instead of producing `Unsupported statement` diagnostics; original regression discovered by `examples/date-comparisons.xps` testing
'''
if old not in text:
    raise SystemExit('If TODO block not found')
text = text.replace(old, new, 1)
p.write_text(text, encoding='utf-8')

# Document the supported layouts.
p = Path('docs/core-language.md')
text = p.read_text(encoding='utf-8')
old = '''## If / ElseIf / Else

```xpscript
If value > 10 Then
    Print "high"
ElseIf value > 0 Then
    Print "positive"
Else
    Print "zero or negative"
End If
```
'''
new = '''## If / ElseIf / Else

XPScript supports both single-line and block `If` statements.

```xpscript
If value > 10 Then Print "high"
If value = 10 Then Print "ten" Else Print "not ten"
```

Normal block form is supported:

```xpscript
If value > 10 Then
    Print "high"
ElseIf value > 0 Then
    Print "positive"
Else
    Print "zero or negative"
End If
```

For layout-sensitive source, `Then` may also be placed on the following physical line. The compiler normalizes this form without changing the physical source-line count used by source tracking:

```xpscript
If value > 10
Then
    Print "high"
End If
```

The same split `Then` form is supported for `ElseIf`. An `ElseIf` branch may also place its first statement after `Then` on the same line while the surrounding `If` remains a block.
'''
if old not in text:
    raise SystemExit('Core-language If section not found')
text = text.replace(old, new, 1)
p.write_text(text, encoding='utf-8')
