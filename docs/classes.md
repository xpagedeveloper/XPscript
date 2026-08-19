# Classes and types

XPScript supports reference-oriented `Class` definitions and value-oriented custom `Type` definitions.

## Class

```xpscript
Class Person
    Private mName As String

    Sub New(ByVal name As String)
        Me.mName = name
    End Sub

    Public Function Describe() As String
        Describe = Me.mName
    End Function
End Class

Sub Main()
    Dim p As Person
    Set p = New Person("Alice")
    Print p.Describe()
End Sub
```

`Sub New` is the constructor. `Me` references the current instance. Class variables use reference semantics.

## Set, New and Nothing

```xpscript
Dim first As Person
Dim second As Person
Set first = New Person("Alice")
Set second = first

If second Is Nothing Then
    Print "missing"
End If
```

`Set second = first` aliases the same object. `Is` performs object identity comparison. `Nothing` represents no object reference.

A `Variant` can also receive an object reference with `Set` when runtime typing is required, for example UIForm grid objects.

## Properties

```xpscript
Class Person
    Private mName As String

    Property Get Name As String
        Name = mName
    End Property

    Property Let Name As String
        mName = Name
    End Property
End Class
```

`Property Get` reads a value. `Property Let` handles scalar assignment. `Property Set` handles object/reference-style assignment where applicable. Indexed properties are supported by the implemented indexed-property lowering layer.

## Sub Delete

A class may define `Sub Delete` for XPScript deletion semantics. Do not equate this with .NET garbage-collector timing.

## Type

`Type` defines a value-oriented record:

```xpscript
Type PersonInfo
    Name As String
    Age As Integer
End Type

Sub Main()
    Dim p As PersonInfo
    p.Name = "Alice"
    p.Age = 40
    Print p.Name
End Sub
```

Assignments between custom Type values use value-copy semantics. Supported nested Type values and Type-member arrays are copied independently rather than aliased.

## Enum

```xpscript
Enum BuildState
    BuildUnknown = 0
    BuildReady = 10
    BuildRunning
End Enum
```

Members may have explicit values or continue by incrementing the previous value.

## Module state

Variables declared outside procedures can be `Public` or `Private`. Module storage supports scalar values, arrays, custom Type values and class/object references according to their normal value/reference semantics.

## Related samples

See `samples/lists-classes.xps`, `samples/indexed-properties.xps`, `samples/module-globals.xps`, `samples/module-arrays.xps`, `samples/module-type-values.xps`, `samples/module-object-references.xps` and the Type value-copy samples for executable coverage.
