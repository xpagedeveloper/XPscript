# ArraySort

`ArraySort(array)` returns a sorted copy of a one-dimensional typed array.

Supported element types:

- `String`: alphanumeric ascending order. `Option Compare NoCase` makes the comparison case-insensitive.
- `Date`: alphanumeric ascending order using the string representation of each date.
- `Integer`: numeric ascending order.
- `Long`: numeric ascending order.
- `Double`: numeric ascending order.

The returned array preserves the source array element type and lower bound. The source array is not modified.

Multidimensional arrays and unsupported element types raise a runtime error.

```xpscript
Dim values(1 To 4) As Integer
Dim sorted() As Integer

values(1) = 20
values(2) = -5
values(3) = 10
values(4) = 2

sorted = ArraySort(values)
Print Join(sorted, ",")
```

Output:

```text
-5,2,10,20
```
