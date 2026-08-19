# XPScript Command Reference

This page is the compact syntax reference for XPScript commands, functions and runtime objects. Each entry shows the accepted call form, explains its parameters and links to a working sample when one exists. Topic pages under `docs/` provide the longer behavioral notes.

Parameter notation:

- `value` means any compatible XPScript expression.
- `[parameter]` means optional.
- `...` means the form may accept additional values of the same kind.
- Array indexes follow the array's actual lower/upper bounds unless a command explicitly documents zero-based indexing.

## Application runtime

| Command/property | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Application.ArgCount` | `Application.ArgCount` | none | Number of command-line arguments. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.Args(index)` | `Application.Args(index)` | `index`: zero-based argument index | Returns one command-line argument. Invalid indexes raise error 9. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.Args` | `Application.Args` | none | Returns a detached String-array copy of all arguments. | [application-runtime-args-copy.xps](../samples/application-runtime-args-copy.xps) |
| `Application.CommandLine` | `Application.CommandLine` | none | Convenience string formed by joining argument values with one space. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.ExecutablePath` | `Application.ExecutablePath` | none | Full path of the running executable. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.ExecutableFileName` | `Application.ExecutableFileName` | none | Executable file name only. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.ExecutableDirectory` | `Application.ExecutableDirectory` | none | Directory containing the executable. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.Path` | `Application.Path` | none | Alias of `ExecutablePath`. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.FileName` | `Application.FileName` | none | Alias of `ExecutableFileName`. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.TempPath` | `Application.TempPath` | none | OS/user temporary directory. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.TempFolder` | `Application.TempFolder` | none | Alias of `TempPath`. | [application-runtime.xps](../samples/application-runtime.xps) |

