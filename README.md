# LS Lite Compiler

LS Lite is a standalone LotusScript-inspired compiler written in C#/.NET 10. It does not depend on HCL Notes/Domino. A small set of explicitly documented compatibility facades, including `NotesSAXParser` and `NotesSAXAttributeList`, is implemented locally by LS Lite without loading Notes/Domino classes.

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
- `Err`, `Err()`, `Erl`, `Erl()`, `Error`, `Error()`, and `Error$`
- the `Error number [, description]` statement
- external Windows DLL declarations with `Declare Function` and `Declare Sub`
- `On Event ... From ... Call ...` for the standalone SAX compatibility parser
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

LS Lite provides a standalone LotusScript-style error state and control-flow implementation.

Supported forms include:

- `Err` and `Err()`
- `Erl` and `Erl()`
- `Error`, `Error()`, `Error$`, `Error(number)`, and `Error$(number)`
- `Error number`
- `Error number, description`
- `On Error GoTo label`
- error-number-specific `On Error n GoTo label`
- `On Error Resume Next`
- `On Error GoTo 0`
- `Resume`
- `Resume Next`
- `Resume label`

Example:

```lotusscript
Sub ReadFile()
    On Error GoTo Handler

    Open "test.txt" For Input As #1
    Exit Sub

Handler:
    Print "Error " & CStr(Err) & ": " & Error$
    Resume Next
End Sub
```

`Error$` without an argument returns the current error description. `Error$(number)` returns the registered description for that error number. The `Error` statement raises a trappable LS Lite runtime error.

Common .NET/OS exceptions are normalized into LotusScript-compatible numbers before `On Error` dispatch. Current mappings include:

- file or directory not found -> `53`
- overflow -> `6`
- division by zero -> `11`
- subscript/index out of range -> `9`
- type mismatch/format conversion -> `13`
- input past end of file -> `62`
- permission/access denied -> `70`

`Erl` exposes the protected LS Lite statement position recorded when the error was trapped. It is stable for error handling and diagnostics inside the generated program, but it is not currently guaranteed to equal the physical source-file line number.

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

A source-level native declaration named `Sleep` remains a normal P/Invoke call. The standalone built-in `Sleep seconds` statement described below is used when no such native declaration is invoked.

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

## Environment and OS compatibility

Functions that normally depend on a host application or operating system use explicit LS Lite standalone behavior.

### Environ / Environ$

`Environ("NAME")` and `Environ$("NAME")` read the generated process environment. A missing variable returns an empty string.

A numeric argument uses LS Lite's deterministic compatibility rule: environment entries are sorted case-insensitively as `NAME=VALUE` strings and returned using a one-based index.

### Shell

`Shell(command [, windowStyle])` starts an external process using the target operating system. LS Lite returns `33` after the process is successfully launched. It does not wait for the process to finish and the value is not the child process exit code.

On Windows, `.cmd` and `.bat` files are launched through `COMSPEC`/`cmd.exe`. A basic LotusScript-style window-state mapping is applied when `windowStyle` is supplied.

### Sleep

`Sleep seconds` suspends the current thread for the requested number of seconds. Fractional seconds are accepted.

```lotusscript
Sleep 0.25
```

### GetObject

`GetObject` is a Windows-only COM compatibility function in LS Lite.

- `GetObject(pathname)` binds to a COM moniker for the supplied path.
- `GetObject("", "Prog.Id")` creates an instance from a registered COM ProgID.

This is not a Notes/Domino object lookup mechanism.

### Stop

`Stop` breaks into an attached debugger. If no debugger is attached, LS Lite raises runtime error `5`, allowing it to be handled by `On Error` rather than silently terminating the process.

## Formatting

LS Lite supports:

- `Format`
- `Format$`
- `FormatNumber`
- `FormatPercent`

`Format` and `Format$` support normal .NET-compatible format masks plus these named compatibility formats:

- `General Number`
- `Currency`
- `Fixed`
- `Standard`
- `Percent`
- `Scientific`
- `Yes/No`
- `True/False`
- `On/Off`

`FormatNumber` and `FormatPercent` are explicit LS Lite standalone extensions. They use the current process culture and support optional decimal-place and negative-number formatting arguments.

## Miscellaneous runtime functions

### Evaluate

