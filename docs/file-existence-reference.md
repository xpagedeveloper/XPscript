# File and directory existence helpers

XPscript provides four Boolean filesystem helpers. They are cross-platform and return `False` when the requested file or directory does not exist.

| Function | Syntax | Result |
|---|---|---|
| `FileExists` | `FileExists(path)` | `True` when `path` identifies an existing regular file. |
| `DirExists` | `DirExists(path)` | `True` when `path` identifies an existing directory. |
| `IsFile` | `IsFile(path)` | `True` when the path exists and is a file. |
| `IsDir` | `IsDir(path)` | `True` when the path exists and is a directory. |

```xpscript
Dim filePath As String
Dim directoryPath As String

filePath = "./data/input.txt"
directoryPath = "./data"

Print CStr(FileExists(filePath))
Print CStr(DirExists(directoryPath))
Print CStr(IsFile(filePath))
Print CStr(IsDir(directoryPath))
```

`FileExists` and `IsFile` both test for an existing file in the current runtime implementation. `DirExists` and `IsDir` both test for an existing directory. The paired names are retained as supported XPscript API surfaces.

These helpers belong to the File I/O and filesystem API documented in `file-io-reference.md`.