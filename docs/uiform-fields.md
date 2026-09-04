# Extended UIForm fields

XPScript UIForm supports additional data-entry fields for web and desktop applications.

## FileField

```xpscript
Call form.AddFileField("attachment", "Attachment")
Call form.SetFileOptions("attachment", ".pdf,image/*", 8388608, True)
```

On web forms this uses multipart/form-data. A single file is stored in `form.Data` as a JSON object. With `multiple=True`, the value is a JSON array. Each uploaded file contains:

```json
{
  "fileName": "document.pdf",
  "contentType": "application/pdf",
  "length": 12345,
  "base64": "..."
}
```

The configured per-file maximum is enforced before the value is exposed to the form. The supported maximum configuration is 64 MiB per file. Desktop currently presents FileField as a text/path field. Use the existing desktop file-dialog API when a native picker is required.

## MultiSelect

```xpscript
Call form.AddMultiSelect("roles", "Roles")
Call form.AddOption("roles", "admin")
Call form.AddOption("roles", "editor")
```

The stored value is a `XPJsonArray`.

## CheckBoxGroup

```xpscript
Call form.AddCheckBoxGroup("features", "Features")
Call form.AddOption("features", "api")
Call form.AddOption("features", "reports")
```

The web renderer uses a checkbox for each option. The stored value is a `XPJsonArray`.

## TelField

```xpscript
Call form.AddTelField("phone", "Phone")
Call form.SetLength("phone", 0, 64)
```

The web renderer uses `input type="tel"`. XPScript does not impose one global phone-number format.

## WeekField

```xpscript
Call form.AddWeekField("week", "Week")
```

Values use ISO week form such as `2026-W34` and are validated before they are stored.

## DecimalField

```xpscript
Call form.AddDecimalField("amount", "Amount")
Call form.SetNumberRange("amount", -1000000, 1000000)
```

The value is parsed using invariant decimal syntax and stored as a numeric JSON value.

## CurrencyField

```xpscript
Call form.AddCurrencyField("price", "Price", "SEK")
Call form.SetNumberRange("price", 0, 1000000)
```

The currency code is display metadata. The stored value is numeric. Currency codes must contain three ASCII letters.

## RichTextField

```xpscript
Call form.AddRichTextField("description", "Description")
Call form.SetLength("description", 0, 100000)
```

On the web, RichTextField stores HTML and is rendered with TinyMCE 7. Desktop falls back to a multiline text editor.

TinyMCE can use Tiny Cloud or a locally hosted TinyMCE installation. Configuration is read from `xpscript-ui.json` in the current application directory or beside the executable.

Tiny Cloud, default configuration:

```json
{
  "tinyMce": {
    "mode": "cloud",
    "scriptUrl": "https://cdn.tiny.cloud/1/no-api-key/tinymce/7/tinymce.min.js"
  }
}
```

`scriptUrl` may be omitted in cloud mode. XPScript then uses the default Tiny Cloud URL above. A Tiny Cloud API-key URL can be supplied instead.

Local/self-hosted TinyMCE:

```json
{
  "tinyMce": {
    "mode": "local",
    "scriptUrl": "/assets/tinymce/tinymce.min.js"
  }
}
```

The local URL must be an application-relative path beginning with `/`, or an absolute HTTPS URL. The application is responsible for actually serving the configured local TinyMCE files.

Environment variables override the JSON file. This is useful for IIS, containers and deployment pipelines:

```text
XPSCRIPT_TINYMCE_MODE=local
XPSCRIPT_TINYMCE_SCRIPT_URL=/assets/tinymce/tinymce.min.js
```

Allowed modes are `cloud` and `local`. Local mode requires a script URL. Invalid modes or URLs fail explicitly instead of silently falling back to another source.

Rich-text HTML is application data. Treat it as untrusted HTML when rendering it outside the editor. Apply an allow-list HTML sanitizer before placing stored HTML into pages where arbitrary HTML execution would be unsafe.

