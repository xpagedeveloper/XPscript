# LS Lite Compiler

LS Lite is a small LotusScript-inspired compiler written in C#/.NET. It intentionally does **not** implement Notes/Domino classes.

It transpiles supported LotusLite source to C# and then uses the installed .NET SDK to publish a Windows x64 single-file executable.

## Requirements

- .NET 10 SDK on the development/compile machine.
- Windows is **not** required for the compiler itself, but the generated target is `win-x64`.
- The generated executable is self-contained by default.

## Build the compiler

```powershell
dotnet build .\src\LSLite.Compiler\LSLite.Compiler.csproj -c Release
```

Run it directly:

```powershell
dotnet run --project .\src\LSLite.Compiler -- .\samples\hello.ls -o .\out\Hello.exe
```

Or publish the compiler itself:

```powershell
dotnet publish .\src\LSLite.Compiler\LSLite.Compiler.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\compiler-publish
```

Then:

```powershell
.\compiler-publish\lslitec.exe .\samples\hello.ls -o .\out\Hello.exe
```

## Entry point

A source file must contain either:

```lotusscript
Sub Main()
End Sub
```

or:

```lotusscript
Sub Initialize()
End Sub
```

`Main` takes precedence if both exist.

## Supported language features

Current compiler support:

- `Option Declare` (accepted as a no-op)
- `Sub` / `End Sub`
- `Function` / `End Function`
- LotusScript-style function return assignment:
  `MyFunction = value`
- `Return value`
- `Dim name As Type`
- assignment and `Let`
- `If / ElseIf / Else / End If`
- `For / To / Step / Next`
- `While / Wend`
- `Do`, `Do While`, `Do Until`, `Loop`, `Loop While`, `Loop Until`
- `Exit For`, `Exit Do`, `Exit While`, `Exit Sub`, `Exit Function`
- `Call Procedure(...)`
- normal procedure/function calls
- `Print expression`
- `'` comments
- operators: `+ - * / Mod`, `= <> < <= > >=`, `And Or Not`, `&`
- types: `String`, `Integer`, `Long`, `Double`, `Single`, `Boolean`, `Byte`, `Date`, `Variant`, `Object`

## Built-in functions

Implemented in the first runtime:

### Strings
`Len`, `Left`, `Right`, `Mid`, `UCase`, `LCase`, `Trim`, `LTrim`, `RTrim`,
`Chr`, `Asc`, `Instr`, `Replace`, `Space`, `String`, `Split`, `Join`

### Conversion
`CStr`, `CInt`, `CLng`, `CDbl`, `CSng`, `CBool`, `Val`, `IsNumeric`,
`Hex`, `Oct`, `Format`

### Math
`Abs`, `Int`, `Fix`, `Round`, `Sqr`, `Rnd`

### Date/time
`Now`, `Today`, `Date`, `Time`, `Year`, `Month`, `Day`, `Hour`, `Minute`, `Second`

### Console replacements
`InputBox` reads from stdin.
`MsgBox` writes to stdout and returns `1`.

## Deliberately not implemented yet

This is a basic compiler, not a complete LotusScript clone. The following need a later compiler/runtime phase:

- Notes/Domino classes
- classes and user-defined types
- `ByRef`
- arrays declared with LotusScript array syntax
- `Select Case`
- `On Error`, `Resume`, `Err`
- file I/O statements
- `New`, object lifetime semantics
- `Set` assignment
- `With`
- labels / `GoTo`
- `Declare` external native functions
- all locale-specific LotusScript coercion edge cases
- GUI implementation of `MsgBox` / `InputBox`

## Example

```lotusscript
Option Declare

Function Square(n As Long) As Long
    Square = n * n
End Function

Sub Main()
    Dim i As Long

    For i = 1 To 5
        Print "Square = " & CStr(Square(i))
    Next i
End Sub
```

Compile:

```powershell
lslitec.exe program.ls -o program.exe
```

## Architecture

1. `LotusTranspiler` parses the supported line-oriented LotusScript subset.
2. It emits a C# program.
3. The generated C# embeds `LotusRuntime`.
4. `CompilerDriver` creates a temporary SDK project.
5. `dotnet publish` produces a self-contained, single-file `win-x64` EXE.
6. The temporary project is removed and the EXE is copied to the requested path.

The runtime is intentionally isolated so future LotusScript built-ins can be added without changing most of the compiler.
