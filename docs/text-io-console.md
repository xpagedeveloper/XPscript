# XPScript text file I/O, Base64 and console I/O

XPScript extends the XPScript-style `Open` statement with independent `Charset` and `Encoding` options for standalone text file I/O.

## Charset

`Charset` controls how Unicode strings are converted to and from bytes.

Supported built-in aliases include:

- `utf-8` / `utf8`
- `unicode` / `utf-16` / `utf16` / `utf-16le`
- `utf-16be`
- `ascii`
- `default` / `ansi`

`unicode` maps to UTF-16 little endian.

Examples:

```xpscriptscript
Dim f As Integer
Dim value As String

f = FreeFile
Open "utf8.txt" For Output As #f Charset "utf-8"
Print #f, "ÅÄÖ 漢字"
Close #f

f = FreeFile
Open "utf8.txt" For Input As #f Charset "utf-8"
Line Input #f, value
Close #f
```

UTF-16:

```xpscriptscript
f = FreeFile
Open "unicode.txt" For Output As #f Charset "unicode"
Print #f, "ÅÄÖ 漢字"
Close #f
```

## Encoding

`Encoding` is independent from `Charset` and controls an additional transfer/storage encoding layer.

Currently supported values are:

- `none` (default)
- `base64`

If `Encoding` is omitted, normal charset-encoded text is read or written.

Base64 using the default charset:

```xpscriptscript
f = FreeFile
Open "data.b64" For Output As #f Encoding "base64"
Print #f, "Fredrik åäö"
Close #f
```

## Combining Charset and Encoding

`Charset` and `Encoding` can be combined. Their order in the `Open` statement is not significant.

```xpscriptscript
Open "data.b64" For Output As #f Charset "utf-16" Encoding "base64"
```

and:

```xpscriptscript
Open "data.b64" For Input As #f Encoding "base64" Charset "unicode"
```

are compatible with each other.

The processing model is:

### Writing

1. XPScript has a Unicode string.
2. `Charset` converts the string to bytes.
3. `Encoding "base64"` Base64-encodes those bytes.
4. The Base64 text is stored in the file.

### Reading

1. XPScript reads the Base64 text.
2. `Encoding "base64"` decodes it to bytes.
3. `Charset` converts those bytes back to a Unicode string.

This means UTF-8 + Base64 and UTF-16 + Base64 produce different Base64 payloads while resulting in the same Unicode text after decoding with the matching charset.

Charset/Encoding options apply to text modes:

- `For Input`
- `For Output`
- `For Append`

Existing `Binary` and `Random` file modes continue to use the binary file runtime and are not changed by these text options.

## Base64 string functions

XPScript provides:

- `ToBase64(value)`
- `ToBase64(value, charset)`
- `FromBase64(value)`
- `FromBase64(value, charset)`

UTF-8 is used by default.

```xpscriptscript
Dim encoded As String
Dim decoded As String

encoded = ToBase64("Fredrik åäö")
decoded = FromBase64(encoded)
```

`$` aliases are accepted as well, for example `ToBase64$()` and `FromBase64$()`.

## URL string functions

XPScript provides:

- `UrlEncode(value)`
- `UrlDecode(value)`

Example:

```xpscriptscript
Dim encoded As String

encoded = UrlEncode("hello world+å")
Print encoded
Print UrlDecode(encoded)
```

`UrlEncode` uses UTF-8-compatible URI percent encoding. `UrlDecode` also treats `+` as a space when decoding form-style values.

## Console Print and Print$

Both forms write to standard output:

```xpscriptscript
Print "Hello"
Print$ "Hello"
```

A bare `Print` or `Print$` writes an empty line.

File output retains its existing syntax:

```xpscriptscript
Print #f, "Hello"
```

## Console Input and Input$

The standalone console input statements read a complete line and wait until the user presses Enter.

Without a prompt:

```xpscriptscript
Dim value As String
Input value
```

or:

```xpscriptscript
Input$ value
```

With a prompt:

```xpscriptscript
Input "Name: ", value
```

The prompt is written without an automatic newline. The line entered by the user is assigned to the target string variable.

File input remains distinct and keeps the `#` syntax:

```xpscriptscript
Input #f, value
Line Input #f, value
```

## Pause

`Pause` waits for one key press without requiring Enter when the executable is connected to an interactive console.

```xpscriptscript
Print "Press any key"
Pause
```

When standard input is redirected, XPScript consumes one input character instead. This allows console programs using `Pause` to be regression-tested in CI.

## CI coverage

`samples/textio-console.xps` is compiled to a Windows executable by GitHub Actions and tests:

- UTF-8 file write/read
- UTF-16/Unicode file write/read
- Base64 file encoding without an explicit charset
- Base64 combined with UTF-16 charset
- both `Charset ... Encoding ...` and `Encoding ... Charset ...` orderings
- `ToBase64`
- `FromBase64`
- `UrlEncode`
- `UrlDecode`
- `Print$`
- bare `Print`
- `Input` with a prompt
- `Input$` without a prompt
- `Pause` with redirected stdin