## Core language and procedures

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Option Declare` | `Option Declare` | none | Requires variables to be declared before use. | [core-language.xps](../samples/core-language.xps) |
| `Option Base` | `Option Base 0` or `Option Base 1` | base: `0` or `1` | Sets the default lower bound for arrays whose lower bound is omitted. | [type-array-option-base.xps](../samples/type-array-option-base.xps) |
| `DefInt` | `DefInt A-Z` | letter range | Gives matching undeclared names the Integer default type where compatibility rules allow it. | [core-language.xps](../samples/core-language.xps) |
| `Dim` | `Dim name As Type` | `name`: variable name; `Type`: XPScript type | Declares a variable. Array bounds may be added after the name. | [core-language.xps](../samples/core-language.xps) |
| `Sub` | `Sub Name([parameters]) ... End Sub` | parameter list | Declares a procedure without a return value. | [functions.xps](../samples/functions.xps) |
| `Function` | `Function Name([parameters]) As Type ... End Function` | parameter list; return type | Declares a procedure that returns a value. | [functions.xps](../samples/functions.xps) |
| `Call` | `Call Procedure(arguments)` | procedure arguments | Calls a Sub/Function while discarding any return value. | [functions.xps](../samples/functions.xps) |
| `ByRef` | `ByRef name As Type` | parameter name/type | Passes a parameter by reference. | [functions.xps](../samples/functions.xps) |
| `ByVal` | `ByVal name As Type` | parameter name/type | Passes a value copy. | [functions.xps](../samples/functions.xps) |
| `Optional` | `Optional name As Type [= default]` | parameter name/type/default | Declares an optional parameter. | [functions.xps](../samples/functions.xps) |
| `Static` | `Static name As Type` | variable name/type | Declares procedure-local state that persists between calls. | [language-extensions.xps](../samples/language-extensions.xps) |
| `If` | `If condition Then ... [Else ...] End If` | Boolean-compatible condition | Conditional execution. | [core-language.xps](../samples/core-language.xps) |
| `Select Case` | `Select Case value ... Case ... End Select` | expression and case values | Multi-branch conditional. | [core-language.xps](../samples/core-language.xps) |
| `For` | `For i = start To finish [Step step] ... Next` | loop variable/start/end/optional step | Numeric loop. | [core-language.xps](../samples/core-language.xps) |
| `ForAll` | `ForAll item In collection ... End ForAll` | loop item and List/collection | Iterates collection/list elements. | [lists-classes.xps](../samples/lists-classes.xps) |
| `GoTo` | `GoTo label` | target label | Jumps to a label in the same procedure. | [core-language.xps](../samples/core-language.xps) |
| `GoSub` | `GoSub label` | target label | Calls a label block and returns with `Return`. | [core-language.xps](../samples/core-language.xps) |
| `Return` | `Return [value]` | optional value depending on context | Returns from a procedure/evaluation or from a `GoSub` target. | [evaluate-xpscript.xps](../samples/evaluate-xpscript.xps) |
| `On Error` | `On Error GoTo label` or `On Error Resume Next` | error target/mode | Installs procedure error handling. | [core-language.xps](../samples/core-language.xps) |
| `Resume` | `Resume`, `Resume Next`, or `Resume label` | optional target | Continues after a handled error. | [nested-resume-targets.xps](../samples/nested-resume-targets.xps) |
| `Error` | `Error number [, description]` | error number; optional text | Raises an XPScript runtime error. | [core-language.xps](../samples/core-language.xps) |
| `With` | `With object ... End With` | object/value expression | Uses a shared expression for member access inside the block. | [language-extensions.xps](../samples/language-extensions.xps) |

## Arrays and Lists

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `ReDim` | `ReDim array(bounds)` | target array and bounds | Allocates/resizes a dynamic array. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `ReDim Preserve` | `ReDim Preserve array(bounds)` | target array and new bounds | Resizes while preserving supported existing elements. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `Erase` | `Erase array` | array variable | Clears/deallocates array contents according to array type. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `LBound` | `LBound(array [, dimension])` | array; optional dimension | Returns lower bound. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `UBound` | `UBound(array [, dimension])` | array; optional dimension | Returns upper bound. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `Array` | `Array(value1, value2, ...)` | values | Creates a Variant array from supplied values. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `Join` | `Join(array [, delimiter])` | array; optional delimiter | Joins array values into one String. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `Explode` | `Explode(text [, delimiter])` | text; optional delimiter | Splits text into an array. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `ArrayAppend` | `ArrayAppend(array, value)` | target array/value | Returns/appends array content according to XPScript array helper semantics. | [evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps) |
| `ArrayGetIndex` | `ArrayGetIndex(array, value)` | array/value | Returns the matching element index using runtime comparison rules. | [evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps) |
| `ArrayUnique` | `ArrayUnique(array)` | array | Returns values with duplicates removed. | [evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps) |
| `ArraySlice` | `ArraySlice(array, start [, count])` | array/start/optional count | Returns a slice. | [evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps) |
| `ArraySplice` | `ArraySplice(array, start, count [, replacement])` | array/start/count/optional replacement | Removes/replaces a range. | [evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps) |
| `IsElement` | `IsElement(list(tag))` | list element expression | Tests whether a List element exists. | [lists-classes.xps](../samples/lists-classes.xps) |
| `ListTag` | `ListTag(list)` | List | Returns List tags/keys. | [lists-classes.xps](../samples/lists-classes.xps) |

## String, conversion and inspection

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `CStr` | `CStr(value)` | value | Converts to String. | [coercion.xps](../samples/coercion.xps) |
| `CByte` | `CByte(value)` | value | Converts to Byte. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CInt` | `CInt(value)` | value | Converts to Integer. | [coercion.xps](../samples/coercion.xps) |
| `CLng` | `CLng(value)` | value | Converts to Long. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CSng` | `CSng(value)` | value | Converts to Single. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CDbl` | `CDbl(value)` | value | Converts to Double. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CCur` | `CCur(value)` | value | Converts to Currency-compatible numeric value. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CBool` | `CBool(value)` | value | Converts to Boolean. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CDate` / `CDat` / `CVDate` | `CDate(value)` | value | Converts compatible input to Date. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `CVar` | `CVar(value)` | value | Returns value as Variant. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CType` | `CType(value, typeName)` | value; target type name | Converts using a runtime XPScript type name. | [coercion.xps](../samples/coercion.xps) |
| `TypeName` | `TypeName(value)` | value | Returns the XPScript runtime type name. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `DataType` | `DataType(value)` | value | Returns the runtime datatype code. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsArray` | `IsArray(value)` | value | Tests for an array. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsDate` | `IsDate(value)` | value | Tests whether a value can be treated as Date. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsNull` | `IsNull(value)` | value | Tests Null state. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsEmpty` | `IsEmpty(value)` | value | Tests Empty state. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsNumeric` | `IsNumeric(value)` | value | Tests numeric convertibility. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsObject` | `IsObject(value)` | value | Tests object/reference values. | [language-extensions.xps](../samples/language-extensions.xps) |
| `IsScalar` | `IsScalar(value)` | value | Tests scalar values. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsList` | `IsList(value)` | value | Tests List values. | [lists-classes.xps](../samples/lists-classes.xps) |
| `IsUnknown` | `IsUnknown(value)` | value | Tests unknown/empty Variant state. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Len` / `LenB` | `Len(text)` | text/value | Returns character/byte-oriented length. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Left` / `Right` | `Left(text, count)` | text; count | Returns characters from the left/right side. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Mid` | `Mid(text, start [, count])` | text/start/optional count | Returns a substring. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Instr` | `Instr([start,] text, search [, compare])` | optional start; source; search; optional comparison mode | Finds a substring. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Replace` | `Replace(text, find, replacement [, start [, count [, compare]]])` | source/find/replacement and optional controls | Replaces matching text. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `LCase` / `UCase` | `LCase(text)` | text | Converts case. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Trim` / `LTrim` / `RTrim` / `FullTrim` | `Trim(text)` | text | Removes whitespace according to the selected helper. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `StrReverse` | `StrReverse(text)` | text | Reverses a String. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Space` | `Space(count)` | number of spaces | Returns repeated spaces. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `String` | `String(count, character)` | repeat count; character/code | Returns a repeated character String. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Chr` / `UChr` | `Chr(code)` | character code | Converts a character code to String. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Asc` / `Uni` | `Asc(text)` | text | Returns the character code of the first character. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `StrLeft` / `StrLeftBack` | `StrLeft(text, delimiter)` | text/delimiter | Returns text left of first/last delimiter. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `StrRight` / `StrRightBack` | `StrRight(text, delimiter)` | text/delimiter | Returns text right of first/last delimiter. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `StrToken` | `StrToken(text, delimiter, index)` | text/delimiter/token index | Returns one delimited token. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `LSet` / `RSet` | `LSet(text, width)` | text/width | Fits/pads text using left/right alignment. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `StrConv` | `StrConv(text, conversion)` | text/conversion mode | Applies supported String conversion. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Val` | `Val(text)` | text | Parses leading numeric content. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Str` | `Str(number)` | number | Formats number with compatibility String semantics. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Bin` / `Hex` / `Oct` | `Hex(number)` | integer-compatible value | Converts to base 2/16/8 String. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Base64Encode` | `Base64Encode(text [, charset])` | text; optional charset | Encodes text as Base64. | [base64-binary.xps](../samples/base64-binary.xps) |
| `Base64Decode` | `Base64Decode(base64 [, charset])` | Base64 text; optional charset | Decodes Base64 to text. | [base64-binary.xps](../samples/base64-binary.xps) |
| `Base64DecodeBinary` | `Base64DecodeBinary(base64)` | Base64 text | Decodes to Byte array. | [base64-binary.xps](../samples/base64-binary.xps) |
| `UrlEncode` / `UrlDecode` | `UrlEncode(text)` | text | URL encodes/decodes text. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |

