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
| `GetFileAttr` | `GetFileAttr(fullPath)` | `fullPath`: existing file or directory path. | Returns the platform file-attribute bit mask. Multiple bits may be set at once. See the attribute list below. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `SetFileAttr` | `SetFileAttr path, attributes` or `SetFileAttr(path, attributes)` | `path`: file path; `attributes`: attribute bit mask. | Changes supported file attributes. Platform limitations are reported explicitly. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `FileCopy` | `FileCopy source, destination` | `source`: existing file; `destination`: target path. | Copies a file with XPScript's cross-platform safety and metadata behavior. | [file-io-portability.xps](../samples/file-io-portability.xps) |
| `Kill` | `Kill path` | `path`: file to delete. | Deletes a file using the portable filesystem runtime. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `Name` | `Name oldPath As newPath` | `oldPath`: current file path; `newPath`: destination path. | Renames or moves a file using target-filesystem semantics. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `MkDir` | `MkDir path` | `path`: directory to create. | Creates a directory. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `RmDir` | `RmDir path` | `path`: empty directory to remove. | Removes an empty directory. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `ChDir` | `ChDir path` | `path`: directory path. | Changes the process current directory using target-OS path semantics. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `ChDrive` | `ChDrive drive` | `drive`: Windows drive specifier. | Changes the current drive on Windows. Other platforms report explicit unsupported behavior. | [file-io-portability.xps](../samples/file-io-portability.xps) |
| `IsFile` | `IsFile(path)` | `path`: filesystem path. | Returns `True` when the path exists and is a file; otherwise `False`. | [file-io-platform-semantics.xps](../samples/file-io-platform-semantics.xps) |
| `IsDir` | `IsDir(path)` | `path`: filesystem path. | Returns `True` when the path exists and is a directory; otherwise `False`. | [file-io-platform-semantics.xps](../samples/file-io-platform-semantics.xps) |
| `Dir` | `Dir([pattern] [, mode [, maxDepth]])` | optional `pattern`: path/search mask. optional `mode`: omitted or `0` = files and directories in the current directory level; `1` = files only in the current level; `2` = directories only in the current level; `3` = files recursively. optional `maxDepth`: recursion depth for mode `3`, default `3`, valid `0..32`. Depth `0` searches only the starting directory, `1` includes one subdirectory level, and so on. Call `Dir()` again to continue the current enumeration. | Enumerates filesystem entries using target-filesystem matching semantics. `.` and `..` are always excluded. Mode `3` is bounded, skips reparse-point/symbolic-link directories, and returns paths relative to the searched directory so nested files retain their subdirectory path. | [file-io-platform-semantics.xps](../samples/file-io-platform-semantics.xps) |

