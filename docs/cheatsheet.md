# XPScript cheat sheet

A compact reference for common XPScript syntax, runtime patterns and CLI commands. Use the full references when exact overloads, limits or platform-specific behavior matter.

## Minimal program

```xpscript
Option Declare

Sub Main()
    Dim message As String
    message = "Hello from XPScript"
    Print message
End Sub
```

Run or compile:

```text
xpscript run app.xps
xpscript compile app.xps -o app
xpscriptc app.xps -o app
```

Target a runtime:

```text
xpscript compile app.xps --platform win-x64 -o app.exe
xpscript compile app.xps --platform linux-x64 -o app
xpscript compile app.xps --platform osx-arm64 -o app
```

Common deployment RIDs are `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64`.

## Variables and types

```xpscript
Dim name As String
Dim count As Integer
Dim total As Double
Dim enabled As Boolean
Dim value As Variant
Dim item As Object
```

Prefer `Option Declare`.

Conversions:

```xpscript
name = CStr(123)
count = CInt("10")
total = CDbl("12.5")
```

## Strings

Concatenate with `&` when the intent is string composition:

```xpscript
Print "Name: " & name
```

## Procedures and functions

```xpscript
Sub ShowMessage(ByVal text As String)
    Print text
End Sub

Function Add(ByVal a As Integer, ByVal b As Integer) As Integer
    Add = a + b
End Function
```

Optional parameter:

```xpscript
Function Greeting(name As String, Optional prefix As String = "Hello") As String
    Greeting = prefix & " " & name
End Function
```

Use `Call` for procedure calls when following the standard XPScript style:

```xpscript
Call ShowMessage("Hello")
```

## Conditions

```xpscript
If count > 10 Then
    Print "large"
ElseIf count > 0 Then
    Print "small"
Else
    Print "empty"
End If
```

## Loops

```xpscript
Dim i As Integer

For i = 1 To 10
    Print CStr(i)
Next
```

For supported iterable values, use `ForAll`:

```xpscript
ForAll item In items
    Print CStr(item)
End ForAll
```

## Arrays

```xpscript
Dim values() As String

ReDim values(2)
values(0) = "A"
values(1) = "B"
values(2) = "C"

Print CStr(LBound(values))
Print CStr(UBound(values))
```

Use `LBound` and `UBound` instead of assuming the lower bound.

## Objects

```xpscript
Dim person As Person

Set person = New Person("Alice")

If person Is Nothing Then
    Print "No object"
End If
```

Object assignment uses `Set`.

## Classes

```xpscript
Class Customer
    Public Name As String

    Public Function DisplayName() As String
        DisplayName = Name
    End Function
End Class
```

## Error handling

```xpscript
Sub Main()
    On Error GoTo Handler

    Error 1001, "Example error"
    Exit Sub

Handler:
    Print CStr(Err)
    Print Error$
End Sub
```

## JSON

Create an object:

```xpscript
Dim data As New XPJsonObject

Call data.Set("name", "Ada")
Call data.Set("enabled", True)

Print data.Stringify()
```

Array:

```xpscript
Dim items As New XPJsonArray

Call items.Add("one")
Call items.Add("two")
```

## UIForm

The same UIForm model is used by desktop, server-rendered web and browser WebAssembly.

```xpscript
Dim data As New XPJsonObject
Dim form As New UIForm("Customer")
Dim result As String

Call form.BindData(data)
Call form.AddTextField("name", "Name")
Call form.AddEmailField("email", "Email")
Call form.SetRequired("name", True)

result = form.ShowDialog()
```

12-column layout:

```xpscript
Dim grid As Variant

Set grid = form.AddGridColumns(12)
Call grid.SetFieldPosition("name", 6)
Call grid.SetFieldPosition("email", 6)
```

Common fields include `AddTextField`, `AddTextArea`, `AddNumberField`, `AddCheckBox`, `AddDateField`, `AddSelect`, `AddRadioGroup`, `AddListBox` and `AddMultiListBox`.

## Desktop UI

```xpscript
Dim result As Integer

result = MsgBox("Continue?", 4, "XPScript")
```

`MsgBox` button groups:

| Value | Buttons |
|---|---|
| 0 | OK |
| 1 | OKCancel |
| 2 | AbortRetryIgnore |
| 3 | YesNoCancel |
| 4 | YesNo |
| 5 | RetryCancel |

Return values include `1=OK`, `2=Cancel`, `3=Abort`, `4=Retry`, `5=Ignore`, `6=Yes`, `7=No`.

## Web route

```xpscript
[Anonymous]
[Get:/api/ping]
Function Ping() As String
    Ping = "pong"
End Function
```

Route parameter:

```xpscript
[Get:/api/users/{id}]
Function GetUser(id As Integer) As String
    GetUser = CStr(id)
End Function
```

Supported compact HTTP route attributes include `Get`, `Post`, `Put`, `Delete` and `Patch`.

Route prefix:

```xpscript
[RoutePrefix:/api/users]
[Authenticated]
[Role:api-user]

[Get:/{id}]
Function GetUser(id As Integer) As String
    GetUser = CStr(id)
End Function
```

Start local Kestrel:

```text
xpscript web ./site
xpscript web ./site --port 9000
```

The local defaults are `127.0.0.1:8080`.

## Server-rendered UIForm

