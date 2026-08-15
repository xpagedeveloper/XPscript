# XPScript Console, Process, Environment and Formatting

> For compact command syntax, parameters and examples, see the [Command Reference](command-reference.md).


## Print

Writes text to standard output.

```xpscript
Print "Hello"
```

A blank `Print` writes a newline.

## Print$

Compatibility form for console text output.

```xpscript
Print$ "READY"
```

## Input

Reads interactive console input, optionally with a prompt.

```xpscript
Dim answer As String
Input "NAME>", answer
```

## Input$

Interactive console-input form. This is distinct from file `Input$(count, #fileNumber)`.

```xpscript
Input$ answer
```

## Pause

Waits for interactive confirmation/input before continuing.

```xpscript
Pause
```

## Command

Returns the program command-line arguments as the runtime command String.

```xpscript
If Command = "hold-lock" Then
    Print "special mode"
End If
```

## Environ

Returns an environment-variable value.

```xpscript
pathValue = Environ("PATH")
```

## Sleep

Suspends execution for the requested duration according to the runtime Sleep API.

```xpscript
Sleep 1
```

## Shell

Starts an external process/command and returns the runtime result identifier/code defined by the Shell implementation.

```xpscript
result = Shell("command")
```

For portable Shell usage, prefer checking `Platform()` and see [docs/platform-native.md](platform-native.md).

## MessageBox

Displays a message/confirmation surface where supported by the runtime implementation.

```xpscript
answer = MessageBox("Message", 0, "XPScript")
```

## InputBox

Prompts for a String value where the runtime/UI environment supports interactive dialogs.

```xpscript
value = InputBox("prompt", "title", "default")
```

## Beep

Requests an audible notification where supported.

```xpscript
Beep
```

## Stop

Stops/breaks execution according to the runtime compatibility semantics.

## Format

Formats a value using a named/custom format.

```xpscript
text = Format(0.125, "Percent")
```

## Format$

String-returning compatibility form.

```xpscript
text = Format$(12.5, "Fixed")
```

## FormatNumber

Formats a number with requested decimal precision.

```xpscript
text = FormatNumber(12.5, 2)
```

## FormatPercent

Formats a numeric value as a percentage.

```xpscript
text = FormatPercent(0.125, 1)
```

## Error / Error$

`Error(number)` or `Error$(number)` returns the standard description for an error number. Without an argument, `Error$` represents the active error description.

See the [XPScript Error Codes](error-codes.md) reference for every public numeric runtime error code and its meaning.

## Console and file input are separate

These are different operations:

```xpscript
Input "NAME>", answer
Input$ answer
```

versus:

```xpscript
part = Input$(3, #f)
```

The latter reads from an open file handle.

## Samples

- [samples/textio-console.xps](../samples/textio-console.xps)
- [samples/platform-shell.xps](../samples/platform-shell.xps)
- [samples/runtime-sax.xps](../samples/runtime-sax.xps) — contains additional legacy compatibility coverage; use the standalone functions documented above for new XPScript programs
- [samples/file-io-extensions.xps](../samples/file-io-extensions.xps)
