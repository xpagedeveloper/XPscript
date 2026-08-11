# XPScript standalone HTTP and JSON compatibility

XPScript implements a standalone compatibility layer for the XPScript HTTP and JSON APIs without loading or requiring XPScript Notes or XPScript.

The compatibility classes are implemented on .NET 10:

- `NotesHTTPRequest` uses `HttpClient`
- `NotesJSONNavigator`, `NotesJSONObject`, `NotesJSONArray`, and `NotesJSONElement` use `System.Text.Json`

## Direct construction instead of NotesSession

In XPScript, these objects are normally created from `NotesSession`. In XPScript, the preferred standalone syntax is direct construction with `New`.

```lotusscript
Dim http As New NotesHTTPRequest
Dim json As New NotesJSONNavigator("")
Dim obj As New NotesJSONObject
Dim arr As New NotesJSONArray
Dim element As New NotesJSONElement("value", "name")
```

The equivalent assignment form is also supported:

```lotusscript
Dim http As NotesHTTPRequest
Dim json As NotesJSONNavigator

Set http = New NotesHTTPRequest
Set json = New NotesJSONNavigator("{""name"":""Fredrik""}")
```

For migration convenience, source that still contains `session.CreateHTTPRequest()` or `session.CreateJSONNavigator(...)` is normalized to the standalone factories by the compiler. No real XPScript `NotesSession` is created or required.

## NotesHTTPRequest

Supported methods:

- `Get(url)`
- `Post(url, data)`
- `Put(url, data)`
- `Patch(url, data)`
- `DeleteResource(url)`
- `SetHeaderField(name, value)`
- `ResetHeaders()`
- `GetResponseHeaders()`
- `SetProxy(proxyHost, proxyPort)`
- `SetProxyUser(userName, password)`
- `ResetProxy()`

Supported properties:

- `ResponseCode`
- `TimeoutSec`
- `MaxRedirects`
- `PreferStrings`
- `PreferUTF8`
- `PreferJSONNavigator`

`ResponseCode` contains the HTTP response status code after a request.

`TimeoutSec` defaults to 30 seconds. A value less than or equal to zero disables the `HttpClient` timeout.

`MaxRedirects` defaults to zero. A positive value enables automatic redirects and sets the maximum redirect count.

`ResetHeaders()` restores these defaults:

- `Accept: application/json`
- `Content-Type: application/json`
- `charsets: utf-8`

Headers set with `SetHeaderField` persist across requests until changed or reset.

### HTTP response type

XPScript uses these standalone return rules:

- `PreferJSONNavigator = True` returns a `NotesJSONNavigator`
- otherwise `PreferStrings = True` returns a Unicode/.NET string decoded from UTF-8
- otherwise the response is returned as a UTF-8 byte array

`PreferUTF8` reflects the default byte-array response mode and is derived from the other response preferences.

### POST, PUT, and PATCH bodies

The data argument can be a string or one of the XPScript JSON compatibility objects. JSON objects are serialized automatically when supplied directly.

```lotusscript
Dim http As New NotesHTTPRequest
Dim json As New NotesJSONNavigator("")
Dim response As Variant

Call json.AppendElement("Fredrik", "name")
Call json.AppendElement("fredrik@example.com", "email")

Call http.SetHeaderField("Content-Type", "application/json")
Call http.SetHeaderField("Accept", "application/json")

http.PreferStrings = True
response = http.Post("https://api.example.com/users", json.Stringify())

Print CStr(http.ResponseCode)
Print response
```

### Proxy behavior

`SetProxy` configures a .NET `WebProxy` for subsequent requests. `SetProxyUser` supplies proxy credentials. `ResetProxy` removes both proxy address and proxy credentials.

TLS certificate validation follows the .NET/operating-system HTTP stack. XPScript does not use XPScript `notes.ini`, XPScript certificate stores, or XPScript HTTP configuration.

## NotesJSONNavigator

`NotesJSONNavigator` is the main parser, builder, modifier, and serializer.

Supported methods:

- `GetElementByName(name)`
- `GetElementByPointer(pointer)`
- `GetFirstElement()`
- `GetNextElement()`
- `GetNthElement(index [, suppressErrors])`
- `Stringify()`
- `AppendElement(value [, name])`
- `AppendArray([name])`
- `AppendObject([name])`

Supported properties:

- `PreferJSONNavigator`
- `PreferUTF8`

Create an empty JSON object:

```lotusscript
Dim json As New NotesJSONNavigator("")
```

Parse JSON:

```lotusscript
Dim json As New NotesJSONNavigator("{""name"":""Fredrik"",""age"":40}")
Dim element As NotesJSONElement

Set element = json.GetElementByName("name")
Print CStr(element.Value)
```

JSON Pointer is supported:

