# XPScript binary and multipart HTTP responses

XPScript keeps the raw response bytes for every native `HttpResponse`. The normal `Body` property remains a decoded text view. Use the binary APIs for PDF, ZIP, images, executables and other non-text payloads.

## Single response

Every response is exposed as one or more `Parts`.

For a normal non-multipart response, `response.Parts.Count` is `1`.

```xpscript
Dim http As New HttpClient
Dim response As HttpResponse
Dim part As Variant

Set response = http.Get("https://example.com/api")
part = response.Parts.Get(0)

Print part.ContentType
Print CStr(part.IsText)
Print part.Body
```

A part exposes:

- `Name`, the multipart form field name when present.
- `FileName`, the normalized filename when present.
- `ContentType`, including charset parameters when supplied.
- `Length`, the raw part length in bytes.
- `IsText`, true for text types, JSON, XML, JavaScript and form-urlencoded content.
- `IsFile`, true when the part has a filename.
- `Body`, decoded text using the part charset or UTF-8 fallback.
- `Headers`, the part headers.
- `SaveToFile(path)`, which writes the exact bytes without text conversion.

## Single binary attachment

When a response includes `Content-Disposition: attachment; filename=...`, the response also exposes the filename directly as `FileName`.

```xpscript
Set response = http.Get("https://example.com/report.pdf")
Print response.FileName
Print CStr(response.BodyLength)
Call response.SaveBodyToFile("report.pdf")
```

`response.FileName` returns the safe leaf filename from `filename=` or `filename*=`. Directory components supplied by the server are removed.

`response.SaveBodyToFile(path)` writes the exact response bytes without text conversion.

The normal 64 MiB HTTP response limit still applies.

## Multipart responses containing text and files

`multipart/form-data` and `multipart/mixed` responses are parsed into `response.Parts`.

`response.Files` is a filtered view that contains only parts with a filename.

A response can therefore contain JSON or text metadata together with one or more binary files.

```xpscript
Dim http As New HttpClient
Dim response As HttpResponse
Dim part As Variant
Dim file As Variant
Dim i As Integer

Set response = http.Get("https://example.com/export")

Print CStr(response.Parts.Count)
Print CStr(response.Files.Count)

For i = 0 To response.Parts.Count - 1
    part = response.Parts.Get(i)

    Print part.Name
    Print part.ContentType

    If part.IsText Then
        Print part.Body
    End If

    If part.IsFile Then
        Print part.FileName
        Call part.SaveToFile(part.FileName)
    End If
Next
```

This lets application code inspect the MIME type before deciding how to consume a part.

For example, a multipart response may contain:

```text
Part 0: application/json; charset=utf-8
Part 1: application/pdf, FileName=report.pdf
Part 2: image/png, FileName=preview.png
```

The JSON part can be read through `part.Body`. The PDF and PNG parts can be saved with `SaveToFile()`.

## File-only view

If only files are relevant, use `response.Files`.

```xpscript
For i = 0 To response.Files.Count - 1
    file = response.Files.Get(i)
    Print file.FileName
    Print file.ContentType
    Print CStr(file.Length)
    Call file.SaveToFile(file.FileName)
Next
```

Convenience members are also available:

```xpscript
Print CStr(response.PartCount)
part = response.GetPart(0)

Print CStr(response.FileCount)
file = response.GetFile(0)
```

Indexes are zero-based.

## Filename safety

Remote filename metadata is data, not a trusted local path. `FileName` strips directory components and control characters. The save methods still require the caller to choose the destination path.

Saving refuses a directory target and refuses overwriting an existing symbolic-link or reparse-point file target. Permission and I/O failures are returned as bounded XPScript runtime errors.

## Binary integrity

The native HTTP compatibility gate verifies on Windows, Linux and macOS that:

- a single binary attachment is saved byte-for-byte,
- multipart responses can contain both text and several files,
- each part exposes its own content type,
- JSON/text parts can be read through `Body`,
- `Files` contains only file parts,
- each file keeps its own filename, field name, content type and byte length,
- UTF-8 `filename*=` metadata is decoded correctly,
- saved multipart files match the original bytes exactly.
