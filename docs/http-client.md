# HTTP client

XPScript includes a native `HttpClient` for outgoing HTTP and REST calls.

## Basic requests

```xpscript
Dim http As New HttpClient
Dim response As HttpResponse

Set response = http.Get("https://api.example.com/customers/42")
Set response = http.Post("https://api.example.com/customers", "body")
Set response = http.Put("https://api.example.com/customers/42", "body")
Set response = http.Patch("https://api.example.com/customers/42", "body")
Set response = http.Delete("https://api.example.com/customers/42")
```

`HttpResponse` exposes `StatusCode`, `StatusText`, `Body`, `BodyLength`, `ContentType`, `Headers`, `IsSuccess`, multipart/file helpers and `SaveBodyToFile`.

## JSON requests

Use the JSON helpers when calling REST services.

```xpscript
Dim http As New HttpClient
Dim response As HttpResponse
Dim data As New JsonObject

Call data.Set("name", "Fredrik")
Call data.Set("enabled", True)

Set response = http.PostJson("https://api.example.com/customers", data)
```

Available helpers:

- `http.GetJson(url)` returns a `JsonDocument` and requires a successful HTTP status.
- `http.PostJson(url, data)` sends JSON and returns `HttpResponse`.
- `http.PutJson(url, data)` sends JSON with PUT.
- `http.PatchJson(url, data)` sends JSON with PATCH.
- `response.Json()` parses the response body and returns a `JsonDocument`.

The JSON write helpers set `Content-Type` to `application/json; charset=utf-8`.

## Query parameters

Use `AddQuery` instead of concatenating untrusted values into a URL.

```xpscript
Dim url As String
url = http.AddQuery("https://api.example.com/search", "q", "hello world")
```

The parameter name and value are URL encoded.

## Form encoded requests

`PostForm` sends a JSON object as `application/x-www-form-urlencoded`.

```xpscript
Dim data As New JsonObject
Dim response As HttpResponse

Call data.Set("name", "Fredrik")
Call data.Set("country", "SE")

Set response = http.PostForm("https://api.example.com/form", data)
```

`PostForm` accepts scalar form values. Nested JSON arrays or objects are rejected.

## UIForm data loading

`UIForm` data is already stored as a JSON object. `LoadForm` combines an HTTP GET, JSON parse and `UIForm.BindData` operation.

```xpscript
Dim http As New HttpClient
Dim form As New UIForm("Customer")

Call http.LoadForm(form, "https://api.example.com/customers/42")

Call form.AddTextField("name", "Name")
Call form.AddEmailField("email", "Email")
```

The endpoint must return a successful HTTP status and a JSON object.

## UIForm data saving

`SaveForm` posts `form.Data` as JSON.

```xpscript
Dim response As HttpResponse
Set response = http.SaveForm(form, "https://api.example.com/customers/42")

If Not response.IsSuccess Then
    Print "Save failed: " & CStr(response.StatusCode)
End If
```

For APIs that update records with PUT, use `PutForm`:

```xpscript
Set response = http.PutForm(form, "https://api.example.com/customers/42")
```

This makes the normal load/edit/save flow:

```xpscript
Dim http As New HttpClient
Dim form As New UIForm("Customer")
Dim response As HttpResponse

Call http.LoadForm(form, "https://api.example.com/customers/42")

Call form.AddTextField("name", "Name")
Call form.AddEmailField("email", "Email")

If form.ShowDialog() = "OK" Then
    Set response = http.PutForm(form, "https://api.example.com/customers/42")
End If
```

## Headers and authentication

```xpscript
http.Timeout = 30
Call http.SetHeader("Accept", "application/json")
Call http.SetHeader("Authorization", "Bearer " & token)
```

Headers can be removed with `RemoveHeader(name)` or all cleared with `ClearHeaders()`.

## Private and local endpoints

Private, loopback, link-local and other non-public network destinations are blocked by default. If an application intentionally calls a trusted local service or intranet API, opt in on that client instance:

```xpscript
Dim http As New HttpClient
http.AllowPrivateNetwork = True
Set response = http.Get("http://127.0.0.1:8080/health")
```

Only enable `AllowPrivateNetwork` when the destination is trusted and application-controlled. Keep it disabled when any part of the destination URL can come from request data or other untrusted input.

## Security and limits

Outgoing URLs must be absolute `http://` or `https://` URLs. URL user information is rejected. By default the native client resolves destinations before sending and rejects loopback, unspecified, link-local, private, carrier-grade NAT, benchmark and multicast/reserved network addresses. `AllowPrivateNetwork = True` explicitly disables that destination restriction for trusted intranet or local endpoints. The native client does not automatically follow redirects. Request bodies are limited to 8 MiB and response bodies to 64 MiB. Header names and values are validated to prevent malformed or injected headers.
