# UIForm

(c) xpagedeveloper.com 2026

`UIForm` provides one form model for standalone/desktop execution and XPScript web execution. The form data model is backed by `JsonObject` or a `JsonDocument` whose root is an object.

## Core form

```xps
Dim data As New JsonObject
Dim form As New UIForm("Contact", 640, 480, True)

Call form.BindData(data)
Call form.AddTextField("name", "Name")
Call form.AddTextArea("notes", "Notes")
Call form.AddNumberField("age", "Age")
Call form.AddCheckBox("active", "Active")
Call form.AddDateField("birthday", "Birthday")
Call form.SetRequired("name", True)
```

Form properties include `Title`, `Width`, `Height`, `Resizable`, `HasExplicitSize`, `Data` and `FieldCount`.

## JSON binding

A field name is also its JSON key.

If the JSON object already contains the key, the current JSON value is loaded into the field.

If the key does not exist, the field starts empty. Merely creating or rendering the field does not add the key to JSON.

If a missing field is submitted/set with an empty value, the key remains absent.

When the user supplies a real value, the key is created in the same bound JSON object.

If a key already exists and the user clears it, the key remains present with the cleared representation.

Example:

```xps
Dim data As New JsonObject
Dim form As New UIForm("Example")

Call form.BindData(data)
Call form.AddTextField("name", "Name")

Print CStr(data.Contains("name"))
' False

Call form.SetFieldValue("name", "")
Print CStr(data.Contains("name"))
' False

Call form.SetFieldValue("name", "Kalle")
Print CStr(data.Contains("name"))
' True
```

## Control types

`AddTextField(name, label)` stores string values.

`AddTextArea(name, label)` stores string values and renders as a multiline control on the web.

`AddNumberField(name, label)` parses submitted web values using invariant numeric syntax and stores a JSON number.

`AddCheckBox(name, label)` stores a JSON Boolean when a value has been supplied. A missing unchecked field remains absent. An existing unchecked field is updated to `false`.

`AddDateField(name, label)` accepts the web `yyyy-MM-dd` date representation and stores that ISO date string in JSON.

`SetRequired(name, True)` marks a field as required. Web rendering emits the `required` attribute and server-side submit validation rejects an empty required value.

## Web execution

The same UIForm API runs through Kestrel, CGI and FastCGI.

On a GET request, `ShowDialog()` renders the form into the current XPScript response and returns `Pending`.

On a POST request, `ShowDialog()` reads the submitted fields, updates the same bound JSON object and returns `OK`.

Typical web script:

```xps
[Anonymous]
[Get]
[Post]
Sub Index()
    Dim data As New JsonObject
    Dim form As New UIForm("Contact")
    Dim result As String

    Call form.BindData(data)
    Call form.AddTextField("name", "Name")
    Call form.AddNumberField("age", "Age")

    result = form.ShowDialog()
    If result = "OK" Then
        Response.ContentType = "application/json; charset=utf-8"
        Response.Write(data.Stringify())
    End If
End Sub
```

HTML labels, names and values are encoded before rendering.

## Desktop execution

The form model, fields and JSON binding are shared with the future desktop backend. Native desktop rendering is a separate backend and is not part of the current core implementation.
