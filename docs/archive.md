# Archive

`Archive` provides ZIP support by default and optional extended archive support when the constructor's second argument is the literal `True`.

```xpscript
Dim zip As New Archive("backup.zip")
Dim extended As New Archive("backup.7z", True)
```

The `True`/`False` argument must be a compile-time literal so XPScript can include only the dependencies required by the application.

## Iterating entries

Existing collection APIs remain available:

```xpscript
ForAll entry In archive.Entries
    Print entry.FullName
End ForAll

ForAll entry In archive.Files()
    Print entry.FullName
End ForAll

ForAll entry In archive.Folders()
    Print entry.FullName
End ForAll
```

An iterator-style API is also available:

```xpscript
Dim entry As ArchiveEntry
Set entry = archive.GetFirstEntry()

While entry Is Not Nothing
    If entry.IsFile Then
        Print "File: " & entry.FullName
    ElseIf entry.IsFolder Then
        Print "Folder: " & entry.FullName
    End If

    Set entry = archive.GetNextEntry(entry)
Wend
```

`IsFile` and `IsFolder` are mutually exclusive. Every `ArchiveEntry` is one or the other. `IsDirectory` is not part of the public XPScript API.

## ArchiveEntry properties

The public entry surface includes:

- `Name`
- `FullName`
- `Extension`
- `Size`
- `CompressedSize`
- `CompressionRatio`
- `Created`
- `Modified`
- `CRC`
- `IsFile`
- `IsFolder`
- `IsEncrypted`

## Format capability matrix

| Format | Default Archive | `Archive(..., True)` read | `Archive(..., True)` write | Notes |
| --- | --- | --- | --- | --- |
| ZIP | Read/write | Read/write | Read/write | Password-protected ZIP read requires `True`; encrypted ZIP writing is not supported. |
| 7z | No | Yes | Yes | Existing archives can be rebuilt when modified. |
| TAR | No | Yes | Yes | Existing archives can be rebuilt when modified. |
| GZip | No | Yes | Yes | Single-entry format. |
| TAR.GZip / TGZ | No | Yes | Yes | Multi-entry compressed TAR. |
| TAR.BZip2 | No | Yes | Yes | Multi-entry compressed TAR. |
| TAR.LZip | No | Yes | Yes | Multi-entry compressed TAR. |
| RAR | No | Yes | No | Read/extract only. |
| BZip2 | No | Yes | No | Raw single-stream write is not exposed. |
| LZip | No | Yes | No | Raw single-stream write is not exposed. |
| XZ | No | Yes | No | Read-only. |
| Zstandard | No | Yes | No | Read-only. |
| ARC / ARJ / ACE / LZW and other supported legacy readers | No | Where supported | No | Read-only. |

When the compiler can determine that a requested operation is unsupported, it reports that at compile time instead of deferring the failure to runtime.

## Passwords

`Password` is currently a read/decryption capability.

```xpscript
Dim archive As New Archive("secure.zip", True)
archive.Password = "secret"
archive.Open()
```

Creating or modifying a password-protected ZIP is not supported. XPScript reports this at compile time when it can determine the invalid write sequence.

## In-memory archives

ZIP can be created entirely in memory without extended support:

```xpscript
Dim archive As New Archive()
archive.Create("zip")
archive.AddText("manifest.txt", "hello")

Dim data As Variant
data = archive.ToBytes()
```

Extended in-memory archives are opened with `True`. TAR, 7z and supported compressed-TAR formats can also be created and rebuilt in memory.

```xpscript
Dim seed() As Byte
Dim archive As New Archive(seed, True)
archive.Create("7z")
archive.AddText("manifest.txt", "hello")

Dim data As Variant
data = archive.ToBytes()
```

## Platform status

Core ZIP, in-memory ZIP and iterator regression tests are configured for Windows, Linux and macOS. Extended archive regressions currently run in the Linux Archive workflow. Android, iOS and browser/WASM require separate runtime/AOT/trimming validation before they should be considered fully supported.
