# Evaluate

`Evaluate` executes supported XPScript source dynamically at runtime. Use it when the expression or small code fragment is data rather than normal source known at compile time.

Prefer normal compiled procedures whenever the code is known in advance. Dynamic evaluation adds parsing/runtime work and makes validation more important.

## Basic expression

```xpscript
Sub Main()
    Dim result As Variant
    result = Evaluate("1 + 2")
    Print CStr(result)
End Sub
```

## XPScript evaluation

Evaluation uses XPScript semantics, including supported conversions, operators and runtime helpers. The exact accepted forms are defined by the implemented evaluator rather than by another BASIC dialect.

When evaluated code returns a value, assign the result to a compatible variable or `Variant`.

## Errors

Treat evaluated text as code. Syntax/runtime failures follow XPScript error behavior and should be handled where dynamic input can fail.

```xpscript
Sub Main()
    On Error GoTo Handler
    Dim result As Variant
    result = Evaluate("10 + 20")
    Print CStr(result)
    Exit Sub
Handler:
    Print CStr(Err)
    Print Error$
End Sub
```

## Security

Do not pass untrusted user input directly to `Evaluate`. Validation must constrain the allowed expression/code before evaluation. Prefer normal parameters and data structures when the user is supplying values rather than code.

## Related samples

The repository contains evaluator regression samples including `samples/evaluate-xpscript.xps` and `samples/evaluate-array-helpers.xps`. Use those as executable compatibility references when extending Evaluate.
