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

The stored value is a `JsonArray`.

## CheckBoxGroup

```xpscript
Call form.AddCheckBoxGroup("features", "Features")
Call form.AddOption("features", "api")
Call form.AddOption("features", "reports")
```

The web renderer uses a checkbox for each option. The stored value is a `JsonArray`.

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

On the web, RichTextField stores HTML and is rendered with TinyMCE 7. The generated form loads TinyMCE from Tiny Cloud using the `no-api-key` URL. Desktop falls back to a multiline text editor.

Rich-text HTML is application data. Treat it as untrusted HTML when rendering it outside the editor. Apply an allow-list HTML sanitizer before placing stored HTML into pages where arbitrary HTML execution would be unsafe.

## LookupField

LookupField loads its options from an HTTP endpoint. This is useful when the lookup data comes from an XPS file that in turn reads Supabase, Domino REST, or another REST API.

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

Lookup endpoints must use an absolute HTTP or HTTPS URL. A response is limited to 5000 option rows. The web renderer uses a select control. Desktop currently uses its existing select control.

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

The web renderer uses an HTML datalist. The selected/stored value is the value property, not the display label. Desktop currently maps AutoCompleteField to the existing select control.

## Example XPS lookup endpoint

The endpoint can use any existing XPScript database or HTTP client.

```xpscript
[Authenticated]
[Get]
[Route:/api/customer-options]
Sub CustomerOptions()
    Dim db As New HTTPDBSupabase("https://supabase.example.com", Application.Get("SUPABASE_KEY"))
    Dim rows As JsonDocument

    Set rows = db.Select("customers", "select=id,name&order=name.asc")
    Response.OK(rows.Root.Value)
End Sub
```

A Domino-backed endpoint can use `HTTPDBDominoRest` instead and transform its result to the same JSON array contract.

## Dirty tracking

All stored field values participate in normal UIForm dirty tracking. Changes appear through:

```xpscript
form.IsDirty
form.DirtyFields
```

If a value is restored to its clean baseline, its field name is removed from `DirtyFields`.