```xpscript
[Anonymous]
[Get]
[Post]
Sub Index()
    Dim data As New XPJsonObject
    Dim form As New UIForm("Customer")
    Dim result As String

    Call form.BindData(data)
    Call form.AddTextField("name", "Name")
    Call form.SetRequired("name", True)

    result = form.ShowDialog()

    If result = "OK" Then
        Response.ContentType = "application/json; charset=utf-8"
        Response.Write(data.Stringify())
    End If
End Sub
```

On GET, `ShowDialog()` renders the form and returns `Pending`. On a valid POST it returns `OK`.

## Browser WebAssembly

Entry point:

```xpscript
[Platform:browser-wasm]

Sub Main()
    Dim form As New UIForm("Browser app")
    Call form.AddTextField("name", "Name")
    Call form.ShowDialog()
End Sub
```

Keep privileged work on the server:

```xpscript
[ServerSide]
Function LoadCustomer(id As String) As String
    LoadCustomer = id
End Function
```

Call a `[ServerSide]` function like a normal function. XPScript preserves sequential source semantics. No explicit `Await` is required.

Only pass bridge-serializable values across the browser/server boundary. Keep Notes objects, database handles, credentials, streams and other server-only state on the server.

## HTTP client

```xpscript
Dim http As New XPHttpClient
Dim response As XPHttpResponse

Set response = http.Get("https://api.example.com/customers/42")
```

JSON POST:

```xpscript
Dim data As New XPJsonObject

Call data.Set("name", "Fredrik")
Set response = http.PostJson("https://api.example.com/customers", data)
```

Bearer token:

```xpscript
Call http.SetHeader("Authorization", "Bearer " & token)
```

Use `AddQuery` for untrusted query values:

```xpscript
url = http.AddQuery("https://api.example.com/search", "q", searchText)
```

Private and loopback destinations are blocked by default. For an application-controlled local endpoint only:

```xpscript
http.AllowPrivateNetwork = True
```

## SQLite

```xpscript
Dim db As New XPDBSQLite("customers.db")
Dim parameters As New XPJsonObject

Call parameters.Set("name", "Ada")
Call db.Execute("INSERT INTO customers(name) VALUES ($name)", parameters)

Set rows = db.Query("SELECT id, name FROM customers WHERE name = $name", parameters)

Call db.Close()
```

Use parameters for values. Do not concatenate untrusted input into SQL.

Transactions:

```xpscript
Call db.BeginTransaction()
Call db.Execute("UPDATE customers SET name = $name WHERE id = 1", parameters)
Call db.Commit()
```

Use `Rollback` to discard the transaction.

## Files and paths

Portable path object:

```xpscript
Dim p As Variant

Set p = New Path("src/test/data.json")

Print p.FileName()
Print p.Extension()
Print p.Absolute()
Print p.Exists()
```

Copy and move:

```xpscript
ok = CopyFile("in.dat", "out.dat")
ok = CopyFile("in.dat", "out.dat", 2)
ok = MoveFile("out.dat", "archive/out.dat", 3)
```

Actions are `1=fail if target exists`, `2=overwrite`, `3=skip if target exists`.

## Application arguments

```xpscript
Print CStr(Application.ArgCount)
Print CStr(Application.Args(0))
```

Run with arguments:

```text
xpscript run app.xps first "second value"
```

## Secrets

Set a stable application ID before using the OS credential store:

```xpscript
Application.Id = "com.example.customerapp"

Call Application.Secrets.Set("api", "user", token)
token = Application.Secrets.Get("api", "user")
```

XPScript namespaces credentials by `Application.Id`. Secret values remain in the operating-system credential store.

## XPAi

```xpscript
Dim ai As New XPAi("openai", Environ("OPENAI_API_KEY"))
Dim response As XPAiResponse

ai.Model = "your-openai-model"
ai.Temperature = 0.2
ai.MaxOutputTokens = 500
ai.SystemPrompt = "Answer clearly and briefly."
ai.UserPrompt = "What is privacy by design?"

Set response = ai.Complete()
Print response.Text
```

Keep AI credentials out of source files. XPAi is server/desktop capable and intentionally unavailable in browser WebAssembly.

## Native Notes/Domino

Only construct `NotesSession` directly. Obtain databases, views, documents, items and other Notes objects from their owning objects.

```xpscript
Dim session As New NotesSession
Dim db As NotesDatabase
Dim view As NotesView
Dim doc As NotesDocument

Set db = session.GetDatabase("", "crm.nsf")
Set view = db.GetView("Customers")
Set doc = view.GetFirstDocument()
```

For Browser-WASM, keep Notes work inside `[ServerSide]` procedures.

## Common security rules

- Keep secrets out of source files and browser-WASM code.
- Use parameterized database queries.
- Keep `XPHttpClient.AllowPrivateNetwork` disabled for user-controlled destinations.
- Validate web input before use.
- Keep filesystem access inside the intended application or deployment boundary.
- Return serializable data across `[ServerSide]` boundaries, not privileged runtime objects.
- Use authenticated routes and roles where access control is required.

## Full references

Use these pages for exact signatures and behavior:

- [Language reference](language-reference.md)
- [Runtime API](api-reference.md)
- [CLI reference](cli-reference.md)
- [Application and state](application-reference.md)
- [File I/O](file-io-reference.md)
- [Desktop UI](desktop-ui-reference.md)
- [UIForm](uiform.md)
- [REST API](rest-api.md)
- [Browser WebAssembly](browser-wasm.md)
- [Browser-WASM server-side functions](browser-wasm-server-side.md)
- [HTTP client](http-client.md)
- [SQLite](sqlite.md)
- [SQL Server](mssql.md)
- [XPAi](ai.md)
- [Native Notes/Domino](notes-c-api.md)
