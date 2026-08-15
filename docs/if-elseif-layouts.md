# If / ElseIf / Else statement layouts

XPScript supports both compact single-line `If` statements and standard block `If / ElseIf / Else / End If` statements.

For a **single-line If**, `End If` is not required:

```xpscript
If a = 1 Then Print "Kalle"
```

```xpscript
If a = 1 Then Print "1" Else Print "2"
```

For a block or multi-line form, use `End If`:

```xpscript
If value = 1 Then
    Print "one"
ElseIf value = 2 Then
    Print "two"
Else
    Print "other"
End If
```

`Then` may also be placed on the next physical line in block syntax:

```xpscript
If value = 1
Then
    Print "one"
ElseIf value = 2
Then
    Print "two"
Else
    Print "other"
End If
```

A branch statement can also remain on the same line as `ElseIf ... Then` while the surrounding construct is a block.

The compiler preserves physical source-line tracking while normalizing supported layouts.