## Math

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Abs` | `Abs(number)` | number | Absolute value. | [compatibility.xps](../samples/compatibility.xps) |
| `Int` | `Int(number)` | number | Floors a number. | [compatibility.xps](../samples/compatibility.xps) |
| `Fix` | `Fix(number)` | number | Truncates toward zero. | [compatibility.xps](../samples/compatibility.xps) |
| `Round` | `Round(number [, digits])` | number; optional digits | Rounds using runtime midpoint semantics. | [compatibility.xps](../samples/compatibility.xps) |
| `Sqr` | `Sqr(number)` | number | Square root. | [compatibility.xps](../samples/compatibility.xps) |
| `Sgn` | `Sgn(number)` | number | Returns -1, 0 or 1. | [compatibility.xps](../samples/compatibility.xps) |
| `Sin` / `Cos` / `Tan` | `Sin(radians)` | angle in radians | Trigonometric function. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `ATn` | `ATn(number)` | number | Arctangent in radians. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `ATn2` | `ATn2(y, x)` | y/x coordinates | Angle from coordinates. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `ASin` / `ACos` | `ASin(number)` | number | Arc-sine/arc-cosine. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `Exp` | `Exp(number)` | exponent | Returns e raised to the exponent. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `Log` | `Log(number)` | positive number | Natural logarithm. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `Fraction` | `Fraction(number)` | number | Fractional part after truncation. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `Rnd` | `Rnd([number])` | optional compatibility argument | Returns a pseudo-random Double. | [compatibility.xps](../samples/compatibility.xps) |
| `Randomize` | `Randomize [seed]` | optional numeric seed | Resets/seeds the pseudo-random generator. | [compatibility.xps](../samples/compatibility.xps) |

## Date and time

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `DateNumber` | `DateNumber(year, month, day)` | year/month/day | Creates a Date from calendar fields. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `TimeNumber` | `TimeNumber(hour, minute, second)` | hour/minute/second | Creates a time value. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `DateValue` | `DateValue(value)` | Date/String-compatible value | Returns date portion. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `TimeValue` | `TimeValue(value)` | Date/String-compatible value | Returns time portion. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Year` / `Month` / `Day` | `Year(date)` | date | Returns date component. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `Hour` / `Minute` / `Second` | `Hour(date)` | date/time | Returns time component. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `DateAdd` | `DateAdd(interval, number, date)` | interval code/count/date | Adds an interval. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `DateDiff` | `DateDiff(interval, date1, date2 [, firstDayOfWeek [, firstWeekOfYear]])` | interval/dates/optional calendar controls | Difference in selected interval. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `DatePart` | `DatePart(interval, date [, firstDayOfWeek [, firstWeekOfYear]])` | interval/date/optional calendar controls | Extracts a date interval component. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Date.Adjust` | `date.Adjust(years, months, days, hours, minutes, seconds)` | signed component adjustments | Returns/updates adjusted Date according to Date object semantics. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `Date.Difference` | `date.Difference(otherDate [, interval])` | comparison date; optional interval | Calculates Date-object difference. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |

## File I/O and filesystem

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `FreeFile` | `FreeFile()` | none | Returns an available file number. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Open` | `Open path For mode [Access access] [Lock lockMode] As #fileNumber [Len = recordLength] [Charset charset]` | path/mode/file number plus optional access, lock, record length, charset | Opens a file using XPScript file semantics. | [file-io-portability.xps](../samples/file-io-portability.xps) |
| `Close` | `Close [#fileNumber [, ...]]` | optional file numbers | Closes one or more files; without numbers closes all open XPScript files. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Print #` | `Print #fileNumber, value` | file number/value | Writes formatted text. | [textio-console.xps](../samples/textio-console.xps) |
| `Line Input #` | `Line Input #fileNumber, variable` | file number/target variable | Reads one text line. | [textio-console.xps](../samples/textio-console.xps) |
| `Input$` | `Input$(count, #fileNumber)` | character/byte count and file number | Reads a fixed amount from a file; console form is documented separately. | [textio-console.xps](../samples/textio-console.xps) |
| `Put` | `Put #fileNumber, [recordOrPosition], value` | file number/optional position/value | Writes Binary/Random data. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Get` | `Get #fileNumber, [recordOrPosition], variable` | file number/optional position/target | Reads Binary/Random data. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Loc` | `Loc(fileNumber)` | file number | Returns current file location/record position. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Lock` | `Lock #fileNumber [, start [To end]]` | file number and optional byte/record range | Acquires an OS-backed range lock where supported. | [file-lock-holder.xps](../samples/file-lock-holder.xps) |
| `Unlock` | `Unlock #fileNumber [, start [To end]]` | file number and optional range | Releases a range lock. | [file-lock-holder.xps](../samples/file-lock-holder.xps) |
| `Kill` | `Kill path` | path | Deletes a file using target-OS semantics. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `FileCopy` | `FileCopy source, destination` | source/destination paths | Copies a file. | [file-io-portability.xps](../samples/file-io-portability.xps) |
| `Name` | `Name oldPath As newPath` | source/destination path | Renames/moves using the filesystem's rename operation. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `FileLen` | `FileLen(path)` | path | Returns file length. | [file-io-portability.xps](../samples/file-io-portability.xps) |
| `FileDateTime` | `FileDateTime(path)` | path | Returns file timestamp. | [file-io-portability.xps](../samples/file-io-portability.xps) |
| `GetFileAttr` | `GetFileAttr(path)` | path | Returns file attributes. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `SetFileAttr` | `SetFileAttr path, attributes` | path/attribute flags | Changes supported file attributes. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `MkDir` | `MkDir path` | directory path | Creates a directory. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `RmDir` | `RmDir path` | directory path | Removes an empty directory. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `ChDir` | `ChDir path` | directory path | Changes current directory. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `CurDir` | `CurDir([drive])` | optional drive where supported | Returns current directory. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `ChDrive` | `ChDrive drive` | Windows drive name/letter | Changes current Windows drive; unsupported on non-Windows targets. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `Dir` | `Dir([pathPattern [, attributes]])` | optional pattern/attributes | Starts/continues directory enumeration using target filesystem semantics. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |

