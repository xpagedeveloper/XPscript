# XPscript programming skill

Use this skill when writing, reviewing, explaining, or modifying XPscript (`.xps`) programs.

## Goal

Produce idiomatic XPscript that matches the repository's current compiler/runtime behavior. Prefer repository documentation and samples over assumptions based on VB, LotusScript, PowerShell, PHP, C#, or other similar languages.

## Source of truth

When syntax or behavior is uncertain, consult these files in this order:

1. `docs/language-reference.md` — language syntax and built-ins.
2. `docs/api-reference.md` — runtime objects and higher-level APIs.
3. `docs/forall-iteration.md` — the common `ForAll` iterable contract.
4. `docs/file-io-reference.md` — filesystem and file I/O.
5. Database-specific docs such as `docs/sqlite.md` and `docs/mssql.md`.
6. `samples/*.xps` and `demo/**/*.xps` — executable examples.
7. Compiler/runtime implementation only when documentation is ambiguous.

Do not invent a built-in or class member because it exists in a related language.

## Core language model

XPscript is case-insensitive and VB/LotusScript-like, but it has its own runtime and compatibility rules.

Prefer explicit declarations in generated programs:

```xpscript
Option Declare

Sub Main()
    Dim message As String
    message = "Hello from XPscript"
    Print message
End Sub
```

Use `Sub ... End Sub` for procedures without a result and `Function ... End Function` for value-returning procedures.

Functions return values by assigning to the function name:

```xpscript
Function Add(a As Integer, b As Integer) As Integer
    Add = a + b
End Function
```

Optional parameters are supported:

```xpscript
Function Greeting(name As String, Optional prefix As String = "Hello") As String
    Greeting = prefix & " " & name
End Function
```

Use block control flow unless a repository sample proves a shorter form is supported:

```xpscript
If value > 0 Then
    Print "positive"
ElseIf value < 0 Then
    Print "negative"
Else
    Print "zero"
End If
```

## Classes and object lifetime

All XPscript classes are instance-based. Create class objects with `New`.

```xpscript
p = New Path("src/test/data.json")
```

Do not model classes as global singletons or static namespaces when writing XPscript. This is important for APIs such as XPDB and XPAI because multiple independent instances must be able to exist at the same time.

Example pattern:

```xpscript
Dim first As Variant
Dim second As Variant

first = New SomeClass(...)
second = New SomeClass(...)
```

Each instance must keep its own state.

When using XPDB or XPAI, follow the exact constructor/member names shown by the current database/AI docs and samples. Never infer those signatures from older examples or another language.

For XPAi structured output, prefer defining the desired result shape as an XPscript class and passing an instance to `SetResultClass` rather than hand-building JSON Schema. Public fields and public readable properties form the schema; private backing state is excluded. Use `SystemPrompt` and `UserPrompt` for the common two-part prompt shape, and use `SetJsonSchema` only when a raw provider-specific schema is required. See `docs/xpai-structured-output.md` and `samples/xpai-structured-output.xps`.

## HTTP client security

`HttpClient` blocks private and local network destinations by default. Leave this protection enabled when a URL may contain request data or other untrusted input.

For a trusted application-controlled intranet or local endpoint, opt in on that client instance:

```xpscript
Dim http As New HttpClient
http.AllowPrivateNetwork = True
Set response = http.Get("http://127.0.0.1:8080/health")
```

Do not enable `AllowPrivateNetwork` merely to make a user-supplied URL work. Request framing and transport headers such as `Host`, `Content-Length`, and `Transfer-Encoding` are runtime-managed and must not be set with `SetHeader`.

## Arrays, Lists, and ForAll

XPscript arrays use XPscript array semantics and support helpers such as `Array`, `ReDim`, `LBound`, `UBound`, `Join`, `Explode`, and array helper functions documented in the language reference.

`ForAll` is the single first-class iteration model for supported iterable values. Use the same syntax for one-dimensional arrays, Lists, JSON arrays/enumerables, database row/result collections, filesystem arrays, other runtime enumerable values, and XPscript classes that expose an iterator.

```xpscript
files = Files("src", "*.xps", True)

ForAll file In files
    Print file
End ForAll
```

Only one-dimensional arrays can be used with `ForAll`. Multidimensional arrays are not flattened and raise runtime error 13; use explicit nested `For` loops for their dimensions.

