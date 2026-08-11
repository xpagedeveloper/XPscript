# XPScript Native HTTP and JSON

This page documents the current XPScript-native HTTP and JSON API demonstrated by `samples/native-http-json.xps`.

## HttpClient

Create an HTTP client with:

```xpscript
Dim http As New HttpClient
```

### Timeout

Timeout is configured in seconds.

```xpscript
http.Timeout = 30
```

### SetHeader

Adds or replaces a request header.

```xpscript
Call http.SetHeader("Accept", "application/json")
Call http.SetHeader("User-Agent", "XPScript")
```

### RemoveHeader

Removes one configured header.

### ClearHeaders

Clears configured request headers.

## HTTP methods

### Get

```xpscript
Set response = http.Get("https://example.com/api")
```

### Post

Sends a request body with POST.

### Put

Sends a request body with PUT.

### Patch

Sends a request body with PATCH.

### Delete

Sends a DELETE request.

Network calls are side-effecting operations. Tests should use a controlled endpoint rather than relying on a public service.

## HttpResponse

HTTP methods return `HttpResponse`.

Available properties include:

- `StatusCode`
- `StatusText`
- `Body`
- `ContentType`
- `Headers`
- `IsSuccess`

Example:

```xpscript
Set response = http.Get("https://example.com/api")
Print CStr(response.StatusCode)
Print response.Body
```

## JsonDocument

### JsonDocument.Parse

Parses JSON text.

```xpscript
Set document = JsonDocument.Parse("{""name"":""Alice""}")
```

### Stringify

Serializes the document back to JSON text.

```xpscript
Print document.Stringify()
```

## JsonObject

Create an object:

```xpscript
Dim obj As New JsonObject
```

### Set

```xpscript
Call obj.Set("name", "Fredrik")
Call obj.Set("enabled", True)
```

### Get

```xpscript
Print CStr(obj.Get("name"))
```

### Contains

Checks whether a property exists.

```xpscript
If obj.Contains("enabled") Then
    Print "exists"
End If
```

### Remove

```xpscript
Call obj.Remove("enabled")
```

### Count

Returns the number of properties.

## JsonArray

Create an array:

```xpscript
Dim arr As New JsonArray
```

### Add

```xpscript
Call arr.Add("one")
Call arr.Add("two")
```

### Get

```xpscript
Print CStr(arr.Get(1))
```

### Set

```xpscript
Call arr.Set(1, "TWO")
```

### RemoveAt

```xpscript
Call arr.RemoveAt(0)
```

### Count

Returns the number of array elements.

## JsonElement

`JsonElement` represents an individual JSON value. The native runtime surface includes element type/value inspection.

## JsonStringify

Serializes a supported JSON object/value.

```xpscript
Print JsonStringify(obj)
```

## JsonEncode

Encodes a supported runtime JSON value as JSON text.

```xpscript
Print JsonEncode(arr)
```

## JsonDecode

Parses JSON text and returns a JSON document/value object.

```xpscript
Set document = JsonDecode("{""ok"":true}")
```

## JsonParse

Alternative parsing helper provided by the native JSON compatibility surface.

## Error handling

Malformed JSON, invalid indexes and HTTP failures should be handled with normal XPScript `On Error` handling where applicable.

Do not place secrets such as authorization tokens in diagnostic output. Use request headers only where required and avoid printing them.

## Samples

- `samples/native-http-json.xps`

` samples/json-http.xps ` contains older compatibility-class coverage and should not be treated as the preferred API for new standalone XPScript programs.
