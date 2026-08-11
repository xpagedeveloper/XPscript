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
- `Option Base 0` and `Option Base 1`
- `DefBool`, `DefByte`, `DefCur`, `DefDbl`, `DefInt`, `DefLng`, `DefSng`, `DefStr`, and `DefVar`
- `Sub` and `Function`
- `Static Sub` and `Static Function`
- LotusScript-style function return assignment
- `Return`
- `Dim` and `Static`
- scalar `ByRef`
- array parameters
- scalar assignment and `Let`
- `If`, `ElseIf`, `Else`, `End If`
- `Select Case`, value cases, ranges, relational cases, and `Case Else`
- `For`, `To`, `Step`, `Next`
- `While`, `Wend`
- `Do`, `Do While`, `Do Until`, `Loop`, `Loop While`, `Loop Until`
- `Exit For`, `Exit Do`, `Exit While`, `Exit Sub`, `Exit Function`
- `Call`
- `Print`
- `With` and `End With`
- labels
- `GoTo`
- `GoSub` and `Return`
- `On Error GoTo label`
- error-number-specific `On Error n GoTo label`
- `On Error Resume Next`
- `On Error GoTo 0`
- `Resume`, `Resume Next`, and `Resume label`
- `Err`, `Erl`, `Error`, and the `Error` statement
- external Windows DLL declarations with `Declare Function` and `Declare Sub`
- comments using `'`
- operators including `+ - * / Mod`, comparisons, `And`, `Or`, `Not`, and `&`
- scalar types including `String`, `Integer`, `Long`, `Double`, `Single`, `Boolean`, `Byte`, `Currency`, `Date`, `Variant`, and `Object`

## Array support

LS Lite implements fixed and dynamic LotusScript-style arrays independently of .NET native array syntax.

Supported array features include:

- fixed arrays such as `Dim values(10) As Long`
- explicit lower and upper bounds such as `Dim values(1 To 10) As Long`
- multidimensional arrays such as `Dim matrix(1 To 5, 0 To 9) As Double`
- up to eight dimensions
- `Option Base 0` and `Option Base 1`
- dynamic arrays such as `Dim values() As String`
- `ReDim`
- `ReDim Preserve`
- `LBound`
- `UBound`
- array element reads and writes
- arrays passed to procedures
- `Erase` for fixed and dynamic arrays

Example:

```lotusscript
Option Base 1

Sub SetFirst(values() As Long)
    values(1) = 99
End Sub

Sub Main()
    Dim values() As Long

    ReDim values(1 To 2)
    values(1) = 10
    values(2) = 20

    ReDim Preserve values(1 To 3)
    values(3) = 30

    Call SetFirst(values)

    Print CStr(LBound(values))
    Print CStr(UBound(values))
    Print CStr(values(1))
End Sub
```

`ReDim Preserve` keeps existing values. For multidimensional arrays, preservation follows the LotusScript-compatible restriction that only the upper bound of the last dimension can change while preserving data.

## ByRef support

Scalar parameters can be explicitly declared `ByRef`.

```lotusscript
Sub Increment(ByRef value As Long)
    value = value + 1
End Sub

Sub Main()
    Dim value As Long
    value = 10
    Call Increment(value)
    Print CStr(value)
End Sub
```

The generated runtime uses reference cells so assignment inside the called procedure updates the original caller variable.

Arrays are passed as shared array objects and array element changes are visible to the caller.

## Select Case

Supported forms include:

```lotusscript
Select Case value
Case 1
    Print "one"
Case 2 To 10
    Print "range"
Case Is > 10
    Print "high"
Case Else
    Print "other"
End Select
```

## Error handling

Supported error handling includes:

```lotusscript
On Error GoTo Handler

Error 123, "Example error"
Print "continues after Resume Next"
GoTo Done

Handler:
    Print CStr(Err) & ": " & Error()
    Resume Next

Done:
On Error GoTo 0
```

Also supported:

- retrying the failing statement with `Resume`
- continuing after the failing statement with `Resume Next`
- resuming at a label with `Resume label`
- `On Error Resume Next`
- error-specific handlers
- `Err`
- `Erl`
- `Error()` and `Error(number)`
- raising errors with `Error number` or `Error number, description`

## GoTo and GoSub

Text labels can be used as branch targets.

