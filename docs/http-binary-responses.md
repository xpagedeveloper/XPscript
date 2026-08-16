# XPScript binary and multipart HTTP responses

XPScript keeps the raw response bytes for every native `HttpResponse`. The normal `Body` property remains a decoded text view. Use the binary APIs for PDF, ZIP, images, executables and other non-text payloads.

## Single binary attachment

When a response includes `Content-Disposition: attachment; filename=...`, the response exposes the filename as `FileName`.

```xpscript
Dim http As New HttpClient
Dim response As HttpResponse

Set response = http.Get("https://example.com/report.pdf")
Print response.FileName
Print CStr(response.BodyLength)
Call response.SaveBodyToFile("report.pdf")
```

Properties and methods:

- `response.FileName` returns the safe leaf filename from `filename=` or `filename*=`. Any directory components supplied by the server are removed.
- `response.BodyLength` returns the raw response length in bytes.
- `response.SaveBodyToFile(path)` writes the exact response bytes without text conversion.
- `response.Body` remains available for text responses and decodes the same raw bytes using the response charset or UTF-8 fallback.

The normal 64 MiB HTTP response limit still applies.

## Multipart responses with several files

`multipart/form-data` and `multipart/mixed` responses are parsed into `response.Files`.

```xpscript
Dim http As New HttpClient
Dim response As HttpResponse
Dim file As Variant
Dim i As Integer

Set response = http.Get("https://example.com/export")

Print CStr(response.Files.Count)

For i = 0 To response.Files.Count - 1
    file = response.Files.Get(i)
    Print file.FileName
    Print file.ContentType
    Print CStr(file.Length)
    Call file.SaveToFile(file.FileName)
Next
```

Each file exposes:

- `Name`, the multipart form field name when present.
- `FileName`, the normalized filename from `filename=` or RFC 5987 `filename*=`.
- `ContentType`, the part content type, or `application/octet-stream` when absent.
- `Length`, the part size in bytes.
- `SaveToFile(path)`, which writes the part bytes exactly as received.

Convenience members on `HttpResponse` are also available:

```xpscript
Print CStr(response.FileCount)
file = response.GetFile(0)
```

File indexes are zero-based.

## Filename safety

Remote filename metadata is data, not a trusted local path. `FileName` strips directory components and control characters. The save methods still require the caller to choose the destination path.

Saving refuses a directory target and refuses overwriting an existing symbolic-link or reparse-point file target. Permission and I/O failures are returned as bounded XPScript runtime errors.

## Binary integrity

The native HTTP compatibility gate verifies on Windows, Linux and macOS that:

- a single binary attachment is saved byte-for-byte,
- multipart responses containing more than one file are parsed correctly,
- each part keeps its own filename, field name, content type and byte length,
- UTF-8 `filename*=` metadata is decoded correctly,
- saved multipart files match the original bytes exactly.
