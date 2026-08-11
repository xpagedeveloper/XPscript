# XPScript

(c) xpagedeveloper.com 2026

XPScript is a standalone programming language compiler implemented in C#/.NET 10. Source files use the `.xps` extension and can target Windows, Linux and macOS executables without requiring an external scripting runtime.

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

Application-local native-library packaging and architecture-specific native asset staging remain tracked implementation items. See `todo/cross-platform-runtime-todo.md`.

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

General file/path operations use .NET filesystem APIs so Windows, Linux and macOS retain their native path and filesystem behavior. `ChDrive` is intentionally Windows-only. Cross-platform differences such as file locking, case sensitivity, permissions, open-file deletion, symlinks, newline handling and file sharing are tracked in `todo/cross-platform-runtime-todo.md` and must be verified independently on each OS.

Text I/O supports `Charset` and the independent `Encoding "base64"` storage layer. See `docs/text-io-console.md`.

## Process execution

`Shell()` is platform-aware:

- Windows: executables, `.cmd`, `.bat`, `.ps1`
- Linux/macOS: executables, executable/shebang scripts, `.sh`/`.bash`, and `.ps1` when PowerShell is installed

Arguments are passed using structured process arguments where possible to avoid unnecessary shell re-parsing. Explicit shell syntax such as pipes/redirection remains a separate compatibility/security design item.

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

See `docs/http-json-compatibility.md` for the current API surface.

## Runtime helpers

The standard runtime includes string, conversion, inspection, math, date/time, formatting, filesystem, process, Base64 and URL helpers. Examples include:

- strings: `Len`, `Left`, `Right`, `Mid`, `Instr`, `Replace`, `StrConv`, `StrLeft`, `StrRight`
- conversions: `CStr`, `CInt`, `CLng`, `CDbl`, `CDate`, `CType`, `CVDate`
- inspection: `TypeName`, `DataType`, `IsArray`, `IsDate`, `IsNumeric`, `IsObject`, `IsList`
- math: `Abs`, `Round`, `Sqr`, `Sin`, `Cos`, `Tan`, `Rnd`
- date/time: `Now`, `Today`, `DateAdd`, `DateDiff`, `DatePart`
- filesystem: `ChDir`, `ChDrive`, `CurDir`, `Dir`, `FileCopy`, `Kill`, `MkDir`, `RmDir`
- Base64/URL: `Base64Encode`, `Base64Decode`, `Base64DecodeBinary`, `ToBase64`, `FromBase64`, `UrlEncode`, `UrlDecode`

## Samples

The `samples` directory contains standalone `.xps` examples for core language features, classes/lists, arrays/operators, HTTP/JSON, text/file I/O, platform behavior, native libraries and compiler compatibility behavior.

## Implementation status

The tracked implementation plan is maintained in:

- `todo/runtime-reference-todo.md`
- `todo/cross-platform-runtime-todo.md`

Items marked `[>]` are implemented or in progress but are waiting for explicit verification while automated workflows are disabled.

Current development that must not trigger CI is kept on branch:

`runtime-development-no-ci`

## Project structure

- `src/XPScript.Compiler` — compiler, preprocessors, transpilers and generated runtime sources
- `samples` — XPScript test and regression programs
- `examples` — reusable end-user example programs as documentation is expanded
- `docs` — language/runtime documentation
- `todo` — implementation tracking

XPScript source code and public documentation use XPScript naming and `.xps` source files consistently.