Lists retain their tag/value alias semantics under `ForAll`. Do not treat an XPscript List as an ordinary array when code relies on `ListTag`, `IsElement`, or keyed list access.

A user class can participate in `ForAll` by exposing a public parameterless `Iterator()` function that returns another supported iterable value:

```xpscript
Class Words
    Public Function Iterator() As Variant
        Iterator = Array("one", "two", "three")
    End Function
End Class

Sub Main()
    Dim words As Words
    Set words = New Words()

    ForAll word In words
        Print word
    End ForAll
End Sub
```

`Iterator()` must not return the object itself. Do not invent collection-specific loop constructs when the runtime value can integrate with `ForAll`.

Do not redeclare the `ForAll` iteration alias immediately before the loop unless a current sample specifically requires it.

## Filesystem APIs

Prefer modern convenience functions for new code unless compatibility with legacy `Dir()` behavior is specifically required.

### Existence and type

```xpscript
If IsFile("config.json") Then
    Print "file"
End If

If IsDir("data") Then
    Print "directory"
End If
```

Existing compatibility functions may include `FileExists` and `DirExists`; prefer `IsFile`/`IsDir` when the distinction between file and directory matters.

### Dir

```xpscript
name = Dir("data/*")
name = Dir("data/*", 1)      ' files only
name = Dir("data/*", 2)      ' directories only
name = Dir("data/*", 3)      ' recursive files, default maxDepth 3
name = Dir("data/*", 3, 1)   ' recursive files, max one level down
```

`Dir()` with no arguments continues the current enumeration. `.` and `..` are always excluded. Recursive mode is bounded: default depth is `3`, valid depth is `0..32`, and link/reparse directories are not traversed.

Prefer `Files()` or `Directories()` when stateful `Dir()` continuation is unnecessary:

```xpscript
files = Files("src", "*.xps", True)
dirs = Directories("src")
```

These return XPscript arrays suitable for `ForAll`.

### FileInfo

```xpscript
info = FileInfo("archive.zip")

Print info.Name
Print info.FullPath
Print info.Extension
Print info.Length
Print info.Created
Print info.Modified
Print info.Accessed
Print info.IsFile
Print info.IsDirectory
Print info.IsLink
Print info.Attributes
```

### File hashes and equality

```xpscript
hash = FileHash("archive.zip")
hash = FileHash("archive.zip", "SHA256")
hash = FileHash("archive.zip", "SHA384")
hash = FileHash("archive.zip", "SHA512")
```

SHA256 is the default. SHA1 and MD5 may exist for legacy compatibility; do not recommend them for security-sensitive integrity checks.

```xpscript
If FileEquals("a.bin", "b.bin") Then
    Print "same content"
End If
```

### CopyFile and MoveFile

Both return Boolean. The optional action defaults to `1`.

```xpscript
ok = CopyFile("a.bin", "b.bin")
ok = CopyFile("a.bin", "b.bin", 2)
ok = MoveFile("b.bin", "archive/b.bin", 3)
```

Actions:

- `1` = fail if the target exists.
- `2` = overwrite the target.
- `3` = skip if the target exists.

A successful transfer returns `True`; a failed/skipped transfer returns `False` according to the documented policy. Legacy `FileCopy` remains available for compatibility.

### Whole-file text I/O

```xpscript
content = ReadFile("config.json")
content = ReadFile("config.json", "utf-8")

WriteFile "config.json", content
WriteFile "config.json", content, "utf-8"
AppendFile "app.log", "done"
```

Supported charset names include `utf-8`, UTF-16 variants, ISO encodings, and code-page names supported by the runtime encoding provider. UTF-8 is the normal default.

### Lines

```xpscript
lines = ReadLines("users.txt")
WriteLines "copy.txt", lines

ForAll line In lines
    Print line
End ForAll
```

### Bytes

```xpscript
data = ReadBytes("image.png")
WriteBytes "copy.png", data
```

Use these instead of handle-based binary I/O when the whole file comfortably fits in memory. Use `Open`/`Get`/`Put` for streaming, random access, locking, or very large files.

## Path class

`Path` is an instance class. The stored path is supplied once in the constructor.

