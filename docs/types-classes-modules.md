# XPScript Types, Classes, Properties and Module State

> For compact command syntax, parameters and examples, see the [Command Reference](command-reference.md).

This page documents features demonstrated by the Type, Class, property and module-level samples.

## Enum

```xpscript
Enum BuildState
    BuildUnknown = 0
    BuildReady = 10
    BuildRunning
    BuildDone = 20
End Enum
```

Enum members may use explicit values or automatic incrementing. Qualified access is supported:

```xpscript
state = BuildState.BuildDone
```

## Type ... End Type

Custom Types are value-oriented records.

```xpscript
Type PersonInfo
    Name As String
    Age As Integer
End Type
```

```xpscript
Dim person As PersonInfo
person.Name = "Alice"
person.Age = 40
```

Assignments between Type values use value-copy semantics. Nested Types and Type-member arrays are deep-copied by the implemented runtime path so destination storage is independent.

### Type arrays

Type fields may contain fixed or dynamic arrays. `ReDim`, bounds, indexing and `Erase` are supported for dynamic Type array members.

### Nested Type copy

Nested Type assignments recursively copy nested Type values and array storage.

### Cyclic Type declarations

Type graphs that would require an unbounded recursive value copy are rejected with a compiler diagnostic.

### Option Base and Type arrays

Implicit lower bounds used by `ReDim` on Type-member arrays follow active `Option Base`.

## Class

Classes provide reference semantics.

```xpscript
Class Person
    Private mName As String

    Sub New(name As String)
        Me.mName = name
    End Sub

    Public Function Describe() As String
        Describe = Me.mName
    End Function
End Class
```

## New and Set

```xpscript
Dim p As Person
Set p = New Person("Alice")
```

Assigning another object variable with `Set` aliases the same object reference.

```xpscript
Dim q As Person
Set q = p
```

## Me

`Me` refers to the current class instance.

## Sub New

`Sub New` is the constructor invoked by `New ClassName(...)`.

## Sub Delete

A class may define `Sub Delete`. The current XPScript object/runtime layer invokes its deletion semantics according to the implemented object model. `Delete` is distinct from normal .NET garbage-collection timing.

## Nothing and Is

`Nothing` represents the absence of an object reference.

```xpscript
If p Is Nothing Then
    Print "no object"
End If
```

`Is` performs object identity comparison rather than value comparison.

## Property Get

```xpscript
Property Get Name As String
    Name = mName
End Property
```

## Property Let

Used for scalar property assignment.

```xpscript
Property Let Name As String
    mName = Name
End Property
```

## Property Set

Used for object/reference-style property assignment where applicable.

## Indexed properties

Parameterized Property Get/Let/Set forms are supported by the indexed-property lowering layer.

The positive sample demonstrates parameterized access while [samples/indexed-properties-error.xps](../samples/indexed-properties-error.xps) demonstrates type diagnostics.

## Module-level variables

Module-level variables are declared outside procedures.

```xpscript
Public Counter As Long
Private InternalName As String
```

Implemented module-level storage includes:

- Public/Private scalar values
- arrays
- custom Type values
- class/object references

## Module arrays

Module arrays support fixed/dynamic declaration, `ReDim`, `ReDim Preserve`, indexed reads/writes, bounds and `Erase`.

## Module Type values

Module-level custom Type instances use the same value-copy rules as local Type values. Nested Type assignment uses copy-then-commit behavior to avoid aliasing and self-assignment corruption.

## Module object references

Module-level class references support `Set`, `New`, aliases, `Nothing`, member access and `Delete`.

## Samples

- [samples/language-extensions.xps](../samples/language-extensions.xps)
- [samples/lists-classes.xps](../samples/lists-classes.xps)
- [samples/indexed-properties.xps](../samples/indexed-properties.xps)
- [samples/indexed-properties-error.xps](../samples/indexed-properties-error.xps)
- [samples/module-globals.xps](../samples/module-globals.xps)
- [samples/module-arrays.xps](../samples/module-arrays.xps)
- [samples/module-type-values.xps](../samples/module-type-values.xps)
- [samples/module-object-references.xps](../samples/module-object-references.xps)
- [samples/type-value-copy.xps](../samples/type-value-copy.xps)
- [samples/type-array-members.xps](../samples/type-array-members.xps)
- [samples/type-array-option-base.xps](../samples/type-array-option-base.xps)
- [samples/type-nested-value-copy.xps](../samples/type-nested-value-copy.xps)
- [samples/module-nested-type-value-copy.xps](../samples/module-nested-type-value-copy.xps)
- [samples/type-cycle-error.xps](../samples/type-cycle-error.xps)