`Evaluate(expression [, host])` is deliberately standalone. It evaluates scalar expressions using the LS Lite runtime and does not provide the Domino `@Formula` engine.

For example:

```lotusscript
Print CStr(Evaluate("1+2*3"))
```

prints `7`.

Expressions containing `@` are rejected with LS Lite runtime error `5` instead of pretending that Domino formula functions are available.

### InputBox

`InputBox(prompt [, title [, default]])` is console based. It writes the prompt to standard output and reads from standard input. If input reaches EOF, the default value is returned.

### MessageBox / MsgBox

`MessageBox` and `MsgBox` use LS Lite's console compatibility implementation. The title and message are written to standard output and the function returns `1` for OK.

### Beep

`Beep` uses the console beep implementation where the target runtime supports it.

### Print

`Print` writes to standard output. `Print #fileNumber` remains the file-output form.

## Standalone SAX compatibility

LS Lite provides self-contained compatibility facades named `NotesSAXParser`, `NotesSAXAttributeList`, and `NotesSAXException`. They do not load or require HCL Notes/Domino.

The parser is event driven. Event handlers can be connected using LotusScript-style syntax and are called synchronously while the XML stream is being parsed:

```lotusscript
Sub Main()
    Dim parser As NotesSAXParser

    Set parser = New NotesSAXParser("<root id=""7""><child>text</child></root>")

    On Event SAX_StartDocument From parser Call SAXStartDocument
    On Event SAX_StartElement From parser Call SAXStartElement
    On Event SAX_Characters From parser Call SAXCharacters
    On Event SAX_EndElement From parser Call SAXEndElement
    On Event SAX_EndDocument From parser Call SAXEndDocument

    parser.Process
End Sub

Sub SAXStartElement(Source As NotesSAXParser, ByVal ElementName As String, Attributes As NotesSAXAttributeList)
    Print ElementName
    If Attributes.Length > 0 Then
        Print Attributes.GetName(1) & "=" & Attributes.GetValue(1)
    End If
End Sub
```

Supported parser operations include:

- `New NotesSAXParser()`
- `New NotesSAXParser(input)`
- `New NotesSAXParser(input, output)`
- `SetInput`
- `SetOutput`
- `Process`
- `Parse`
- `Output`
- `On Event eventName From parser Call handler`
- `On Event eventName From parser Remove [handler]`

Input can be XML text, a file path, a byte array, a text reader, or a stream at runtime. External XML entity resolution is disabled by the compatibility runtime.

Events currently emitted by the standalone parser are:

- `SAX_StartDocument`
- `SAX_EndDocument`
- `SAX_StartElement`
- `SAX_EndElement`
- `SAX_Characters`
- `SAX_IgnorableWhiteSpace`
- `SAX_ProcessingInstruction`
- `SAX_FatalError`

`NotesSAXAttributeList` provides:

- `Length`
- `GetName(indexOrName)`
- `GetValue(indexOrName)`
- `GetType(indexOrName)`

Numeric attribute access is one based. XML attributes currently report type `CDATA`.

The façade intentionally focuses on the event-driven parsing behavior needed by standalone LS Lite applications. Domino-specific parser integration and the full set of DTD/entity callback semantics are not currently emulated.

See `samples/runtime-sax.ls` for the CI-tested event-callback example.

## Built-in functions

The runtime implements a broad standalone subset of LotusScript standard functions.

### Strings

`Len`, `LenB`, `Left`, `Right`, `Mid`, `UCase`, `LCase`, `Trim`, `LTrim`, `RTrim`, `FullTrim`, `StrReverse`, `Chr`, `Asc`, `Instr`, `StrComp`, `Replace`, `Space`, `String`, `Split`, `Join`

### Conversion and type inspection

`CStr`, `CByte`, `CInt`, `CLng`, `CDbl`, `CSng`, `CCur`, `CBool`, `CVar`, `CDat`, `CDate`, `DataType`, `TypeName`, `Val`, `IsNumeric`, `IsArray`, `IsDate`, `IsEmpty`, `IsNull`, `IsObject`, `IsScalar`, `Bin`, `Hex`, `Oct`, `Str`

### Math

`Abs`, `Int`, `Fix`, `Round`, `Sqr`, `Sgn`, `Sin`, `Cos`, `Tan`, `ATn`, `ATn2`, `ASin`, `ACos`, `Exp`, `Log`, `Fraction`, `Rnd`, `Randomize`