```xpscript
p = New Path("src/test/data.json")

Print p.FileName()
Print p.FileNameWithoutExtension()
Print p.Extension()
Print p.Directory()
Print p.Root()
Print p.Normalize()
Print p.Absolute()
Print p.Relative("src/test/archive.json")
Print p.ChangeExtension(".xml")
Print p.IsAbsolute()
Print p.Exists()
Print p.Combine("child.txt")
```

Do not write static forms such as `Path.Absolute(path)` unless the language documentation explicitly adds such an API in the future.

`ChangeExtension()` returns the changed path string; it does not rename a file.

## GetFileAttr

`GetFileAttr(path)` returns a bit mask. Test attributes with bitwise `And`.

```xpscript
If (GetFileAttr(fullPath) And 16) <> 0 Then
    Print "directory"
End If
```

Common bits include `ReadOnly=1`, `Hidden=2`, `System=4`, `Directory=16`, `Archive=32`, `Normal=128`, `Temporary=256`, `SparseFile=512`, `ReparsePoint=1024`, `Compressed=2048`, `Offline=4096`, `NotContentIndexed=8192`, and `Encrypted=16384`.

Windows exposes native Windows attributes. On macOS/Linux XPscript synthesizes `Hidden=2` for names beginning with `.`; other bits depend on what the runtime/filesystem exposes. Do not assume Windows-only metadata exists on Unix-like systems.

## Error handling

Use XPscript error handling, not C#/Java exception syntax:

```xpscript
On Error GoTo Handler

content = ReadFile("config.json")
Exit Sub

Handler:
Print "Error " & CStr(Err) & ": " & Error$
```

Use `On Error Resume Next` only when the program intentionally handles an expected failure immediately afterward.

## Cross-platform rules

XPscript targets Windows, Linux, and macOS. When writing portable programs:

- Prefer `Path` operations over manual separator manipulation when paths are non-trivial.
- Do not assume drive letters outside Windows.
- Do not assume filesystem case sensitivity is the same on every OS.
- Treat symlinks/reparse points carefully in recursive traversal.
- Use documented charset names instead of platform-default text encodings.
- Avoid hard-coded Windows paths in examples intended to be portable.

## LLM coding workflow

When asked to write an XPscript program:

1. Identify whether the request needs plain language syntax, a runtime class, filesystem I/O, database access, AI, UI, web APIs, or native interop.
2. Read the relevant current reference/sample if any API name or signature is uncertain.
3. Prefer `Option Declare` and explicit types where practical.
4. Instantiate every class with `New`; preserve independent per-instance state.
5. Prefer modern convenience APIs (`Files`, `ReadFile`, `FileInfo`, `Path`, etc.) over legacy/stateful APIs unless compatibility is requested.
6. Use XPscript arrays/Lists rather than CLR/.NET collection assumptions.
7. Use `ForAll` as the common loop for supported iterable values; do not create type-specific loop models.
8. Keep code cross-platform unless the user explicitly requests one OS.
9. Do not silently invent unsupported overloads, properties, or optional arguments.
10. If modifying the language or runtime, add/update an executable `.xps` regression sample and documentation in the same change.
11. If the change affects how an LLM should write XPscript, update this skill in the same PR.

## Verification checklist

Before presenting code as valid XPscript, check:

- Every variable required by `Option Declare` is declared.
- Every class construction uses `New`.
- Optional arguments match documented signatures.
- `ForAll` is used only with supported iterables and never with multidimensional arrays.
- Iterable user classes expose public parameterless `Iterator()` returning another iterable.
- Function return values use XPscript semantics.
- File/directory recursion is bounded where appropriate.
- Path behavior is portable where the program claims to be portable.
- Charset names are explicit when encoding matters.
- The code does not confuse legacy `FileCopy` with Boolean `CopyFile`.
- The code does not use static `Path.*` calls for the instance-based `Path` class.

## Maintenance rule

This file is a living LLM contract for XPscript. Any PR that changes user-visible language syntax, built-ins, runtime classes, constructors, object lifecycle, function signatures, filesystem behavior, database/AI APIs, iteration behavior, or recommended idioms should review and update this file in the same PR when the change affects code generation guidance.

Keep this skill concise and operational. Link to authoritative docs for exhaustive reference instead of duplicating the entire language manual.
