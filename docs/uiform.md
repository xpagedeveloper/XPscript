# UIForm

`UIForm` is the shared XPScript form model for desktop, server-rendered web and browser WebAssembly applications. Fields can bind to a `JsonObject` or object-root `JsonDocument`. The same form definition is rendered by the active UI backend.

## Basic form

```xpscript
Dim data As New JsonObject
Dim form As New UIForm("Customer")
Dim result As String

Call form.BindData(data)
Call form.AddTextField("name", "Name")
Call form.AddTextField("email", "Email")
Call form.AddTextField("company", "Company")
Call form.SetRequired("name", True)

result = form.ShowDialog()
```

The form title belongs to the top-level form container. On the web it is rendered before the field grid.

## Fields

Core field APIs include `AddTextField(name, label)`, `AddTextArea(name, label)`, `AddNumberField(name, label)`, `AddCheckBox(name, label)`, `AddDateField(name, label)`, `AddSelect(name, label)`, `AddRadioGroup(name, label)`, `AddListBox(name, label)` and `AddMultiListBox(name, label)`. `SetRequired(name, True)` marks a field required.

A field name is also its JSON binding key. Creating or rendering an empty field does not by itself create a missing JSON key. A supplied value creates or updates the key.

## ListBox and MultiListBox

`ListBox` allows one selected option. Its bound value is a scalar string.

```xpscript
Call form.AddListBox("country", "Country")
Call form.AddOption("country", "SE")
Call form.AddOption("country", "NO")
Call form.AddOption("country", "DK")
```

`MultiListBox` allows several selected options. Its bound value is a `JsonArray` of strings. Use `GetFieldValues(name)` when you want the selected values as a `JsonArray` regardless of the current backing value.

```xpscript
Dim data As New JsonObject
Dim selected As New JsonArray
Dim values As Variant

Call selected.Add("SE")
Call selected.Add("DK")
Call data.Set("markets", selected)
Call form.BindData(data)

Call form.AddMultiListBox("markets", "Markets")
Call form.AddOption("markets", "SE")
Call form.AddOption("markets", "NO")
Call form.AddOption("markets", "DK")

values = form.GetFieldValues("markets")
Print CStr(values.Count)
```

`AddOption` and `ClearOptions` work with `Select`, `RadioGroup`, `ListBox` and `MultiListBox`. Submitted values are checked against the configured option list before they are written to bound data.

The same ListBox APIs are used on desktop, server-rendered web and browser-wasm. Desktop uses native Avalonia list controls. Server-rendered web uses HTML `select` controls. Browser-wasm uses DOM `select` controls and returns multiple selections as an array.

## Grid layout

The form is the top container. Add a grid with a configurable number of columns:

```xpscript
Dim grid As Variant
Set grid = form.AddGridColumns(12)
```

`12` is the number of columns in a full row. Fields flow from left to right. `SetFieldPosition(fieldName, columns)` only specifies how many grid columns the field consumes. It does not specify absolute coordinates.

```xpscript
Call grid.SetFieldPosition("name", 6)
Call grid.SetFieldPosition("email", 6)
Call grid.SetFieldPosition("company", 8)
Call grid.SetFieldPosition("status", 4)
```

With a 12-column grid this produces two rows: `name 6 + email 6`, then `company 8 + status 4`. If the next field does not fit in the remaining columns, it automatically flows to the next row.

Use `AddNewRow()` only when you want an explicit row break:

```xpscript
Call grid.SetFieldPosition("name", 4)
Call grid.AddNewRow()
Call grid.SetFieldPosition("email", 6)
```

The email field starts a new row even though eight columns remained on the previous row.

## Complete web form

```xpscript
[Anonymous]
[Get]
[Post]
Sub Index()
    Dim data As New JsonObject
    Dim form As New UIForm("Customer")
    Dim grid As Variant
    Dim result As String

    Call form.BindData(data)
    Call form.AddTextField("name", "Name")
    Call form.AddTextField("email", "Email")
    Call form.AddTextField("company", "Company")
    Call form.AddTextField("status", "Status")
    Call form.SetRequired("name", True)

    Set grid = form.AddGridColumns(12)
    Call grid.SetFieldPosition("name", 6)
    Call grid.SetFieldPosition("email", 6)
    Call grid.AddNewRow()
    Call grid.SetFieldPosition("company", 8)
    Call grid.SetFieldPosition("status", 4)

    result = form.ShowDialog()
    If result = "OK" Then
        Response.ContentType = "application/json; charset=utf-8"
        Response.Write(data.Stringify())
    End If
End Sub
```

## Web rendering

Web UIForm uses Bootstrap 5.3.8 styling. The runtime loads the Bootstrap stylesheet once per response and applies Bootstrap form, grid and button classes. The title is outside the field grid so explicit field layout cannot move it below the controls.

On GET, `ShowDialog()` renders the form and returns `Pending`. On POST, it reads submitted values, performs form validation, updates the bound data and returns `OK` when accepted.

HTML labels, names and values are encoded before rendering.

## Desktop rendering

Desktop UIForm uses the same form definition, fields, binding and layout intent. The desktop backend renders native desktop controls instead of Bootstrap HTML. Web-only styling does not change desktop rendering.

## UIListView on web

UIListView web output uses Bootstrap table styling, responsive table wrapping, controls and buttons. UIListView runtime is installed only when a script actually uses UIListView, so a UIForm-only script does not require UIListView runtime structures.