## Console, environment and process

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Print` | `Print value` | value | Writes to standard output. | [textio-console.xps](../samples/textio-console.xps) |
| `Input` | `Input [prompt,] variable` | optional prompt/target variable | Reads console input using compatibility semantics. | [textio-console.xps](../samples/textio-console.xps) |
| `Pause` | `Pause [prompt]` | optional prompt | Waits for console interaction. | [textio-console.xps](../samples/textio-console.xps) |
| `Command` | `Command()` | none | Returns process command-line text/compatibility representation. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Environ` | `Environ(name)` | environment variable name | Returns an environment-variable value. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Sleep` | `Sleep milliseconds` | duration in milliseconds | Suspends the current execution thread. | [runtime-sax.xps](../samples/runtime-sax.xps) |
| `Platform` | `Platform()` or `Platform` | none | Returns `Windows`, `Linux`, `MacOS`, `FreeBSD` or `Unknown`. | [platform-shell.xps](../samples/platform-shell.xps) |
| `Shell` | `Shell(command [, windowStyle])` | command/program text; optional Windows window style | Executes a program/script through the cross-platform process runtime. Normal arguments are structured; explicit shell syntax should use `cmd.exe /c`, `sh -c` or `pwsh -Command`. | [platform-shell.xps](../samples/platform-shell.xps) |
| `MessageBox` | `MessageBox(prompt [, buttons [, title]])` | message; optional buttons/title | Displays a message box where the runtime/platform supports it. | [runtime-sax.xps](../samples/runtime-sax.xps) |
| `InputBox` | `InputBox(prompt [, title [, default]])` | prompt; optional title/default | Displays an input prompt where supported. | [runtime-sax.xps](../samples/runtime-sax.xps) |
| `Beep` | `Beep` | none | Requests an audible notification where supported. | [runtime-sax.xps](../samples/runtime-sax.xps) |
| `Stop` | `Stop` | none | Stops/interrupts execution according to runtime behavior. | [runtime-sax.xps](../samples/runtime-sax.xps) |
| `Format` / `Format$` | `Format(value [, format])` | value; optional format string | Formats a value. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `FormatNumber` | `FormatNumber(number [, digits])` | number; optional decimal digits | Formats numeric output. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `FormatPercent` | `FormatPercent(number [, digits])` | number; optional decimal digits | Formats percentage output. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |

## Evaluate

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Evaluate` | `Evaluate(sourceText [, callvar])` | XPScript source text; optional callvar object/collection | Executes the supported isolated Evaluate subset and returns the explicit `Return` value when present. | [evaluate-xpscript.xps](../samples/evaluate-xpscript.xps) |

