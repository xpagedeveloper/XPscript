# XPScript programming language

XPScript uses BASIC-style syntax. Statements are line-oriented and source files use `.xps`.

## Program entry point

```xpscript
Sub Main()
    Print "Hello"
End Sub
```

For web files, the dispatcher invokes a route procedure such as `Sub Index()` instead of `Main()`.

## Variables

```xpscript
Dim name As String
Dim count As Integer
Dim total As Double
Dim enabled As Boolean
Dim value As Variant
Dim item As Object
```

Use `Option Declare` in application code when you want undeclared names to be compiler errors.

## Assignment and conversion

```xpscript
Dim name As String
Dim count As Integer
name = "Items: " + 5
count = CInt("10")
```

XPScript intentionally supports several forgiving conversions. Do not depend on implicit conversion when ambiguity would make code harder to maintain. Use `CStr`, `CInt`, `CLng`, `CDbl`, `CDate` and related conversion functions at API boundaries.

## Procedures

```xpscript
Sub ShowMessage(ByVal text As String)
    Print text
End Sub

Function Add(ByVal a As Integer, ByVal b As Integer) As Integer
    Add = a + b
End Function
```

`ByRef` lets a procedure modify the caller's variable. `ByVal` passes a value copy. Optional trailing parameters can use `Optional` and a default value.

## Conditions

```xpscript
If count > 10 Then
    Print "large"
ElseIf count > 0 Then
    Print "small"
Else
    Print "empty"
End If
```

`Select Case` is useful for multiple branches.

## Loops

```xpscript
Dim i As Integer
For i = 1 To 10
    Print CStr(i)
Next
```

Use `ForAll` for supported list/collection iteration.

## Arrays and Lists

```xpscript
Dim values() As String
ReDim values(2)
values(0) = "A"
values(1) = "B"
values(2) = "C"
```

`Option Base 0` or `Option Base 1` controls implicit lower bounds where the syntax permits it. Use `LBound` and `UBound` instead of assuming a bound in reusable code.

## Error handling

```xpscript
Sub Main()
    On Error GoTo Handler
    Error 1001, "Example error"
    Exit Sub
Handler:
    Print CStr(Err)
    Print Error$
End Sub
```

`Erl` reports the physical XPScript source line where source-line tracking is available. Included files retain their own source filename and line numbering in diagnostics.

## Objects

```xpscript
Dim person As Person
Set person = New Person("Alice")
If person Is Nothing Then
    Print "No object"
End If
```

Object assignment uses `Set`. Classes use reference semantics. Custom `Type` values use value semantics.

## Includes

When using include functionality, treat the included file as a separate source unit for diagnostics. Compiler errors should identify the include filename and its line inside that file.

## Coding recommendations

Prefer `Option Declare`. Keep procedures focused. Convert external input explicitly. Validate web input before use. Use `LBound`/`UBound` for arrays. Handle expected failures with `On Error`. Keep filesystem and web-root boundaries explicit. Do not put secrets directly in source files.

See [Command reference](commands.md) for supported statements and runtime functions, [Classes](classes.md) for the object model and [Web programming](web.md) for HTTP code.
