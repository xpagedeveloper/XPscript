# XPScript File I/O and Filesystem

This page documents functionality demonstrated by `samples/core-language.xps`, `samples/file-io-extensions.xps`, `samples/file-io-portability.xps`, `samples/filesystem-portability-semantics.xps`, `samples/file-charset-bom.xps`, `samples/file-lock-holder.xps`, `samples/file-lock-contender.xps` and `samples/file-delete-open-semantics.xps`.

## FreeFile

Returns an available XPScript file number.

```xpscript
Dim f As Integer
f = FreeFile()
```

## Open

### Input

```xpscript
Open "input.txt" For Input As #f
```

### Output

```xpscript
Open "output.txt" For Output As #f
```

### Append

```xpscript
Open "output.txt" For Append As #f
```

### Binary

```xpscript
Open "data.bin" For Binary As #f
```

### Random

```xpscript
Open "records.bin" For Random As #f Len = 16
```

## Charset

Text files may specify an explicit charset.

```xpscript
Open "text.txt" For Output As #f Charset "utf-8"
```

Supported explicit names demonstrated/defined by the portability layer include:

- `utf-8` — UTF-8 without BOM
- `utf-8-bom` — UTF-8 with BOM
- `utf-16` / `utf-16le`
- `utf-16le-nobom`
- `utf-16be`
- `utf-16be-nobom`
- `latin1`, `latin-1`, `iso-8859-1`
- `default`, `ansi` — XPScript's deterministic Latin-1 compatibility encoding

## Print #

Writes a text line to an output/append file.

```xpscript
Print #f, "Hello"
```

## Line Input #

Reads one line.

```xpscript
Line Input #f, line
```

## Input$(count, #file)

Reads a fixed number of characters/bytes through the file-input API and is distinct from interactive console input.

```xpscript
part = Input$(3, #f)
```

## Put and Get

Binary and Random modes support positional writes/reads.

```xpscript
Put #f, 1, value
Get #f, 1, value
```

For Random mode the position identifies a record; for Binary mode it identifies the binary file position according to the runtime's file semantics.

## Loc

Returns the current logical file position.

```xpscript
Print CStr(Loc(f))
```

## Close

```xpscript
Close #f
```

## Lock / Unlock

Locks a byte/record region on the underlying operating-system file handle.

```xpscript
Lock #f, 1 To 3
Unlock #f, 1 To 3
```

Binary/Random streams permit multiple read/write handles so explicit `Lock`/`Unlock` controls conflicting regions. Lock conflicts map to XPScript access error 70.

The cross-process fixtures are intentionally split into:

- `samples/file-lock-holder.xps`
- `samples/file-lock-contender.xps`

## Kill

Deletes a file.

```xpscript
Kill "file.txt"
```

Delete-while-open behavior follows the target operating system rather than emulating one platform on another.

## FileCopy

```xpscript
FileCopy "source.txt", "copy.txt"
```

On Unix, the portability layer attempts to preserve executable permission bits.

## Name

Renames or moves a file.

```xpscript
Name "old.txt" As "new.txt"
```

The runtime does not silently replace a cross-filesystem rename with copy+delete because that would change atomicity, ownership and permission semantics.

## FileLen

Returns file length.

## FileDateTime

Returns the file's modification date/time.

## GetFileAttr / SetFileAttr

Read or change file attributes.

On Unix, dot-files are recognized as hidden for attribute reads. Setting the Hidden attribute does not silently rename a file with a leading dot.

## MkDir / RmDir

```xpscript
MkDir "folder"
RmDir "folder"
```

## ChDir / CurDir

Changes or reads the current working directory.

## ChDrive

Changes drive on Windows. Cross-platform behavior remains explicitly platform-specific; non-Windows callers should not assume drive-letter semantics.

## Dir

Enumerates filesystem entries using the target filesystem's native case behavior.

## Path portability

Paths are normalized by the shared portability runtime. The runtime does not rewrite filename case or hide the target filesystem's case-sensitivity rules.

Symlinks/reparse points retain operating-system semantics rather than being automatically dereferenced or rewritten.

## Newline behavior

XPScript text output follows the target OS newline convention:

- Windows: CRLF
- Linux/macOS: LF

## Samples

- `samples/core-language.xps`
- `samples/file-io-extensions.xps`
- `samples/file-io-portability.xps`
- `samples/filesystem-portability-semantics.xps`
- `samples/file-charset-bom.xps`
- `samples/file-lock-holder.xps`
- `samples/file-lock-contender.xps`
- `samples/file-delete-open-semantics.xps`