See [evaluate.md](evaluate.md) for callvar snapshots, supported functions, limits and diagnostic sanitization.

## Native declarations and references

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Declare Function` | `Declare Function Name Lib "library" [Alias "entry"] (...) As Type` | library; optional entry alias; parameter signature/return type | Declares a native function. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `Declare Sub` | `Declare Sub Name Lib "library" [Alias "entry"] (...)` | library; optional entry alias; parameter signature | Declares a native procedure. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| Platform library selectors | `WindowsLib "..." LinuxLib "..." MacOSLib "..."` | platform-specific library names | Selects a library by target OS. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| Architecture library selectors | `WindowsX64Lib`, `WindowsArm64Lib`, `LinuxX64Lib`, `LinuxArm64Lib`, `MacOSX64Lib`, `MacOSArm64Lib` | target-specific library name/path | Selects native library by exact RID architecture. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |
| Platform aliases | `WindowsAlias`, `LinuxAlias`, `MacOSAlias` plus architecture-specific forms | exported entry-point name | Selects exported function name by target. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |
| `Reference` | `Reference "relative/path/Assembly.dll"` | project-local managed assembly path | Adds a managed build/deployment reference; it does not implicitly expose CLR type/member syntax. | [managed reference fixture](../tests/ManagedReferenceFixture/FixtureApi.cs) |
| `ReferenceNative` | `ReferenceNative "relative/path/library" Runtime "rid"` | project-local native file and one supported RID | Packages a managed assembly's RID-specific native dependency only for the selected target RID. | [managed reference deployment probe](../tests/ManagedReferenceDeploymentProbe/Program.cs) |

## Native HTTP and JSON

| Command/type | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `HttpClient` | `Dim client As New HttpClient` | none | Creates the native HTTP client object. | [native-http-json.xps](../samples/native-http-json.xps) |
| `HttpClient.Get` | `client.Get(url)` | absolute `http`/`https` URL | Sends GET and returns `HttpResponse`. | [native-http-json.xps](../samples/native-http-json.xps) |
| `HttpClient.Post` | `client.Post(url, body [, contentType])` | URL/body/optional content type | Sends POST. | [native-http-json.xps](../samples/native-http-json.xps) |
| `HttpClient.Put` | `client.Put(url, body [, contentType])` | URL/body/optional content type | Sends PUT. | [native-http-json.xps](../samples/native-http-json.xps) |
| `HttpClient.Patch` | `client.Patch(url, body [, contentType])` | URL/body/optional content type | Sends PATCH. | [native-http-json.xps](../samples/native-http-json.xps) |
| `HttpClient.Delete` | `client.Delete(url)` | URL | Sends DELETE. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonParse` | `JsonParse(text)` | JSON text | Parses JSON. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonStringify` | `JsonStringify(value)` | JSON-compatible value/object | Serializes JSON. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonEncode` / `JsonDecode` | `JsonEncode(value)` | value/text | Compatibility/native JSON conversion helpers. | [native-http-json.xps](../samples/native-http-json.xps) |