| `FileInfo` | `FileInfo(path)` | `path`: existing file or directory. | Returns a metadata object with `Name`, `FullPath`, `Extension`, `Length`, `Created`, `Modified`, `Accessed`, `IsFile`, `IsDirectory`, `IsLink`, and `Attributes`. | [file-convenience.xps](../samples/file-convenience.xps) |
| `FileHash` | `FileHash(path [, algorithm])` | `algorithm`: optional `SHA256` (default), `SHA384`, `SHA512`; legacy compatibility also accepts `SHA1` and `MD5`. | Streams the file and returns an uppercase hexadecimal digest. | [file-convenience.xps](../samples/file-convenience.xps) |
| `FileEquals` | `FileEquals(path1, path2)` | two file paths. | Returns `True` when files have equal length and byte-for-byte content. | [file-convenience.xps](../samples/file-convenience.xps) |
| `Files` | `Files(pathOrPattern [, mask [, recursive [, maxDepth]]])` | path/pattern, optional mask, recursive flag, optional depth `0..32` (default `3`). | Returns a String array of matching full file paths; recursive traversal skips link/reparse directories. | [file-convenience.xps](../samples/file-convenience.xps) |
| `Directories` | `Directories(pathOrPattern [, mask [, recursive [, maxDepth]]])` | path/pattern, optional mask, recursive flag, optional depth `0..32` (default `3`). | Returns a String array of matching full directory paths and works directly with `ForAll`. | [file-convenience.xps](../samples/file-convenience.xps) |
| `ReadFile` | `ReadFile(path [, charset])` | file path and optional charset. | Reads an entire text file. UTF-8 is the default; BOMs are detected. | [file-convenience.xps](../samples/file-convenience.xps) |
| `WriteFile` | `WriteFile path, content [, charset]` | path, text, optional charset. | Replaces an entire text file. Function-call syntax is also accepted. | [file-convenience.xps](../samples/file-convenience.xps) |
| `AppendFile` | `AppendFile path, content [, charset]` | path, text, optional charset. | Appends text to a file. Function-call syntax is also accepted. | [file-convenience.xps](../samples/file-convenience.xps) |
| `ReadLines` | `ReadLines(path [, charset])` | path and optional charset. | Returns a String array with one element per text line. | [file-convenience.xps](../samples/file-convenience.xps) |
| `WriteLines` | `WriteLines path, values [, charset]` | path, array/list values, optional charset. | Writes one array/list value per line. | [file-convenience.xps](../samples/file-convenience.xps) |
| `ReadBytes` | `ReadBytes(path)` | file path. | Returns the complete file as a Byte array. | [file-convenience.xps](../samples/file-convenience.xps) |
| `WriteBytes` | `WriteBytes path, values` | path and Byte array/array-like value. | Replaces a file with the supplied bytes. | [file-convenience.xps](../samples/file-convenience.xps) |

### `GetFileAttr(fullPath)` attribute values

`GetFileAttr` returns an integer bit mask. Test a specific attribute with a bitwise `And`, for example:

```xpscript
If (GetFileAttr(fullPath) And 16) <> 0 Then
    Print "directory"
End If
```

Common attribute bits are:

- `1` (`ReadOnly`) — Windows: native read-only attribute. macOS/Linux: may be returned when the runtime/filesystem exposes a read-only attribute; Unix permission bits are not a one-to-one Windows attribute mapping.
- `2` (`Hidden`) — Windows: native Hidden attribute. macOS/Linux: XPScript guarantees this bit when the final path component starts with `.`.
- `4` (`System`) — Windows System attribute where supported; normally not meaningful on Unix-like filesystems.
- `16` (`Directory`) — directory on Windows, macOS, and Linux.
- `32` (`Archive`) — Windows Archive attribute where supported; normally not meaningful on Unix-like filesystems.
- `128` (`Normal`) — an otherwise ordinary file/path may report Normal.
- `256` (`Temporary`) — temporary attribute where the runtime/filesystem exposes it.
- `512` (`SparseFile`) — sparse-file attribute where exposed.
- `1024` (`ReparsePoint`) — Windows reparse point; on macOS/Linux .NET can use this bit for symbolic-link/reparse-style entries where exposed.
- `2048` (`Compressed`) — compressed attribute where exposed.
- `4096` (`Offline`) — Windows Offline attribute where supported; normally not meaningful on Unix-like filesystems.
- `8192` (`NotContentIndexed`) — Windows indexing attribute where supported; normally not meaningful on Unix-like filesystems.
- `16384` (`Encrypted`) — encrypted attribute where the runtime/filesystem exposes it.

The result is a bit mask, not a single value. Windows can therefore return combinations such as `Directory + Hidden = 18`. On macOS/Linux, XPScript explicitly adds `Hidden (2)` for dot-prefixed names because hidden state is conventionally represented by the filename rather than a native Windows-style attribute. Other bits are whatever the underlying .NET runtime and filesystem report for that path; do not assume Windows-only metadata such as `Archive` or `System` exists on Unix-like systems.

### Whole-file convenience API

