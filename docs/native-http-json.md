# XPScript Native HTTP and JSON


## HttpClient

Create an HTTP client with:

```xpscript
Dim http As New HttpClient
```

### Timeout

Timeout is configured in seconds and must be a finite value greater than zero.

```xpscript
http.Timeout = 30
```

### SetHeader

Adds or replaces a request header.

```xpscript
Call http.SetHeader("Accept", "application/json")
Call http.SetHeader("User-Agent", "XPScript")
```

Header names must be valid HTTP token names. Header values reject CR, LF, NUL and prohibited control characters.

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

## HTTP redirect policy

Automatic redirects are disabled by design.

If a server responds with `301`, `302`, `303`, `307` or `308`, XPScript returns that response to the caller instead of automatically following the `Location` header. This prevents configured authorization/custom headers from being silently forwarded to another origin.

An application may inspect the returned `Location` header and explicitly issue another request after applying its own host/origin policy.

## HTTP resource limits

The native HTTP runtime applies fixed defensive limits:

- request body: maximum **8 MiB** of UTF-8 payload,
- response body: maximum **8 MiB**,
- response bodies are read using `ResponseHeadersRead` and checked while streaming,
- a declared `Content-Length` larger than the response limit is rejected before buffering the body.

These limits are runtime protections, not application-level authorization rules.

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

## HttpClient lifetime

The runtime client owns an underlying .NET `HttpClient` and handler. The runtime type supports deterministic disposal. Applications should avoid creating unbounded numbers of clients and retaining them indefinitely.

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

If a mutation would exceed the JSON resource budget, the previous value is restored and the operation fails.

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

`Add` and `Set` roll back their mutation if the resulting JSON value exceeds the configured resource budget.

### RemoveAt

```xpscript
Call arr.RemoveAt(0)
```

### Count

Returns the number of array elements.

## JsonElement

`JsonElement` represents an individual JSON value. The native runtime surface includes element type/value inspection.

## JSON resource limits

The native JSON runtime currently applies:

- parser input: maximum **8 MiB** UTF-8,
- nesting: maximum **64 levels**,
- node count: maximum **100,000 nodes**,
- estimated in-memory JSON payload: maximum **16 MiB**,
- serialized JSON output: maximum **16 MiB** UTF-8.

These limits apply to parsed values and to values built through `JsonObject`/`JsonArray` mutation. They are designed to prevent accidental or hostile unbounded allocation, but they are not a substitute for application-specific schema and field limits.

Malformed JSON diagnostics do not echo the complete source payload.

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

Malformed JSON, resource-budget violations, invalid indexes and HTTP failures should be handled with normal XPScript `On Error` handling where applicable.

Do not place secrets such as authorization tokens in diagnostic output. Use request headers only where required and avoid printing them.

## Samples

- [samples/native-http-json.xps](../samples/native-http-json.xps)
- [samples/native-http-header-validation.xps](../samples/native-http-header-validation.xps)
- [samples/native-http-resource-limits.xps](../samples/native-http-resource-limits.xps)
- [samples/json-resource-limits.xps](../samples/json-resource-limits.xps)

` samples/json-http.xps ` contains older compatibility-class coverage and should not be treated as the preferred API for new standalone XPScript programs.
