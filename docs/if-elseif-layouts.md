# If and ElseIf layouts

XPscript supports both block and single-line `If` forms.

A single-line `If` has a statement after `Then` on the same logical source line. It does not use `End If`.

```xpscript
If ready Then Print "ready"
If failed Then Error 1001
```

`Error` follows the same rule as other single-line statements. The following form is valid and does not require `End If`:

```xpscript
If invalid Then Error 1001, "Invalid value"
```

A block `If` is selected when `Then` is the last token on the logical line. Whitespace and a trailing comment do not count as a statement. A block `If` requires `End If`.

```xpscript
If invalid Then
    Error 1001, "Invalid value"
End If

If invalid Then    ' validation block
    Error 1001, "Invalid value"
End If
```

The distinction is therefore based on code after `Then`, excluding whitespace and comments. If code follows `Then`, XPscript uses the single-line form. If no code follows `Then`, XPscript uses the block form.

See [`samples/if-layouts.xps`](../samples/if-layouts.xps) for executable coverage of the supported layouts.