```lotusscript
Dim json As New NotesJSONNavigator("{""items"":[""one"",""two""]}")
Dim element As NotesJSONElement

Set element = json.GetElementByPointer("/items/1")
Print CStr(element.Value)
```

JSON Pointer array positions are zero-based as defined by JSON Pointer. `GetNthElement`, by contrast, is one-based for XPScript compatibility.

## NotesJSONObject

Supported property:

- `Size`

Supported methods:

- `GetElementByName(name)`
- `GetFirstElement()`
- `GetNextElement()`
- `GetNthElement(index [, suppressErrors])`
- `AppendElement(value, name)`
- `AppendArray(name)`
- `AppendObject(name)`
- `Copy(sourceObject)`

Example:

```lotusscript
Dim obj As New NotesJSONObject
Dim element As NotesJSONElement

Call obj.AppendElement("Fredrik", "name")
Call obj.AppendElement(40, "age")

Set element = obj.GetElementByName("name")
Print CStr(obj.Size)
Print CStr(element.Value)
```

## NotesJSONArray

Supported property:

- `Size`

Supported methods:

- `GetFirstElement()`
- `GetNextElement()`
- `GetNthElement(index [, suppressErrors])`
- `AppendElement(value)`
- `AppendArray()`
- `AppendObject()`
- `Copy(sourceArray)`

`GetNthElement` uses a one-based index.

```lotusscript
Dim arr As New NotesJSONArray
Dim element As NotesJSONElement

Call arr.AppendElement("one")
Call arr.AppendElement("two")

Set element = arr.GetNthElement(2)
Print CStr(element.Value)
```

If the optional `suppressErrors` argument is true, an out-of-range lookup returns `Nothing` rather than raising the index error.

## NotesJSONElement

Supported properties:

- `Name`
- `Type`
- `Value`

Supported method:

- `Copy(sourceElement)`

`Value` can contain scalar values, `NotesJSONObject`, or `NotesJSONArray`.

Example:

```lotusscript
Dim element As New NotesJSONElement("Fredrik", "name")

Print element.Name
Print CStr(element.Type)
Print CStr(element.Value)
```

The supported JSON element constants are:

- `Jsonelem_type_object = 1`
- `Jsonelem_type_array = 2`
- `Jsonelem_type_string = 3`
- `Jsonelem_type_number = 4`
- `Jsonelem_type_boolean = 5`
- `Jsonelem_type_utf8_bytearray = 6`
- `Jsonelem_type_empty = 64`

## Building nested JSON

```lotusscript
Dim json As New NotesJSONNavigator("")
Dim address As NotesJSONObject
Dim roles As NotesJSONArray

Call json.AppendElement("Fredrik", "name")
Call json.AppendElement(40, "age")
Call json.AppendElement(True, "active")

Set address = json.AppendObject("address")
Call address.AppendElement("Linkoping", "city")
Call address.AppendElement("Sweden", "country")

Set roles = json.AppendArray("roles")
Call roles.AppendElement("admin")
Call roles.AppendElement("developer")

Print json.Stringify()
```

This produces compact JSON equivalent to:

```json
{
  "name": "Fredrik",
  "age": 40,
  "active": true,
  "address": {
    "city": "Linkoping",
    "country": "Sweden"
  },
  "roles": ["admin", "developer"]
}
```

## HTTP response directly as JSON

```lotusscript
Dim http As New NotesHTTPRequest
Dim json As NotesJSONNavigator
Dim element As NotesJSONElement

http.PreferJSONNavigator = True
Set json = http.Get("https://api.example.com/user/1")

Set element = json.GetElementByName("name")
Print CStr(element.Value)
```

## Compatibility boundaries

The class names and the supported language surface intentionally resemble the XPScript XPScript APIs so existing integration code needs minimal changes. Internally these are XPScript classes, not Notes/XPScript classes.

Notable standalone differences:

- Direct `New` is the primary construction mechanism rather than `NotesSession` factories.
- HTTP uses the .NET networking stack.
- JSON uses `System.Text.Json`.
- HTTP TLS trust follows the target operating system and .NET.
- There is no dependency on a XPScript server, Notes client, Notes ID, `notes.ini`, or XPScript data directory.
- JSON byte arrays are normal .NET byte arrays and are not subject to the classic XPScript 64 KB array limitation.

## CI coverage

`samples/json-http.xps` is compiled to a Windows executable by GitHub Actions. The workflow starts a local HTTP server and validates:

- direct `New` construction of the compatibility classes
- JSON creation and serialization
- JSON parsing
- JSON Pointer lookup
- one-based `GetNthElement`
- object, array, and element values
- `Copy`
- JSON element constants
- GET
- POST
- PUT
- PATCH
- DELETE
- response status codes
- response headers
- header persistence/reset behavior
- proxy configuration API
- string responses
- `PreferJSONNavigator` responses

This means the CI checks both compiler generation and the behavior of the generated standalone executable.
