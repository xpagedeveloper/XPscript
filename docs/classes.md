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

## Default visibility and Option Public

Without an explicit visibility modifier, variables, procedures and properties are `Private` by default.

`Option Public` changes that default for declarations that do not already specify `Public` or `Private`:

```xpscript
Option Public

Class Person
    Name As String

    Property Get DisplayName As String
        DisplayName = Name
    End Property
End Class
```

Here both `Name` and `DisplayName` are public. An explicit modifier always wins:

```xpscript
Option Public

Class Person
    Private InternalId As String
    Public Name As String
End Class
```

The compiler entry points `Sub Main` and `Sub Initialize` remain callable by the generated host even when source declarations otherwise default to private.

A class cannot declare a field and a property with the same name, regardless of declaration order or letter casing. For example, this is a compile-time error:

```xpscript
Class InvalidPerson
    Public Name As String

    Public Property Get Name As String
        Name = "invalid"
    End Property
End Class
```

`Property Get` and `Property Let`/`Property Set` with the same name are the two accessors of one property and are therefore allowed.

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

    Public Property Get Name As String
        Name = mName
    End Property

    Public Property Let Name As String
        mName = UCase(Trim(Name))
    End Property
End Class
```

`Property Get` reads a value by executing the code in the getter. `Property Let` handles scalar assignment and executes the setter code instead of writing directly to a backing field. `Property Set` handles object/reference-style assignment where applicable. Indexed properties are supported by the implemented indexed-property lowering layer.

The generated CLR property delegates to the XPscript accessor procedures, so code inside getters and setters is preserved for ordinary property access, JSON conversion and REST model binding.

For JSON serialization, public fields and public readable properties are included. Private fields and properties are excluded. A readable property is serialized by invoking its `Property Get` code. A write-only property is not emitted in JSON output. During JSON deserialization/model binding, a public writable property is assigned through its setter, so validation or transformation code in `Property Let`/`Property Set` is executed.

For example:

```xpscript
Class User
    Private mName As String

    Public Id As Integer

    Public Property Get Name As String
        Name = "USER-" & mName
    End Property

    Public Property Let Name As String
        mName = UCase(Trim(Name))
    End Property
End Class
```

After assigning `user.Name = " anna "`, JSON serialization exposes the public API of the class and produces data equivalent to:

```json
{
  "id": 7,
  "name": "USER-ANNA"
}
```

The private backing variable `mName` is never serialized.

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

Variables declared outside procedures can be `Public` or `Private`. Without `Option Public`, declarations with no explicit modifier default to `Private`; with `Option Public`, they default to `Public`. Explicit `Public` or `Private` always takes precedence. Module storage supports scalar values, arrays, custom Type values and class/object references according to their normal value/reference semantics.

## Related samples

- [lists-classes.xps](../samples/lists-classes.xps)
- [indexed-properties.xps](../samples/indexed-properties.xps)
- [module-globals.xps](../samples/module-globals.xps)
- [module-arrays.xps](../samples/module-arrays.xps)
- [module-type-values.xps](../samples/module-type-values.xps)
- [module-object-references.xps](../samples/module-object-references.xps)
