# Utility functions

## FileExists

`FileExists(path)` returns `True` when `path` identifies an existing regular file. It returns `False` when the file does not exist.

```xps
Dim found As Boolean
found = FileExists("./data/input.txt")
```

## DirExists

`DirExists(path)` returns `True` when `path` identifies an existing directory. It returns `False` when the directory does not exist.

```xps
Dim found As Boolean
found = DirExists("./data")
```

## StrTemplate

`StrTemplate(template, values)` replaces numbered placeholders with values from a one-dimensional array or list.

```xps
Dim arr(1) As String
arr(0) = "fredrik"
arr(1) = "52"

Dim text As String
text = StrTemplate("Hello {0}, you are {1} years old.", arr)
```

The result is:

```text
Hello fredrik, you are 52 years old.
```

Placeholders use the numeric array index. The same placeholder may be used more than once.

Escape a literal opening or closing brace with a backslash:

```xps
text = StrTemplate("Literal \{0\}, real value {0}, literal closing \}.", arr)
```

This produces:

```text
Literal {0}, real value fredrik, literal closing }.
```

Only `{number}` is treated as a placeholder. Other braced text is left unchanged. An out-of-range placeholder raises an XPScript runtime error.
