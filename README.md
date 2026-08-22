# XPScript

(c) xpagedeveloper.com 2026

Source available for testing and internal use only. No commercial rights are granted. Commercial use requires a separate written license from the copyright holder.

XPScript is a standalone programming language compiler implemented in C#/.NET 10. Source files use the `.xps` extension and can target Windows, Linux and macOS executables without requiring an external scripting runtime.

## Documentation

The language/runtime reference is maintained under `docs/` and intentionally reuses source fixtures already present under `samples/`.

Start with:

- `docs/index.md` — documentation index mapped to sample files
- `docs/core-language.md`
- `docs/arrays-lists-operators.md`
- `docs/types-classes-modules.md`
- `docs/strings-conversion-base64.md`
- `docs/math-functions.md`
- `docs/date-time.md`
- `docs/file-io-filesystem.md`
- `docs/console-process-formatting.md`
- `docs/platform-native.md`
- `docs/native-http-json.md`
- `docs/sqlite.md`
- `docs/mssql.md`
- `docs/evaluate.md`
- `docs/security.md` — security boundaries and powerful APIs
- `docs/diagnostics-security.md` — diagnostic redaction and secret-safe error policy

Negative samples intentionally demonstrate errors and are identified as such in the documentation. Older compatibility fixtures are not automatically presented as the preferred standalone XPScript API.

## Compiler

Build the compiler:

```powershell
dotnet build .\src\XPScript.Compiler\XPScript.Compiler.csproj -c Release
```

Compile for the current platform:

```powershell
xpscriptc program.xps -o program
```

Compile for a specific runtime:

```powershell
xpscriptc program.xps --runtime win-x64 -o program.exe
xpscriptc program.xps --runtime linux-x64 -o program
xpscriptc program.xps --runtime linux-arm64 -o program
xpscriptc program.xps --runtime osx-x64 -o program
xpscriptc program.xps --runtime osx-arm64 -o program
```

Compiler result formats:

```powershell
xpscriptc program.xps --result-format text
xpscriptc program.xps --result-format json
xpscriptc program.xps --result-format xml
```

A successful structured result reports `result = ok`. A compile failure reports `result = error` and includes diagnostics with source line, position, description, source code and marked code.

## Entry point

```xpscript
Sub Main()
    Print "Hello from XPScript"
End Sub
```

`Sub Initialize()` is also accepted when `Main` is not present.

## Core language

Implemented language areas include:

- `Sub`, `Function`, `Call`, `Return`, `Exit Sub`, `Exit Function`
- `Dim`, `Static`, module-level `Public` and `Private`
- scalar types: `String`, `Integer`, `Long`, `Single`, `Double`, `Currency`, `Boolean`, `Byte`, `Date`, `Variant`, `Object`
- `ByVal`, `ByRef`, `Optional`
- `Enum`
- user-defined `Type` with scalar fields and array members
- classes, constructors, destructors and properties
- `If`, `ElseIf`, `Else`
- `Select Case`
- `For`, `While`, `Do`, `ForAll`
- `GoTo`, `GoSub`, labels
- `On Error`, `Resume`, `Err`, `Error`, `Erl`
- fixed and dynamic arrays, multidimensional arrays, `ReDim Preserve`
- tagged lists
- external native-library declarations with platform-specific `.dll`, `.so` and `.dylib` selection

## Platform targeting

`Platform()` returns a stable runtime platform name that can be used in XPScript code:

```xpscript
If Platform() = "Windows" Then
    Print "Running on Windows"
ElseIf Platform() = "Linux" Then
    Print "Running on Linux"
ElseIf Platform() = "MacOS" Then
    Print "Running on macOS"
End If
```

The compiler currently supports these target RIDs:

- `win-x64`
- `win-arm64`
- `linux-x64`
- `linux-arm64`
- `osx-x64`
- `osx-arm64`

If `--runtime` is omitted, the compiler targets the current OS and process architecture.

## External native libraries

A native declaration can select a different library and exported entry point for each target platform:

```xpscript
Declare Function NativeProcessId Lib "native-process" _
    WindowsLib "kernel32.dll" WindowsAlias "GetCurrentProcessId" _
    LinuxLib "libc.so.6" LinuxAlias "getpid" _
    MacOSLib "libSystem.B.dylib" MacOSAlias "getpid" _
    () As Integer
```

Selection is based on the target RID passed to the compiler, not the operating system on which the compiler itself is running.

Application-local native-library packaging, exact x64/arm64 asset selection, executable-directory resolution and native loader security are documented in `docs/platform-native.md` and permanently verified by the native security workflows on Windows, Ubuntu and macOS. Scalar ABI coverage also includes the supported x64/arm64 target matrix.

## Operators and coercion

XPScript supports arithmetic, comparison, logical and string operators including:

- `+ - * / \\ Mod ^`
- `= <> < > <= >=`
- `And Or Not Xor Eqv Imp`
- `Like`
- `&`

The `+` operator is deliberately forgiving. For example, String + Integer appends the integer as text, while Integer + a numeric String performs numeric addition.

