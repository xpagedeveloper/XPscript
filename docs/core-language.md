# XPScript Core Language

> For compact command syntax, parameters and examples, see the [Command Reference](command-reference.md).


## Options

### Option Declare

Requires variables to be declared before use.

```xpscript
Option Declare
```

### Option Base

Controls the implicit lower bound of arrays when no lower bound is specified.

```xpscript
Option Base 1
Dim values(10) As Integer
```

### DefInt

Defines the default type for variable names beginning with selected letters.

```xpscript
DefInt A-C
```

## Variables

```xpscript
Dim count As Integer
Dim name As String
Dim amount As Currency
Dim whenValue As Date
Dim data As Variant
Dim item As Object
```

Supported scalar types include Variant, Boolean, Byte, Integer, Long, Single, Double, Currency, String, Date and Object.

## Sub and Function

```xpscript
Sub ShowValue(value As Integer)
    Print CStr(value)
End Sub

Function AddValues(a As Integer, b As Integer) As Integer
    AddValues = a + b
End Function
```

Use `Call` when invoking a Sub explicitly.

```xpscript
Call ShowValue(10)
```

## ByRef and ByVal

`ByRef` allows a procedure to modify the caller's variable.

```xpscript
Sub Increment(ByRef value As Long)
    value = value + 1
End Sub
```

`ByVal` passes a value without exposing the caller's variable for replacement.

## Optional parameters

```xpscript
Function MakeGreeting(name As String, Optional prefix As String = "Hello") As String
    MakeGreeting = prefix & " " & name
End Function
```

Calls may omit trailing optional parameters.

## Static variables and procedures

A local `Static` variable keeps its value between calls.

```xpscript
Function Counter() As Long
    Static count As Long
    count = count + 1
    Counter = count
End Function
```

A `Static Function` has its own static procedure semantics as demonstrated in [samples/core-language.xps](../samples/core-language.xps).

## If / ElseIf / Else

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

## Select Case

Supported forms include literal values, ranges and relational cases.

```xpscript
Select Case value
Case 1
    Print "one"
Case 2 To 10
    Print "range"
Case Is > 10
    Print "high"
Case Else
    Print "other"
End Select
```

## GoTo, GoSub and Return

Labels may be used with `GoTo` and `GoSub`.

```xpscript
GoSub Worker
GoTo Done

Worker:
    Print "worker"
    Return

Done:
```

## Error handling

### On Error GoTo

```xpscript
On Error GoTo Handler
Error 123, "example"
Exit Sub

Handler:
    Print CStr(Err)
    Print Error$
```

### Resume

`Resume` retries the failing statement.

### Resume Next

`Resume Next` continues with the statement after the failing statement.

### Resume label

```xpscript
Handler:
    Resume RecoveryPoint

RecoveryPoint:
```

### On Error Resume Next

```xpscript
On Error Resume Next
```

### On Error GoTo 0

Disables the active error handler.

### Err, Error, Error$ and Erl

- `Err` returns the current error number.
- `Error(number)` returns the standard description for an error number.
- `Error$` returns the current error description.
- `Erl` returns the physical XPScript source line associated with the captured error where source-line tracking is available.

See:

- [samples/core-language.xps](../samples/core-language.xps)
- [samples/erl-physical-source-line.xps](../samples/erl-physical-source-line.xps)
- [samples/nested-resume-targets.xps](../samples/nested-resume-targets.xps)

## With

`With` shortens repeated member access.

```xpscript
With person
    .Name = "Alice"
    Print .Name
End With
```

## Native Declare

Native functions can be declared with `Declare Function` or `Declare Sub`.

```xpscript
Declare Function GetTickCount Lib "kernel32.dll" Alias "GetTickCount" () As Long
```

For portable native declarations, see [docs/platform-native.md](platform-native.md).

## Samples

- [samples/hello.xps](../samples/hello.xps)
- [samples/functions.xps](../samples/functions.xps)
- [samples/core-language.xps](../samples/core-language.xps)
- [samples/language-extensions.xps](../samples/language-extensions.xps)
- [samples/compiler-errors.xps](../samples/compiler-errors.xps)
- [samples/reserved-identifier-error.xps](../samples/reserved-identifier-error.xps)
- [samples/reserved-runtime-type-error.xps](../samples/reserved-runtime-type-error.xps)
- [samples/erl-physical-source-line.xps](../samples/erl-physical-source-line.xps)
- [samples/nested-resume-targets.xps](../samples/nested-resume-targets.xps)
