# Regular expressions

XPScript provides regular-expression helpers in the core runtime. The implementation uses .NET regular expressions with culture-invariant matching and a bounded execution timeout.

## RegexValidate

`RegexValidate(source, regex)` returns `True` when the regular expression matches the source string. It returns `False` when there is no match.

```xpscript
Dim valid As Boolean

valid = RegexValidate("ABC-123", "^[A-Z]+-[0-9]+$")
Print CStr(valid)
```

Parameters:

- `source`: String-compatible value to test.
- `regex`: Regular-expression pattern.

Return value: `Boolean`.

The function tests whether the pattern has a match. Use `^` and `$` when the complete source string must match the expression.

## RegexMatch

`RegexMatch(source, regex)` returns every full regular-expression match as an XPScript `String` array.

```xpscript
Dim matches As Variant

matches = RegexMatch("A12 B345 C7", "[0-9]+")
Print CStr(matches(0))
Print CStr(matches(1))
Print CStr(matches(2))
```

The example returns `12`, `345` and `7`.

The result can also be assigned to a typed dynamic String array:

```xpscript
Dim matches() As String

matches = RegexMatch("X9 Y88", "[0-9]+")
Print matches(0)
Print matches(1)
```

`RegexMatch` returns only full match values. Capture groups are not returned separately. An expression with no matches returns an empty XPScript String array.

The returned value uses normal XPScript array semantics and can be stored in a `Variant`, indexed normally and used with the standard array functions.

## Errors and limits

Patterns are limited to 4096 characters and cannot contain control characters. Invalid patterns raise an XPScript runtime error. Regex execution uses a 250 ms timeout to limit pathological expressions.

## UIForm validation

UIForm uses the same regular-expression model through `SetRegexValidation(fieldName, pattern)` for text-entry fields.

```xpscript
Call form.AddTextField("code", "Code")
Call form.SetRegexValidation("code", "^[A-Z]{3}-[0-9]{3}$")
```

UIForm patterns are limited to 1024 characters. Desktop, server-rendered web and browser-wasm use the configured rule as field validation metadata. Server-side web handling always validates submitted values again before accepting them.

See [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) for executable core examples and [UIForm regex validation](uiform-regex-validation.md) for UIForm-specific details.
