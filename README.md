# LS Lite Compiler

LS Lite is a standalone LotusScript-inspired compiler written in C#/.NET 10. It intentionally does not implement HCL Notes/Domino classes.

LS Lite transpiles supported source code to C# and uses the .NET SDK to publish a Windows x64 executable.

## Requirements

- .NET 10 SDK on the development or compile machine
- Windows is not required to run the compiler, but the generated target is currently `win-x64`
- Generated executables are self-contained by default

## Build the compiler

```powershell
dotnet build .\src\LSLite.Compiler\LSLite.Compiler.csproj -c Release
```

Run it directly:

```powershell
dotnet run --project .\src\LSLite.Compiler -- .\samples\hello.ls -o .\out\Hello.exe
```

Publish the compiler itself:

```powershell
dotnet publish .\src\LSLite.Compiler\LSLite.Compiler.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\compiler-publish
```

Then compile an LS Lite source file:

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

`Main` takes precedence when both exist.

## Supported language features

Current compiler support includes:

- `Option Declare`
- `Sub` and `Function`
- LotusScript-style function return assignment
- `Return`
- `Dim`
- scalar assignment and `Let`
- `If`, `ElseIf`, `Else`, `End If`
- `For`, `To`, `Step`, `Next`
- `While`, `Wend`
- `Do`, `Do While`, `Do Until`, `Loop`, `Loop While`, `Loop Until`
- `Exit For`, `Exit Do`, `Exit While`, `Exit Sub`, `Exit Function`
- `Call`
- `Print`
- comments using `'`
- operators including `+ - * / Mod`, comparisons, `And`, `Or`, `Not`, and `&`
- scalar types including `String`, `Integer`, `Long`, `Double`, `Single`, `Boolean`, `Byte`, `Currency`, `Date`, `Variant`, and `Object`

## LotusScript List support

LS Lite implements tagged LotusScript-style lists separately from normal arrays.

Supported syntax and operations:

- `Dim name List As Type`
- tagged reads and writes with `list("tag")`
- automatic creation of a list element when assigning a new tag
- `IsElement(list("tag"))`
- `ForAll value In list`
- `ListTag(value)` inside `ForAll`
- assignment to the `ForAll` alias updates the list element
- `Exit ForAll`
- `Erase list("tag")`
- `Erase list`
- list fields inside classes

Example:

```lotusscript
Dim users List As String

users("admin") = "Fredrik"
users("guest") = "Guest"

ForAll value In users
    Print ListTag(value) & ": " & value
End ForAll

If IsElement(users("guest")) Then
    Erase users("guest")
End If
```

## Class and object support

LS Lite supports user-defined classes without Notes/Domino dependencies.

Implemented features:

- `Class` and `End Class`
- class fields
- methods using `Sub` and `Function`
- constructors using `Sub New`
- destructors using `Sub Delete`
- parameterless `Property Get`
- parameterless `Property Set`
- public and private classes and members
- object variables
- shared object references
- `Dim object As ClassName`
- `Dim object As New ClassName(...)`
- `Set object = New ClassName(...)`
- `Set object2 = object1`
- `Set object = Nothing`
- `object Is Nothing`
- `object Is Not Nothing`
- `Delete object`
- `Me`
- method and property access through object references

`Set object2 = object1` shares the LS Lite object reference. `Delete object1` invokes `Sub Delete` and invalidates the shared reference, so aliases such as `object2` also evaluate as `Nothing`.

Example:

```lotusscript
Class Person
    Private mName As String

    Sub New(name As String)
        Me.mName = name
    End Sub

    Public Property Get Name As String
        Name = Me.mName
    End Property

    Public Property Set Name As String
        Me.mName = Name
    End Property

    Public Function Describe() As String
        Describe = Me.mName
    End Function

    Sub Delete()
        Me.mName = ""
    End Sub
End Class

Sub Main()
    Dim person As Person
    Dim alias As Person

    Set person = New Person("Fredrik")
    Set alias = person

    Print alias.Name

    Delete person

    If alias Is Nothing Then
        Print "Deleted"
    End If
End Sub
```

