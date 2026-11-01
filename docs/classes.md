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

For inherited classes, call the direct base-class constructor explicitly with `Parent.New(...)` as the first statement in the child `Sub New` when the base constructor requires arguments:

```xpscript
Class BasePerson
    Public Name As String

    Public Sub New(ByVal name As String)
        Name = name
    End Sub
End Class

Class Employee Extend BasePerson
    Public Role As String

    Public Sub New(ByVal name As String, ByVal role As String)
        Call Parent.New(name)
        Role = role
    End Sub
End Class
```

`Parent.New(...)` targets the direct base class. The base constructor completes before the remaining child-constructor statements execute. A derived constructor that does not use `Parent.New(...)` relies on the base class having a parameterless constructor.

### AST constructor forwarding contract

In the experimental AST path, `Parent.New(...)` must be the first statement in a
derived `Sub New` and may occur exactly once. It targets only the direct base
class; it does not dispatch virtually and it must not be used from a root class.

Arguments are evaluated once, left to right. Required and trailing Optional
parameters use the same type, ByVal/ByRef, default-value and writable-storage
rules as a direct `New` call. Omitted Optional arguments are forwarded as fresh
storage where the parameter is Optional ByRef; defaults run in declaration order
and may observe earlier arguments. A failed base call preserves the caller's
existing assignment target and cleans only ownership completed by the new
instance's construction path.

The AST compiler rejects late or duplicate `Parent.New` calls, root-class calls,
private/inaccessible base constructors, invalid arity, incompatible argument
types and non-writable ByRef arguments with mapped diagnostics. These rules are
covered by the constructor-parent and constructor-optional fixtures; normal
compiler parity is claimed only for the documented supported subset.

### AST lifecycle migration scope

The current AST lifecycle matrix covers root and derived `Sub New`/`Sub Delete`, scalar ByVal and ByRef parameters, trailing Optional parameters, Object/user-class references, Variant, List, array and the supported native/reference constructor families. It also covers `Parent.New`, derived virtual dispatch, failed-construction cleanup, repeated deletion and the test-only GC/finalizer observation seam.

The supported normal/AST parity claim is intentionally narrower: parity is established for the documented scalar ByVal constructor/destructor fixtures and selected native/reference families. AST capability tests cover Optional and ByRef behavior where the legacy compiler requires different arity or has known runtime limitations; those tests are not presented as legacy parity.

The following remain separate follow-up work and do not change production ownership semantics: Optional/ByRef `XPImage` constructor ownership, whole-array constructor ownership (distinct from array storage), broader GC/derived-method migration boundaries, and platform-specific desktop/mobile lifecycle behavior. The Linux lifecycle workflow verifies the completed image-get, constructor and destructor families as separate steps.


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