## LookupField

LookupField loads its options from an HTTP endpoint. This is useful when the lookup data comes from an XPS file that in turn reads Supabase, Domino REST, or another REST API.

Static lookup mode loads the option array when the form is created:

```xpscript
Call form.AddLookupField( _
    "customerId", _
    "Customer", _
    "https://app.example.com/api/customer-options.xps", _
    "id", _
    "name")
```

The endpoint must return a JSON array:

```json
[
  { "id": "c1", "name": "Customer One" },
  { "id": "c2", "name": "Customer Two" }
]
```

The fourth and fifth arguments select the JSON property used as the stored value and display label. The three-argument form expects properties named `value` and `label`.

```xpscript
Call form.AddLookupField("customerId", "Customer", url)
```

Lookup endpoints must use an absolute HTTP or HTTPS URL. Static mode accepts up to 5000 option rows. The web renderer uses a select control. Desktop currently uses its existing select control.

### Server-side lookup search

For large datasets, pass `True` as the final argument. XPScript then does not preload the endpoint. The browser requests matching rows as the user types.

```xpscript
Call form.AddLookupField( _
    "customerId", _
    "Customer", _
    "https://app.example.com/api/customer-options.xps", _
    "id", _
    "name", _
    True)

Call form.SetRemoteSearchOptions("customerId", "q", "value", 2, 25)
```

`SetRemoteSearchOptions` arguments are field name, search parameter, exact-value parameter, minimum number of typed characters and maximum results shown by the client.

The browser searches with requests such as:

```text
GET /api/customer-options.xps?q=fred&limit=25
```

When the form is submitted, XPScript validates the selected value against the backend instead of trusting a value posted by the browser:

```text
GET /api/customer-options.xps?value=c123&limit=2
```

The endpoint must therefore support both the configured search parameter and exact-value parameter. The `limit` parameter is a requested upper bound. The endpoint should enforce its own maximum as well.

The server-side search endpoint returns the same JSON-array format as static lookup mode. Server-side search is intended primarily for same-origin XPS endpoints. Cross-origin endpoints must allow the browser request through CORS.

## AutoCompleteField

AutoCompleteField uses the same JSON endpoint contract as LookupField:

```xpscript
Call form.AddAutoCompleteField( _
    "productId", _
    "Product", _
    "https://app.example.com/api/product-options.xps", _
    "id", _
    "name")
```

Static mode uses an HTML datalist. The selected/stored value is the value property, not the display label. Desktop currently maps AutoCompleteField to the existing select control.

Server-side autocomplete uses the same final `True` argument and remote-search settings:

```xpscript
Call form.AddAutoCompleteField( _
    "productId", _
    "Product", _
    "https://app.example.com/api/product-options.xps", _
    "id", _
    "name", _
    True)

Call form.SetRemoteSearchOptions("productId", "q", "value", 3, 20)
```

The web renderer performs a debounced search after the configured minimum number of characters. The visible label and stored value remain separate. The posted value is validated server-side using the exact-value lookup before it is accepted.

## Example XPS lookup endpoint

The endpoint can use any existing XPScript database or HTTP client. For server-side mode, inspect `Request.Query` and apply the filter to the backend instead of loading the complete table.

Conceptually, the endpoint should implement these two request forms:

```text
?q=search text&limit=25
?value=exact-id&limit=2
```

It should then return the same JSON array:

```json
[
  { "id": "c1", "name": "Customer One" }
]
```

The implementation can use `XPHttpDbSupabase`, `XPHttpDbDominoRest` or `XPHttpClient`. Keep authentication and authorization on the lookup endpoint. Do not expose database service credentials to browser code.

## Dirty tracking

All stored field values participate in normal UIForm dirty tracking. Changes appear through:

```xpscript
form.IsDirty
form.DirtyFields
```

If a value is restored to its clean baseline, its field name is removed from `DirtyFields`.