See `samples/lists-classes.ls` for a CI-tested example combining lists, classes, properties, constructors, object references, `Set`, `New`, `Delete`, and `Me`.

## Built-in functions

The runtime implements a broad standalone subset of LotusScript standard functions.

### Strings

`Len`, `LenB`, `Left`, `Right`, `Mid`, `UCase`, `LCase`, `Trim`, `LTrim`, `RTrim`, `FullTrim`, `StrReverse`, `Chr`, `Asc`, `Instr`, `StrComp`, `Replace`, `Space`, `String`, `Split`, `Join`, `Format`

### Conversion and type inspection

`CStr`, `CByte`, `CInt`, `CLng`, `CDbl`, `CSng`, `CCur`, `CBool`, `CVar`, `CDat`, `CDate`, `DataType`, `TypeName`, `Val`, `IsNumeric`, `IsArray`, `IsDate`, `IsEmpty`, `IsNull`, `IsObject`, `IsScalar`, `Bin`, `Hex`, `Oct`, `Str`

### Math

`Abs`, `Int`, `Fix`, `Round`, `Sqr`, `Sgn`, `Sin`, `Cos`, `Tan`, `ATn`, `ATn2`, `ASin`, `ACos`, `Exp`, `Log`, `Fraction`, `Rnd`, `Randomize`

### Date and time

`Now`, `Today`, `Date`, `Time`, `Year`, `Month`, `Day`, `Hour`, `Minute`, `Second`, `DateNumber`, `TimeNumber`, `DateValue`, `TimeValue`, `Weekday`, `MonthName`, `WeekdayName`, `DateAdd`, `DateDiff`, `DatePart`, `Timer`

### File and environment

Supported file operations include:

- `FreeFile`
- `Open ... For Input/Output/Append/Binary/Random As #n`
- `Close`
- `Print #`
- `Write #`
- `Input #`
- `Line Input #`
- `EOF`
- `LOF`
- `Seek`
- `FileLen`
- `FileDateTime`
- `GetFileAttr`
- `SetFileAttr`
- `FileCopy`
- `Kill`
- `Name ... As ...`
- `MkDir`
- `RmDir`
- `ChDir`
- `CurDir`
- `Dir`
- `Environ`
- `Command`

### Console replacements

- `InputBox` reads from standard input
- `MsgBox` writes to standard output and returns `1`
- `Beep` uses the console beep implementation where supported

## Continuous integration

The GitHub Actions workflow uses .NET 10 on Windows and performs:

1. restore
2. compiler build
3. compilation of `samples/compatibility.ls`
4. execution and verification of the standard runtime compatibility sample
5. compilation of `samples/lists-classes.ls`
6. execution and verification of the List and class compatibility sample

This checks both the compiler itself and generated Windows executables.

## Not implemented yet

LS Lite is not yet a complete LotusScript clone. Remaining areas include:

- Notes/Domino classes
- LotusScript array declaration and full array semantics
- scalar `ByRef` semantics
- `Select Case`
- `On Error`, `Resume`, and `Err`
- `With`
- labels and `GoTo`
- external native functions using `Declare`
- parameterized or indexed properties
- complete class inheritance edge cases
- every locale-specific LotusScript coercion rule
- native GUI implementations of `MsgBox` and `InputBox`

## Architecture

1. `LotusTranspiler` invokes the advanced language transpiler.
2. `AdvancedLotusTranspiler` parses the supported LotusScript-compatible syntax and emits C#.
3. `LotusRuntime` provides standard functions and file operations.
4. `LSList<T>` provides tagged List semantics and `ForAll` aliases.
5. `LSRef<T>` provides shared object-reference semantics for `Set`, `Nothing`, and `Delete`.
6. `CompilerDriver` creates a temporary .NET 10 SDK project.
7. `dotnet publish` creates the requested Windows executable.
8. Temporary compiler files are removed after publishing.
