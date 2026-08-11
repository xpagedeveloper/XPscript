# XPScript

(c) xpagedeveloper.com 2026

XPScript is a standalone programming language compiler implemented in C#/.NET 10. Source files use the `.xps` extension and compile to Windows executables without requiring an external scripting runtime.

## Compiler

Build the compiler:

```powershell
dotnet build .\src\XPScript.Compiler\XPScript.Compiler.csproj -c Release
```

Compile a script:

```powershell
xpscriptc program.xps -o program.exe
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
- user-defined `Type` with scalar fields
- classes, constructors, destructors and properties
- `If`, `ElseIf`, `Else`
- `Select Case`
- `For`, `While`, `Do`, `ForAll`
- `GoTo`, `GoSub`, labels
- `On Error`, `Resume`, `Err`, `Error`, `Erl`
- fixed and dynamic arrays, multidimensional arrays, `ReDim Preserve`
- tagged lists
- external Windows DLL declarations

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

Text I/O supports `Charset` and the independent `Encoding "base64"` storage layer. See `docs/text-io-console.md`.

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
- Base64/URL: `Base64Encode`, `Base64Decode`, `ToBase64`, `FromBase64`, `UrlEncode`, `UrlDecode`

## Samples

The `samples` directory contains standalone `.xps` examples for core language features, classes/lists, arrays/operators, HTTP/JSON, text/file I/O and compiler compatibility behavior.

## Implementation status

The tracked implementation plan is maintained in:

`todo/runtime-reference-todo.md`

Items marked `[>]` are implemented or in progress but are waiting for explicit verification while automated workflows are disabled.

Current development that must not trigger CI is kept on branch:

`runtime-development-no-ci`

## Project structure

- `src/XPScript.Compiler` — compiler, preprocessors, transpilers and generated runtime sources
- `samples` — XPScript example programs
- `docs` — language/runtime documentation
- `todo` — implementation tracking

XPScript source code and public documentation use XPScript naming and `.xps` source files consistently.