```lotusscript
GoSub Worker
Print "returned"
GoTo Done

Worker:
    Print "worker"
    Return

Done:
```

`GoSub` return positions are tracked per procedure and nested calls use a runtime stack.

## With

`With` supports member access using leading dots.

```lotusscript
With person
    .Name = "Fredrik"
    Print .Name
End With
```

Nested `With` blocks are supported by the compatibility preprocessor.

## Static variables and procedures

Local `Static` variables retain their value between calls.

```lotusscript
Function Counter() As Long
    Static count As Long
    count = count + 1
    Counter = count
End Function
```

`Static Sub` and `Static Function` are also supported. Local variables in a static procedure retain their values between calls.

## Deftype

Default variable types can be selected by the first letter of a name.

```lotusscript
DefInt A-C

Sub Main()
    Dim apple
    apple = 42
    Print TypeName(apple)
End Sub
```

Letter ranges and comma-separated ranges are supported.

## External DLL declarations

Windows native functions and procedures can be declared using LotusScript-style declarations.

```lotusscript
Declare Function GetTickCount Lib "kernel32.dll" Alias "GetTickCount" () As Long
Declare Sub Sleep Lib "kernel32.dll" Alias "Sleep" (ByVal milliseconds As Long)
```

LS Lite generates .NET P/Invoke declarations. `Lib`, `Alias`, Function/Sub, scalar parameters, `ByVal`, and scalar return types are supported.

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

## File I/O

Sequential, Binary, and Random modes are supported.

Supported file operations include:

- `FreeFile`
- `Open ... For Input/Output/Append/Binary/Random As #n`
- Random record length using `Len = n`
- `Close`
- `Print #`
- `Write #`
- `Input #`
- `Line Input #`
- `EOF`
- `LOF`
- `Seek`
- `Loc`
- `Get`
- `Put`
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

Binary positioning is byte based and one based at the language surface. Random positioning uses the configured record length and one-based record numbers. `Loc` reports mode-specific position information.

`Get` and `Put` currently support the standalone scalar types and strings used by the runtime, including Byte, Boolean, Integer, Long, Single, Double, Currency, Date, and String.

## Console replacements

- `InputBox` reads from standard input
- `MsgBox` writes to standard output and returns `1`
- `Beep` uses the console beep implementation where supported

## Continuous integration

The GitHub Actions workflow uses .NET 10 on Windows and performs:

1. compiler restore and build
2. compilation and execution of `samples/compatibility.ls`
3. verification of string, number, date, and file functionality
4. compilation and execution of `samples/lists-classes.ls`
5. verification of List and class behavior
6. compilation and execution of `samples/core-language.ls`
7. verification of arrays, ReDim Preserve, LBound/UBound, ByRef, Select Case, error handling, Resume variants, GoTo, GoSub, labels, native declarations, Binary/Random Get/Put/Loc, With, Static, and Deftype

This checks both the compiler itself and generated Windows executables.

## Remaining compatibility work

LS Lite is not intended to provide Notes/Domino APIs. Areas that still require additional compatibility work include:

- Notes/Domino classes
- user-defined Type/UDT support
- parameterized or indexed properties
- complete class inheritance edge cases
- native DLL declarations involving UDTs, pointers, callbacks, or complex marshaling
- Binary/Random `Get` and `Put` for UDT records and other complex aggregate values
- every locale-specific LotusScript coercion edge case
- native GUI implementations of `MsgBox` and `InputBox`

## Architecture

1. `LotusTranspiler` protects source literals and orchestrates the compiler passes.
2. `CoreCompatibilityTranspiler` adds arrays, ByRef, Select Case, error handling, labels, native declarations, advanced file I/O, With, Static, and Deftype support.
3. `AdvancedLotusTranspiler` handles the base language, classes, Lists, expressions, and procedure generation.
4. `LotusRuntime` provides standard functions and basic runtime services.
5. `LSArray` provides LotusScript-style bounds, dimensions, ReDim, and Preserve semantics.
6. `LSList<T>` provides tagged List semantics and `ForAll` aliases.
7. `LSRef<T>` provides shared object-reference semantics for `Set`, `Nothing`, and `Delete`.
8. `LSControlRuntime` provides error-handler and GoSub state.
9. `LSFileRuntime` provides unified sequential, Binary, and Random file access.
10. `CompilerDriver` creates a temporary .NET 10 SDK project and publishes the Windows executable.
