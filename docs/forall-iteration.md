# ForAll iteration

`ForAll` is XPscript's first-class collection iteration statement. The language uses one iteration model across supported collection/runtime types instead of introducing a separate loop form for each API.

```xpscript
ForAll item In collection
    Print item
End ForAll
```

## Supported iterable values

`ForAll` supports:

- one-dimensional XPscript arrays,
- XPscript Lists,
- JSON array/runtime values that expose enumerable semantics,
- database row/result collections that expose enumerable semantics,
- filesystem arrays returned by functions such as `Files()`, `Directories()`, and `ReadLines()`,
- other runtime values that expose the common enumerable contract,
- XPscript class instances that provide a public parameterless `Iterator()` function.

The compiler/runtime routes these through the same `ForAll` enumeration path. APIs should expose enumerable values rather than invent API-specific loop statements.

## XPscript classes

A user class becomes iterable by exposing:

```xpscript
Public Function Iterator() As Variant
```

`Iterator()` must return another supported iterable value. It must not return the object itself.

Example:

```xpscript
Class Words
    Public Function Iterator() As Variant
        Iterator = Array("one", "two", "three")
    End Function
End Class

Sub Main()
    Dim words As Words
    Set words = New Words()

    ForAll word In words
        Print word
    End ForAll
End Sub
```

Class construction still follows the normal XPscript rule: class instances are created with `New`.

## Lists

Lists participate in the common iteration model while retaining XPscript's existing List alias semantics. A `ForAll` alias over a List still supports list-specific behavior such as `ListTag(alias)` and assignment through the alias where supported.

## Arrays

Only one-dimensional arrays are iterable with `ForAll`.

```xpscript
Dim values(2) As Integer
ForAll value In values
    Print value
End ForAll
```

A multidimensional array is not flattened implicitly. Attempting to use it with `ForAll` raises runtime error 13 with a one-dimensional-array diagnostic.

```xpscript
Dim matrix(1, 1) As Integer

' Runtime error: ForAll supports only one-dimensional arrays.
ForAll value In matrix
    Print value
End ForAll
```

Use explicit nested `For` loops when iterating dimensions of a multidimensional array.

## Filesystem collections

Modern filesystem functions return XPscript arrays and therefore use normal `ForAll` syntax:

```xpscript
files = Files("src", "*.xps", True)

ForAll file In files
    Print file
End ForAll
```

No filesystem-specific iteration construct is required.

## Runtime design rule

New collection-like runtime APIs should integrate with the common iterable contract whenever practical. Do not add a new XPscript loop model merely because a backend or runtime type has its own native iterator abstraction.
