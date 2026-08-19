# UIForm regex validation

Use `SetRegexValidation(fieldName, pattern)` to require a text-entry field to match a regular expression before the submitted value is accepted.

Supported fields:

- `TextField`
- `TextArea`
- `PasswordField`
- `EmailField`
- `UrlField`

Example:

```xpscript
Dim form As New UIForm("Customer")

Call form.AddTextField("customerCode", "Customer code")
Call form.SetRequired("customerCode", True)
Call form.SetRegexValidation("customerCode", "^[A-Z]{3}-[0-9]{4}$")
Call form.SetFieldPlaceholder("customerCode", "ABC-1234")

Call form.ShowDialog()
```

An empty pattern clears regex validation for the field.

The pattern is validated when `SetRegexValidation` is called. Invalid patterns produce a UIForm runtime error. Patterns are limited to 1024 characters and may not contain control characters.

Submitted non-empty text is checked with .NET regular expressions using culture-invariant matching and a bounded match timeout. The server runtime validates the value again when handling a submitted form, so browser-side validation is never the security boundary.

Regex validation is independent of `SetRequired` and `SetLength`. Empty values are accepted by regex validation unless the field is also required.