### Date and time

`Now`, `Today`, `Date`, `Time`, `Year`, `Month`, `Day`, `Hour`, `Minute`, `Second`, `DateNumber`, `TimeNumber`, `DateValue`, `TimeValue`, `Weekday`, `MonthName`, `WeekdayName`, `DateAdd`, `DateDiff`, `DatePart`, `Timer`

### Host, formatting, and interaction

`Environ`, `Format`, `Format$`, `FormatNumber`, `FormatPercent`, `Shell`, `Sleep`, `Evaluate`, `GetObject`, `InputBox`, `MessageBox`, `MsgBox`, `Beep`, `Print`, `Stop`, `Error`, `Error$`, `Err`, `Erl`

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
- `Command`

Binary positioning is byte based and one based at the language surface. Random positioning uses the configured record length and one-based record numbers. `Loc` reports mode-specific position information.

`Get` and `Put` currently support the standalone scalar types and strings used by the runtime, including Byte, Boolean, Integer, Long, Single, Double, Currency, Date, and String.

## Continuous integration

The GitHub Actions workflow uses .NET 10 on Windows and performs:

1. compiler restore and build
2. compilation and execution of `samples/compatibility.ls`
3. verification of string, number, date, and file functionality
4. compilation and execution of `samples/lists-classes.ls`
5. verification of List and class behavior
6. compilation and execution of `samples/core-language.ls`
7. verification of arrays, ReDim Preserve, LBound/UBound, ByRef, Select Case, error handling, Resume variants, GoTo, GoSub, labels, native declarations, Binary/Random Get/Put/Loc, With, Static, and Deftype
8. compilation and execution of `samples/runtime-sax.ls`
9. verification of environment access, formatting, Error/Error$/Err/Erl handling, missing-file error 53, Resume Next, Sleep, Shell, Evaluate, MessageBox, SAX event callbacks, and SAX attribute access

This checks both the compiler itself and generated Windows executables.

## Remaining compatibility work

LS Lite is not intended to provide the general Notes/Domino object model. The SAX names documented above are standalone compatibility facades rather than Domino objects.

Areas that still require additional compatibility work include:

- general Notes/Domino classes outside the documented SAX compatibility facade
- complete Notes SAX DTD/entity callback parity
- user-defined Type/UDT support
- parameterized or indexed properties
- complete class inheritance edge cases
- native DLL declarations involving UDTs, pointers, callbacks, or complex marshaling
- Binary/Random `Get` and `Put` for UDT records and other complex aggregate values
- physical source-line fidelity for `Erl`
- every locale-specific LotusScript coercion and formatting edge case
- native GUI implementations of `MessageBox` and `InputBox`

## Architecture

1. `LotusTranspiler` protects source literals and orchestrates the compiler passes.
2. `ExtendedCompatibilityTranspiler` normalizes host-dependent functions, `Error$` shorthand, and SAX event syntax before the core language pass.
3. `CoreCompatibilityTranspiler` adds arrays, ByRef, Select Case, error handling, labels, native declarations, advanced file I/O, With, Static, and Deftype support.
4. `AdvancedLotusTranspiler` handles the base language, classes, Lists, expressions, and procedure generation.
5. `LotusRuntime` provides standard functions and basic runtime services.
6. `LSExtendedRuntime` provides environment, OS, formatting, interaction, Evaluate, GetObject, Shell, Sleep, and Stop compatibility behavior.
7. `LSExtendedErrorRuntime` maps common .NET/OS exceptions to LotusScript-compatible error numbers.
8. `NotesSAXParser`, `NotesSAXAttributeList`, and `LSSaxRuntime` provide the standalone event-driven SAX facade.
9. `LSArray` provides LotusScript-style bounds, dimensions, ReDim, and Preserve semantics.
10. `LSList<T>` provides tagged List semantics and `ForAll` aliases.
11. `LSRef<T>` provides shared object-reference semantics for `Set`, `Nothing`, and `Delete`.
12. `LSControlRuntime` provides error-handler and GoSub state.
13. `LSFileRuntime` provides unified sequential, Binary, and Random file access.
14. `CompilerDriver` creates a temporary .NET 10 SDK project and publishes the Windows executable.