See [native-http-json.md](native-http-json.md) for object members, response/header handling and security validation.

## Types and classes

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Enum` | `Enum Name ... End Enum` | enum name/members | Declares an Enum. | [language-extensions.xps](../samples/language-extensions.xps) |
| `Type` | `Type Name ... End Type` | type name/fields | Declares a value-style Type. | [type-value-copy.xps](../samples/type-value-copy.xps) |
| `Class` | `Class Name ... End Class` | class name/members | Declares a class. | [lists-classes.xps](../samples/lists-classes.xps) |
| `New` | `New ClassName` | class type | Creates an object instance. | [lists-classes.xps](../samples/lists-classes.xps) |
| `Set` | `Set target = objectExpression` | object target/expression | Assigns an object reference. | [module-object-references.xps](../samples/module-object-references.xps) |
| `Delete` | `Delete objectVariable` | object variable | Releases/deletes object reference according to XPScript class semantics. | [lists-classes.xps](../samples/lists-classes.xps) |
| `Nothing` | `Nothing` | none | Null object-reference value. | [lists-classes.xps](../samples/lists-classes.xps) |
| `Me` | `Me` | none | Current class instance. | [lists-classes.xps](../samples/lists-classes.xps) |
| `Property Get` | `Property Get Name(...) As Type` | optional indexes/return type | Declares readable property member. | [indexed-properties.xps](../samples/indexed-properties.xps) |
| `Property Let` | `Property Let Name(..., value)` | optional indexes/value | Declares scalar/value property assignment. | [indexed-properties.xps](../samples/indexed-properties.xps) |
| `Property Set` | `Property Set Name(..., value)` | optional indexes/object value | Declares object-reference property assignment. | [indexed-properties.xps](../samples/indexed-properties.xps) |

## Compiler CLI

| Command/option | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| Compile | `xpscriptc <source.xps> [-o output] [--runtime rid] [--framework-dependent] [--result-format text|json|xml]` | source; optional output/RID/mode/result format | Compiles an `.xps` source file. | [direct-script-execution.md](direct-script-execution.md) |
| Run | `xpscriptc run <source.xps> [--runtime rid] [--result-format text|json|xml] [--] [script arguments...]` | source; optional RID/result format; script args after `--` | Compiles to an isolated temporary output and runs the matching-host artifact. | [direct-script-execution.md](direct-script-execution.md) |
| `--runtime` / `--rid` | `--runtime win-x64` | supported RID | Selects target runtime. | [cross-platform-runtime.md](cross-platform-runtime.md) |
| `--framework-dependent` | `--framework-dependent` | none | Produces framework-dependent output instead of self-contained single-file output. | [cross-platform-runtime.md](cross-platform-runtime.md) |
| `--result-format` | `--result-format text|json|xml` | output format | Selects compiler diagnostic/result serialization. | [direct-script-execution.md](direct-script-execution.md) |
| `--restricted` | `--restricted` | none | Enables restricted source-root policy. | [include-source-files.md](include-source-files.md) |
| `--source-root` | `--source-root DIR` | allowed source directory; repeatable | Adds an allowed source root for restricted include/dependency resolution. | [include-source-files.md](include-source-files.md) |

## Related topic documentation

- [Application](application.md)
- [Core language](core-language.md)
- [Arrays, Lists and operators](arrays-lists-operators.md)
- [Strings, conversion and Base64](strings-conversion-base64.md)
- [Math functions](math-functions.md)
- [Date and time](date-time.md)
- [File I/O and filesystem](file-io-filesystem.md)
- [Text I/O and console](text-io-console.md)
- [Console/process/formatting](console-process-formatting.md)
- [Evaluate](evaluate.md)
- [Cross-platform runtime](cross-platform-runtime.md)
- [Native declarations](platform-native.md)
- [Native HTTP/JSON](native-http-json.md)
- [Direct script execution](direct-script-execution.md)
