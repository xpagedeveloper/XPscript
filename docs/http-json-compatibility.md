# XPScript HTTP and JSON runtime

XPScript provides standalone HTTP and JSON APIs implemented on .NET 10. The public API uses XPScript-native names and does not require any external application runtime.

## HttpClient

Create a client:

```xpscript
Dim http As New HttpClient
```

Supported methods:

- `Get(url)`
- `Post(url, body)`
- `Put(url, body)`
- `Patch(url, body)`
- `Delete(url)`
- `SetHeader(name, value)`
- `RemoveHeader(name)`
- `ClearHeaders()`

`Timeout` controls request timeout in seconds.

Example:

```xpscript
Dim http As New HttpClient
Dim response As HttpResponse

Call http.SetHeader("Accept", "application/json")
http.Timeout = 30
Set response = http.Get("https://api.example.com/users")

Print CStr(response.StatusCode)
Print response.StatusText
Print response.ContentType
Print response.Body
Print CStr(response.IsSuccess)
```

## HttpResponse

Response properties:

- `StatusCode`
- `StatusText`
- `Body`
- `ContentType`
- `Headers`
- `IsSuccess`

`Headers` contains the response headers exposed by the standalone runtime.

## Sending JSON

```xpscript
Dim http As New HttpClient
Dim body As New JsonObject
Dim response As HttpResponse

Call body.Set("name", "Alice")
Call body.Set("active", True)

Call http.SetHeader("Content-Type", "application/json")
Set response = http.Post("https://api.example.com/users", JsonStringify(body))
Print response.Body
```

## JsonDocument

Parse JSON:

```xpscript
Dim document As JsonDocument
Set document = JsonDocument.Parse("{""name"":""Alice""}")
Print document.Stringify()
```

## JsonObject

```xpscript
Dim obj As New JsonObject
Call obj.Set("name", "Alice")
Call obj.Set("age", 42)

Print CStr(obj.Contains("name"))
Print CStr(obj.Count)
Print CStr(obj.Get("age"))

Call obj.Remove("age")
```

Supported operations:

- `Get(name)`
- `Set(name, value)`
- `Remove(name)`
- `Contains(name)`
- `Count`

## JsonArray

```xpscript
Dim values As New JsonArray
Call values.Add("one")
Call values.Add("two")

Print CStr(values.Get(0))
Call values.Set(1, "second")
Call values.RemoveAt(0)
Print CStr(values.Count)
```

Supported operations:

- `Add(value)`
- `Get(index)`
- `Set(index, value)`
- `RemoveAt(index)`
- `Count`

## JsonElement

`JsonElement` exposes:

- `Type`
- `Value`

Recommended Type values are:

- `Object`
- `Array`
- `String`
- `Number`
- `Boolean`
- `Null`

## JSON helper functions

XPScript also provides convenience functions:

```xpscript
Set value = JsonParse(text)
text = JsonStringify(value)
text = JsonEncode(value)
Set value = JsonDecode(text)
```

`JsonEncode` is a serialization convenience alias and `JsonDecode` is a parsing convenience alias.

## Implementation

The HTTP runtime uses the .NET HTTP stack. JSON handling uses `System.Text.Json` / `System.Text.Json.Nodes` internally. These are implementation details; XPScript source uses the public APIs described above.

## Verification status

The XPScript-native HTTP/JSON implementation and sample source are currently on branch `runtime-development-no-ci`. Automated GitHub workflow execution is intentionally disabled until explicitly re-enabled.
