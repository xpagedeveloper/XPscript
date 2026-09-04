# UIForm dirty tracking

`UIForm` tracks changes to its bound data against a clean baseline.

## IsDirty

Use `IsDirty` to test whether the form data differs from the baseline.

```xpscript
Dim form As New UIForm("Customer")
Dim data As New XPJsonObject

Call data.Set("name", "Alice")
Call form.AddTextField("name", "Name")
Call form.BindData(data)

Print CStr(form.IsDirty)
' False

Call form.SetFieldValue("name", "Bob")
Print CStr(form.IsDirty)
' True
```

Setting a field to its existing value does not make the form dirty. If a changed value is changed back to its baseline value, the field becomes clean again.

## DirtyFields

`DirtyFields` returns a `XPJsonArray` containing the names of form fields whose current values differ from the baseline.

```xpscript
Call form.SetFieldValue("name", "Bob")

Print CStr(form.DirtyFields.Count)
' 1

Print CStr(form.DirtyFields.Get(0))
' name
```

The list contains each dirty field once and follows the form field order.

## MarkClean

`MarkClean()` makes the current form data the new baseline.

```xpscript
Call form.MarkClean()
Print CStr(form.IsDirty)
' False
```

`BindData()` also establishes a new clean baseline.

## HTTP load and save

`XPHttpClient.LoadForm()` binds the loaded JSON object and therefore leaves the form clean.

A successful `XPHttpClient.SaveForm()` or `XPHttpClient.PutForm()` automatically calls `MarkClean()` after the server returns a successful HTTP status. A failed HTTP response does not clear dirty state.

```xpscript
Dim http As New XPHttpClient
Dim response As XPHttpResponse
Dim form As New UIForm("Customer")

Call form.AddTextField("name", "Name")
Call http.LoadForm(form, "https://api.example.com/customers/42")

Call form.SetFieldValue("name", "New name")

If form.IsDirty Then
    Set response = http.PutForm(form, "https://api.example.com/customers/42")
End If
```