`Files(...)`, `Directories(...)`, `ReadLines(...)`, and `ReadBytes(...)` return arrays that can be consumed directly by `ForAll`. Recursive `Files`/`Directories` use the same bounded-depth model as recursive `Dir`: default depth `3`, valid range `0..32`, and link/reparse-point directories are not traversed.

Text helpers accept .NET charset names. Common names include `utf-8`, `utf-16`, `utf-16be`, `iso-8859-1`, and other ISO/code-page names available through the platform encoding provider. UTF-8 without BOM is used when charset is omitted; readers still detect BOMs.

`FileHash` defaults to SHA-256. SHA-384 and SHA-512 are recommended alternatives. SHA-1 and MD5 are available only for compatibility with legacy file manifests and should not be chosen for security-sensitive integrity checks.

### `Dir` mode examples

```xpscript
Dim item As String

' Omitted mode / 0: files and directories at this level.
item = Dir("C:\\Temp\\*")

' 1: files only at this level.
item = Dir("C:\\Temp\\*", 1)

' 2: directories only at this level.
item = Dir("C:\\Temp\\*", 2)

' 3: recursive files, default maximum depth 3.
item = Dir("C:\\Temp\\*", 3)

' Recursive files, but only one level below C:\\Temp.
item = Dir("C:\\Temp\\*", 3, 1)

Do While item <> ""
    Print item
    item = Dir()
Loop
```

`Dir()` without arguments never starts a new search; it continues the most recently started `Dir(pattern [, mode [, maxDepth]])` enumeration. Mode values outside `0` through `3` are invalid. `maxDepth` values outside `0` through `32` are invalid. The `maxDepth` limit prevents accidental unbounded traversal of large roots such as `C:\\` or `/`.

## Copyable example

For the broadest text/Binary/locking demonstration, copy [samples/file-io-extensions.xps](../samples/file-io-extensions.xps). For file position and global handle reset, copy [samples/file-position-reset.xps](../samples/file-position-reset.xps).

Compile them with:

```powershell
xpscriptc .\samples\file-io-extensions.xps -o .\out\file-io-demo.exe --framework-dependent
xpscriptc .\samples\file-position-reset.xps -o .\out\file-position-demo.exe --framework-dependent
```


## CopyFile and MoveFile

`CopyFile(source, target [, action])` and `MoveFile(source, target [, action])` return `True` only when a file was actually copied or moved. The optional `action` defaults to `1`.

- `1` = fail if the target already exists; returns `False` and leaves both files unchanged.
- `2` = overwrite an existing target; returns `True` when the transfer succeeds.
- `3` = skip if the target already exists; returns `False` because no transfer was performed.

A missing source or unavailable destination directory returns `False`. Invalid action values raise runtime error 5. The legacy `FileCopy` statement remains available for compatibility and keeps its existing behavior.

```xpscript
ok = CopyFile("in.dat", "out.dat")
ok = CopyFile("in.dat", "out.dat", 2)
ok = MoveFile("out.dat", "archive/out.dat", 3)
```

## Path object

The built-in `Path` object centralizes cross-platform path manipulation and reuses XPscript's existing filesystem path normalization and existence checks.

```xpscript
full = Path.Combine("data", "config.json")
name = Path.FileName(full)
ext = Path.Extension(full)
dir = Path.Directory(full)
absolute = Path.Absolute(full)
relative = Path.Relative("data", absolute)
exists = Path.Exists(full)
```

`Path.Combine(left, right)` joins two path parts using the target platform separator. `Path.FileName(path)` returns the final file or directory name. `Path.Extension(path)` returns the extension including the leading dot or an empty string. `Path.Directory(path)` returns the absolute parent directory. `Path.Absolute(path)` resolves an absolute path. `Path.Relative(basePath, path)` calculates `path` relative to `basePath`. `Path.Exists(path)` returns `True` for either an existing file or an existing directory and internally reuses the existing `FileExists`/`DirExists` behavior.