## File I/O

XPScript supports sequential, Binary and Random file access.

```xpscript
Dim f As Integer
Dim value As String

f = FreeFile
Open "data.txt" For Input As #f Charset "utf-8"
value = Input$(5, #f)
Close #f
```

File `Input$(count, #fileNumber)` is separate from interactive console input.

File locking uses operating-system file locks:

```xpscript
Open "data.bin" For Binary As #f
Lock #f, 1 To 100
' protected file region
Unlock #f, 1 To 100
Close #f
```

Binary lock ranges are byte based and 1-based at the XPScript surface. Random ranges map to records. Sequential modes lock the file as a whole.

General file/path operations use .NET filesystem APIs so Windows, Linux and macOS retain their native path and filesystem behavior. `ChDrive` is intentionally Windows-only. Cross-platform differences such as file locking, case sensitivity, permissions, open-file deletion, symlinks, newline handling and file sharing are tracked in `todo/done/cross-platform-runtime-todo.md` and were verified independently on each OS.

Text I/O supports `Charset` and the independent `Encoding "base64"` storage layer. See `docs/file-io-filesystem.md` and `docs/text-io-console.md`.

## Process execution

`Shell()` is platform-aware:

- Windows: executables, `.cmd`, `.bat`, `.ps1`
- Linux/macOS: executables, executable/shebang scripts, `.sh`/`.bash`, and `.ps1` when PowerShell is installed

Arguments are passed using structured process arguments where possible to avoid unnecessary shell re-parsing. `.cmd`/`.bat` execution still crosses a `cmd.exe` command-shell boundary and must not receive untrusted concatenated command text.

See `docs/platform-native.md`, `docs/console-process-formatting.md` and `docs/security.md`.

## HTTP

The XPScript-native HTTP API uses `HttpClient` and `HttpResponse`:

```xpscript
Dim http As New HttpClient
Dim response As HttpResponse

Call http.SetHeader("Accept", "application/json")
Set response = http.Get("https://api.example.com/users")

Print CStr(response.StatusCode)
Print response.Body
```

Supported request methods are `Get`, `Post`, `Put`, `Patch` and `Delete`. Headers and timeout can be configured per client.

Header names/values are validated before request construction. CR/LF header injection is rejected. Native HTTP URLs must be absolute `http://` or `https://` URLs. Application-level host/network allowlisting is still required when user-controlled URLs could create SSRF risk.

## JSON

XPScript provides native JSON classes and helper functions:

```xpscript
Dim obj As New JsonObject
Call obj.Set("name", "Alice")
Call obj.Set("active", True)

Print JsonStringify(obj)
```

Available APIs include:

- `JsonDocument.Parse` / `Stringify`
- `JsonObject.Get`, `Set`, `Remove`, `Contains`, `Count`
- `JsonArray.Add`, `Get`, `Set`, `RemoveAt`, `Count`
- `JsonElement.Type`, `Value`
- `JsonParse`, `JsonStringify`, `JsonEncode`, `JsonDecode`

See `docs/native-http-json.md` for the preferred standalone API.

## Runtime helpers

The standard runtime includes string, conversion, inspection, math, date/time, formatting, filesystem, process, Base64 and URL helpers. Examples include:

- strings: `Len`, `Left`, `Right`, `Mid`, `Instr`, `Replace`, `StrConv`, `StrLeft`, `StrRight`
- conversions: `CStr`, `CInt`, `CLng`, `CDbl`, `CDate`, `CType`, `CVDate`
- inspection: `TypeName`, `DataType`, `IsArray`, `IsDate`, `IsNumeric`, `IsObject`, `IsList`
- math: `Abs`, `Round`, `Sqr`, `Sin`, `Cos`, `Tan`, `Rnd`
- date/time: `Now`, `Today`, `DateAdd`, `DateDiff`, `DatePart`
- filesystem: `ChDir`, `ChDrive`, `CurDir`, `Dir`, `FileCopy`, `Kill`, `MkDir`, `RmDir`
- Base64/URL: `Base64Encode`, `Base64Decode`, `Base64DecodeBinary`, `ToBase64`, `FromBase64`, `UrlEncode`, `UrlDecode`

The complete sample-based grouping is in `docs/index.md`.

## Samples

The `samples` directory contains XPScript source fixtures for core language features, classes/lists, arrays/operators, HTTP/JSON, text/file I/O, platform behavior, native libraries, Evaluate, security diagnostics and compiler compatibility behavior.

Documentation should reuse these samples instead of creating duplicate example programs unless a new example is explicitly needed.

## Security

XPScript source execution is code execution. APIs such as `Shell`, general file I/O, HTTP, native interop and compatibility COM/OLE surfaces use the privileges of the current process and are not automatically sandboxed.

See `docs/security.md` for trust boundaries and deployment guidance. Diagnostic redaction policy is documented in `docs/diagnostics-security.md`. Static hardening and permanent verification are tracked in `todo/security-review-todo.md`.

## Implementation status

The tracked implementation plan is maintained in:

- `todo/runtime-reference-todo.md`
