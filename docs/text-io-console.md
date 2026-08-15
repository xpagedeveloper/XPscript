# XPScript text file I/O, Base64 and console I/O

> For compact command syntax, parameters and examples, see the [Command Reference](command-reference.md).

XPScript provides standalone text, binary and console I/O. File input and interactive console input are intentionally separate APIs.

## Charset

`Charset` controls conversion between XPScript Unicode strings and file bytes.

Supported aliases include:

- `utf-8` / `utf8`
- `unicode` / `utf-16` / `utf16` / `utf-16le`
- `utf-16be`
- `ascii`
- `latin1`
- `default` / `ansi`

Example:

```xpscript
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

## Encoding

`Encoding` is independent from `Charset` and represents an additional storage/transfer encoding layer.

Supported values:

- `none`
- `base64`

Example:

```xpscript
f = FreeFile
Open "data.b64" For Output As #f Charset "utf-8" Encoding "base64"
Print #f, "Fredrik åäö"
Close #f
```

On write, Charset converts text to bytes and Base64 then encodes those bytes. Reading performs the reverse operation.

## File Input$

The file function:

```xpscript
value = Input$(count, #fileNumber)
```

reads `count` characters from the current position of the already-open file. This is file I/O and is not interactive user input.

Example:

```xpscript
Dim f As Integer
Dim value As String

f = FreeFile
Open "data.txt" For Input As #f Charset "utf-8"
value = Input$(5, #f)
Close #f
```

The file position advances by the amount read. A count of zero returns an empty string. Invalid counts or attempts to read beyond available data produce a runtime error.

## File locking

`Lock` and `Unlock` operate on the operating-system file handle.

```xpscript
Open "data.bin" For Binary As #f
Lock #f, 1 To 100
' protected region
Unlock #f, 1 To 100
Close #f
```

Binary ranges are 1-based byte ranges. Random file ranges map to records using the configured record length. Sequential modes use whole-file locking.

The lock is not an XPScript-only flag; another process or file handle must observe the OS lock.

## Base64 functions

Available helpers:

- `ToBase64(value)`
- `ToBase64(value, charset)`
- `FromBase64(value)`
- `FromBase64(value, charset)`
- `Base64Encode(value)`
- `Base64Encode(value, charset)`
- `Base64Decode(value)`
- `Base64Decode(value, charset)`

UTF-8 is used by default when no charset is supplied.

```xpscript
Dim encoded As String
Dim decoded As String

encoded = Base64Encode("Fredrik åäö")
decoded = Base64Decode(encoded)
```

## URL functions

- `UrlEncode(value)`
- `UrlDecode(value)`

```xpscript
Dim encoded As String
encoded = UrlEncode("hello world+å")
Print UrlDecode(encoded)
```

## Console Print

```xpscript
Print "Hello"
Print$ "Hello"
```

A bare `Print` writes an empty line. File output remains `Print #fileNumber, value`.

## Interactive console Input

Interactive console input is separate from file `Input$`.

```xpscript
Dim value As String
Input value
Input "Name: ", value
Input$ value
```

These console forms read from standard input. They must never be used to implement or rewrite `Input$(count, #fileNumber)`.

## Pause

```xpscript
Print "Press any key"
Pause
```

`Pause` waits for console input. When standard input is redirected it consumes an input character instead.

## Verification status

Regression samples exist for UTF-8, UTF-16, Latin-1, Base64, console I/O, file `Input$` and OS file locking. Automated workflow execution is intentionally deferred while the development branch `runtime-development-no-ci` is active.
