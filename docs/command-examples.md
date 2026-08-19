# Core command examples

This page supplements [commands.md](commands.md). The command reference is the authoritative list of built-in commands, functions, parameters and links to tested repository samples. The examples below are intentionally small and can be saved as individual `.xps` files and executed with `xpscriptc run file.xps`.

## Option Declare

```xpscript
Option Declare
Sub Main()
    Dim name As String
    name = "XPScript"
    Print name
End Sub
```

## Dim

```xpscript
Sub Main()
    Dim count As Integer
    count = 5
    Print CStr(count)
End Sub
```

## Sub and Call

```xpscript
Sub Hello(ByVal name As String)
    Print "Hello " + name
End Sub
Sub Main()
    Call Hello("Kalle")
End Sub
```

## Function

```xpscript
Function Add(ByVal a As Integer, ByVal b As Integer) As Integer
    Add = a + b
End Function
Sub Main()
    Print CStr(Add(2, 3))
End Sub
```

## ByRef

```xpscript
Sub Increment(ByRef value As Integer)
    value = value + 1
End Sub
Sub Main()
    Dim n As Integer
    n = 1
    Call Increment(n)
    Print CStr(n)
End Sub
```

## Optional

```xpscript
Function Greeting(ByVal name As String, Optional prefix As String = "Hello") As String
    Greeting = prefix + " " + name
End Function
Sub Main()
    Print Greeting("Kalle")
End Sub
```

## Static

```xpscript
Function Counter() As Long
    Static value As Long
    value = value + 1
    Counter = value
End Function
Sub Main()
    Print CStr(Counter())
    Print CStr(Counter())
End Sub
```

## If

```xpscript
Sub Main()
    Dim value As Integer
    value = 10
    If value >= 10 Then
        Print "ten or more"
    Else
        Print "less than ten"
    End If
End Sub
```

## Select Case

```xpscript
Sub Main()
    Dim value As Integer
    value = 2
    Select Case value
    Case 1
        Print "one"
    Case 2
        Print "two"
    Case Else
        Print "other"
    End Select
End Sub
```

## For

```xpscript
Sub Main()
    Dim i As Integer
    For i = 1 To 3
        Print CStr(i)
    Next
End Sub
```

## ForAll

```xpscript
Sub Main()
    Dim values List As String
    Dim item As Variant
    values("first") = "A"
    values("second") = "B"
    ForAll item In values
        Print CStr(item)
    End ForAll
End Sub
```

## GoTo

```xpscript
Sub Main()
    GoTo Done
    Print "not printed"
Done:
    Print "done"
End Sub
```

## GoSub and Return

```xpscript
Sub Main()
    GoSub Worker
    Print "back"
    Exit Sub
Worker:
    Print "worker"
    Return
End Sub
```

## On Error and Resume Next

```xpscript
Sub Main()
    On Error Resume Next
    Error 1001, "example"
    Print "continued"
End Sub
```

## Error

```xpscript
Sub Main()
    On Error GoTo Handler
    Error 1001, "example"
    Exit Sub
Handler:
    Print CStr(Err)
    Print Error$
End Sub
```

## With

```xpscript
Class Person
    Public Name As String
End Class
Sub Main()
    Dim p As Person
    Set p = New Person()
    With p
        .Name = "Kalle"
        Print .Name
    End With
End Sub
```

## ReDim

```xpscript
Sub Main()
    Dim values() As String
    ReDim values(2)
    values(0) = "A"
    Print values(0)
End Sub
```

## ReDim Preserve

```xpscript
Sub Main()
    Dim values() As String
    ReDim values(1)
    values(0) = "A"
    ReDim Preserve values(2)
    Print values(0)
End Sub
```

## Erase

```xpscript
Sub Main()
    Dim values() As String
    ReDim values(2)
    Erase values
    Print "array erased"
End Sub
```

## Set and New

```xpscript
Class Person
    Public Name As String
End Class
Sub Main()
    Dim p As Person
    Set p = New Person()
    p.Name = "Kalle"
    Print p.Name
End Sub
```

For string, date, array, filesystem, JSON, HTTP, process and other built-in functions, use [commands.md](commands.md). Each reference entry links to a repository sample that exercises that API.
