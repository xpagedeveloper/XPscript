# File and filesystem command reference

This is the complete reference for XPScript file I/O and filesystem commands. Each entry includes syntax, parameters, behavior, and a complete `.xps` program that can be copied and compiled.

For broader compatibility notes, also see the repository [compatibility matrix](../COMPATIBILITY.md).

## File handles and text I/O

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `FreeFile` | `FreeFile()` | none | Returns an available XPScript file number. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Open` | `Open path For mode As #number [Len = recordLength]` | `path`: file path; `mode`: `Input`, `Output`, `Append`, `Binary`, or `Random`; `number`: file number; `recordLength`: optional Random record size. | Opens a file using the requested access mode. | [file-io-portability.xps](../samples/file-io-portability.xps) |
| `Open ... Charset` | `Open path For Input|Output|Append As #number Charset charset [Encoding transferEncoding]` | `charset`: text encoding such as `utf-8`; `transferEncoding`: optional `base64`; other arguments are the normal `Open` arguments. | Opens a sequential text file with explicit character and optional transfer encoding. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Close` | `Close [#number]` | optional `number`: one open file number. | Closes the selected file. With no number, closes all currently open XPScript file numbers. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Reset` | `Reset` | none | Flushes and closes all currently open XPScript file numbers. It is equivalent to `Close` without a file number and makes those numbers available to `FreeFile` again. | [file-position-reset.xps](../samples/file-position-reset.xps) |
| `Print #` | `Print #number, value` | `number`: output file number; `value`: text/value to write. | Writes a line to a sequential output/append file. | [textio-console.xps](../samples/textio-console.xps) |
| `Write #` | `Write #number, value [, value ...]` | `number`: output file number; `value`: one or more values. | Writes comma-separated, type-aware sequential data suitable for `Input #`. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Line Input #` | `Line Input #number, variable` | `number`: input file number; `variable`: String/Variant target. | Reads one complete text line from a sequential input file. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Input #` | `Input #number, variable` | `number`: input file number; `variable`: target variable. | Reads the next delimited sequential value from an input file. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Input$` | `Input$(count, #number)` | `count`: requested character/byte count according to the file mode; `number`: input file number. | Reads a fixed amount of content from an open file. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `EOF` | `EOF(number)` | `number`: input file number. | Returns `True` when the current input file has no more data to read. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `LOF` | `LOF(number)` | `number`: open file number. | Returns the length of the open file in bytes. | [file-position-reset.xps](../samples/file-position-reset.xps) |
| `Seek` | `Seek(number)` | `number`: open file number. | Returns the current one-based byte position for sequential/Binary files, or the current one-based record position for Random files. | [file-position-reset.xps](../samples/file-position-reset.xps) |
| `Seek` statement | `Seek #number, position` | `number`: open file number; `position`: one-based byte position, or Random record position. | Moves the current file position. Reader buffers are discarded and writer buffers are flushed before repositioning. | [file-position-reset.xps](../samples/file-position-reset.xps) |
| `Loc` | `Loc(number)` | `number`: open file number. | Returns the current logical location. Binary/Random mode reports the most recent logical record/location; sequential mode follows XPScript compatibility block semantics. | [file-position-reset.xps](../samples/file-position-reset.xps) |

## Binary, Random, and locking

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Put` | `Put #number, position, value` | `number`: Binary/Random file number; `position`: byte/record position, or omitted position where supported; `value`: data to write. | Writes binary or Random-mode data. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Get` | `Get #number, position, variable` | `number`: Binary/Random file number; `position`: byte/record position; `variable`: destination. | Reads binary or Random-mode data. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Lock` | `Lock #number [, start [To end]]` | `number`: Binary/Random file number; `start`/`end`: optional byte or record range. | Acquires an OS-backed file or range lock. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Unlock` | `Unlock #number [, start [To end]]` | `number`: file number; `start`/`end`: range previously locked. | Releases an OS-backed file or range lock. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |

## File metadata and filesystem operations

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `FileLen` | `FileLen(path)` | `path`: file path. | Returns the file length in bytes using XPScript's portable filesystem runtime. | [file-io-portability.xps](../samples/file-io-portability.xps) |
| `FileDateTime` | `FileDateTime(path)` | `path`: file path. | Returns the file's last-write date/time. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `GetFileAttr` | `GetFileAttr(path)` | `path`: file path. | Returns the file attribute bit mask. Platform-specific attributes follow XPScript portability semantics. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `SetFileAttr` | `SetFileAttr path, attributes` or `SetFileAttr(path, attributes)` | `path`: file path; `attributes`: attribute bit mask. | Changes supported file attributes. Platform limitations are reported explicitly. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `FileCopy` | `FileCopy source, destination` | `source`: existing file; `destination`: target path. | Copies a file with XPScript's cross-platform safety and metadata behavior. | [file-io-portability.xps](../samples/file-io-portability.xps) |
| `Kill` | `Kill path` | `path`: file to delete. | Deletes a file using the portable filesystem runtime. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `Name` | `Name oldPath As newPath` | `oldPath`: current file path; `newPath`: destination path. | Renames or moves a file using target-filesystem semantics. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `MkDir` | `MkDir path` | `path`: directory to create. | Creates a directory. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `RmDir` | `RmDir path` | `path`: empty directory to remove. | Removes an empty directory. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `ChDir` | `ChDir path` | `path`: directory path. | Changes the process current directory using target-OS path semantics. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `ChDrive` | `ChDrive drive` | `drive`: Windows drive specifier. | Changes the current drive on Windows. Other platforms report explicit unsupported behavior. | [file-io-portability.xps](../samples/file-io-portability.xps) |
| `Dir` | `Dir([pattern])` | optional `pattern`: path/search mask. Call `Dir()` again to continue enumeration. | Enumerates filesystem entries using target-filesystem matching semantics. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |

## Copyable example

For the broadest text/Binary/locking demonstration, copy [samples/file-io-extensions.xps](../samples/file-io-extensions.xps). For file position and global handle reset, copy [samples/file-position-reset.xps](../samples/file-position-reset.xps).

Compile them with:

```powershell
xpscriptc .\samples\file-io-extensions.xps -o .\out\file-io-demo.exe --framework-dependent
xpscriptc .\samples\file-position-reset.xps -o .\out\file-position-demo.exe --framework-dependent
```
