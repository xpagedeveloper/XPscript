# If / ElseIf / Else statement layouts

XPScript accepts `If`, `ElseIf`, `Else` and `End If` in standard block layouts, compact single-line layouts, and mixed layouts where `Then` or the first branch statement moves to another physical line.

Examples:

```xpscript
If value = 1 Then Print "one" ElseIf value = 2 Then Print "two" Else Print "other" End If
```

```xpscript
If value = 1 Then
    Print "one"
ElseIf value = 2 Then
    Print "two"
Else
    Print "other"
End If
```

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

The compiler preserves physical source-line tracking while normalizing these layouts.
