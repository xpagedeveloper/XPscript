# XPScript Arrays, Lists and Operators


## Arrays

### Fixed arrays

```xpscript
Dim matrix(1 To 2, 0 To 1) As Long
```

XPScript supports explicit lower and upper bounds and multidimensional arrays.

### Dynamic arrays

```xpscript
Dim names() As String
ReDim names(1 To 2)
```

### ReDim Preserve

```xpscript
ReDim Preserve names(1 To 3)
```

`Preserve` retains existing values subject to XPScript array resizing rules.

### LBound and UBound

```xpscript
Print CStr(LBound(names))
Print CStr(UBound(names))
```

For multidimensional arrays, pass the dimension number.

```xpscript
LBound(matrix, 1)
UBound(matrix, 2)
```

### Array

Creates an array value from arguments.

```xpscript
values = Array(10, 20, 30)
```

### Join

Joins array values into a String.

```xpscript
Print Join(values, "|")
```

### Explode

Splits a String using an explicit delimiter and returns an XPScript array.

```xpscript
parts = Explode(",", "A,B,C")
```

### ArrayAppend

Returns an array with another value or array appended.

```xpscript
result = ArrayAppend(values, "next")
```

### ArrayGetIndex

Returns the index of a matching value, using the active comparison semantics.

```xpscript
index = ArrayGetIndex(values, "five")
```

### ArrayUnique

Returns unique array values.

```xpscript
uniqueValues = ArrayUnique(values)
```

### ArraySlice

Returns a selected range of values.

```xpscript
slice = ArraySlice(values, 1, 4)
```

### ArraySplice

Removes a range from an array and optionally inserts replacement values. It returns the removed values.

```xpscript
removed = ArraySplice(values, 1, 2, "X", "Y")
```

## Lists

Declare a keyed List with:

```xpscript
Dim users List As String
```

Assign values by tag:

```xpscript
users("admin") = "Alice"
```

### IsElement

Checks whether a List element exists.

```xpscript
If IsElement(users("admin")) Then
    Print users("admin")
End If
```

### ForAll

Iterates array/List values.

```xpscript
ForAll value In users
    Print ListTag(value) & ":" & value
End ForAll
```

### ListTag

Returns the tag associated with the current List element during List iteration.

### Erase List element

```xpscript
Erase users("guest")
```

### IsList

Returns True when the supplied value is an XPScript List.

## Comparison behavior

### Option Compare NoCase

```xpscript
Option Compare NoCase
```

Makes supported text comparison operations case-insensitive.

### Like

Supports wildcard pattern matching.

Examples demonstrated in the sample include:

```xpscript
"File123" Like "file###"
"abc" Like "a?c"
"abc5" Like "a*[0-9]"
"abc" Like "a[!d]c"
```

Pattern forms include `*`, `?`, `#` and character classes.

## Arithmetic and logical operators

### Exponentiation

```xpscript
2 ^ 3
```

### Integer division

```xpscript
16.9 \ 5.6
```

### Mod

```xpscript
17 Mod 5
```

### Boolean/bitwise operators

Supported operators demonstrated by the samples:

- `And`
- `Or`
- `Not`
- `Xor`
- `Eqv`
- `Imp`

Boolean operands use logical semantics; numeric operands use integer bitwise semantics where applicable.

## Concatenation and line continuation

`&` is explicit String concatenation.

```xpscript
text = "A" & "B"
```

A trailing `_` continues a statement on the next physical source line.

## Samples

- [samples/operators-arrays.xps](../samples/operators-arrays.xps)
- [samples/lists-classes.xps](../samples/lists-classes.xps)
- [samples/module-arrays.xps](../samples/module-arrays.xps)
- [samples/evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps)
- [samples/evaluate-nested-collections.xps](../samples/evaluate-nested-collections.xps)
