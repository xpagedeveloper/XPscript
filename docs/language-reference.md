# XPScript language and built-in command reference

This is the primary reference for XPScript language statements, operators, built-in functions, file/console/process commands, native declarations and compiler CLI options. Runtime objects such as HTTP, JSON, databases, AI, UI and web state are indexed in [Runtime API reference](api-reference.md).

Every row has a command title, syntax, parameter description, short behavior description and a complete `.xps` example that can be copied and compiled.

## Quick navigation

- [Declarations and procedures](#declarations-and-procedures)
- [Control flow and errors](#control-flow-and-errors)
- [Operators](#operators)
- [Arrays and lists](#arrays-and-lists)
- [Conversion and inspection](#conversion-and-inspection)
- [Strings and text](#strings-and-text)
- [Regular expressions and encoding](#regular-expressions-and-encoding)
- [Math and date/time](#math-and-datetime)
- [File and filesystem](#file-and-filesystem)
- [Console, environment and process](#console-environment-and-process)
- [Classes, types and properties](#classes-types-and-properties)
- [Evaluate](#evaluate)
- [Native and managed interop](#native-and-managed-interop)
- [Compiler CLI](#compiler-cli)

## Declarations and procedures

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Option Declare` | `Option Declare` | none | Requires variables to be declared before use. | [core-language.xps](../samples/core-language.xps) |
| `Option Base` | `Option Base 0` or `Option Base 1` | `0` or `1`: default lower array bound. | Sets the implicit lower bound for arrays. | [type-array-option-base.xps](../samples/type-array-option-base.xps) |
| `DefInt` | `DefInt A-Z` | letter range. | Sets default Integer typing for matching undeclared type suffix/name ranges. | [core-language.xps](../samples/core-language.xps) |
| `Dim` | `Dim name As Type` | variable `name` and `Type`. | Declares a variable. | [hello.xps](../demo/console/hello.xps) |
| `Static` | `Static name As Type` | local variable name/type. | Declares procedure-local persistent state. | [language-extensions.xps](../samples/language-extensions.xps) |
| `Public` | `Public name As Type` | module variable name/type. | Declares module-visible state. | [module-object-references.xps](../samples/module-object-references.xps) |
| `Private` | `Private name As Type` | module variable name/type. | Declares module-private state. | [xpai.xps](../samples/xpai.xps) |
| `Sub` | `Sub Name(parameters) ... End Sub` | procedure name and parameters. | Declares a procedure with no return value. | [functions.xps](../samples/functions.xps) |
| `Function` | `Function Name(parameters) As Type ... End Function` | name, parameters, return type. | Declares a value-returning procedure. | [functions.xps](../samples/functions.xps) |
| `Call` | `Call Procedure(arguments)` | procedure arguments. | Calls a procedure and discards a return value. | [functions.xps](../samples/functions.xps) |
| `Exit Sub` | `Exit Sub` | none | Returns immediately from the current Sub. | [statement-layout-audit.xps](../samples/statement-layout-audit.xps) |
| `Exit Function` | `Exit Function` | none | Returns immediately from the current Function using its current return value. | [statement-layout-audit.xps](../samples/statement-layout-audit.xps) |
| `ByRef` | `ByRef name As Type` | parameter name/type. | Passes the parameter by reference. | [functions.xps](../samples/functions.xps) |
| `ByVal` | `ByVal name As Type` | parameter name/type. | Passes the parameter by value/copy semantics. | [byval-copy-semantics.xps](../samples/byval-copy-semantics.xps) |
| `Optional` | `Optional name As Type = value` | parameter and default value. | Declares an optional procedure parameter. | [functions.xps](../samples/functions.xps) |
| line continuation `_` | `expression _` | trailing underscore on a continued physical line. | Continues supported expressions, argument lists and declarations. | [statement-layout-audit.xps](../samples/statement-layout-audit.xps) |

## Control flow and errors

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `If` | `If condition Then ... End If` | Boolean/coercible `condition`. | Executes a conditional branch. | [core-language.xps](../samples/core-language.xps) |
| `ElseIf` | `ElseIf condition Then` | branch condition. | Adds another conditional branch. | [core-language.xps](../samples/core-language.xps) |
| `Else` | `Else` | none | Adds fallback branch. | [core-language.xps](../samples/core-language.xps) |
| `Select Case` | `Select Case value ... End Select` | expression to match. | Multi-branch selection. | [core-language.xps](../samples/core-language.xps) |
| `Case` | `Case value` or `Case Else` | match expression/range or fallback. | Defines one Select Case branch. | [core-language.xps](../samples/core-language.xps) |
| `For` / `Next` | `For i = start To finish [Step step] ... Next` | loop variable, start, finish, optional step. | Numeric loop. | [hello.xps](../demo/console/hello.xps) |
| `ForAll` | `ForAll item In collection ... End ForAll` | item variable and collection/list. | Iterates a collection/list. | [lists-classes.xps](../samples/lists-classes.xps) |
| `Do` / `Loop` | `Do ... Loop` | none. | Repeats until exited/condition form ends. | [statement-layout-audit.xps](../samples/statement-layout-audit.xps) |
| `Do While` | `Do While condition ... Loop` | loop condition. | Repeats while condition is true. | [statement-layout-audit.xps](../samples/statement-layout-audit.xps) |
| `Do Until` | `Do Until condition ... Loop` | loop condition. | Repeats until condition becomes true. | [statement-layout-audit.xps](../samples/statement-layout-audit.xps) |
| `While` / `Wend` | `While condition ... Wend` | loop condition. | Legacy-style while loop. | [statement-layout-audit.xps](../samples/statement-layout-audit.xps) |
| `GoTo` | `GoTo label` | label in current procedure. | Jumps to a label. | [core-language.xps](../samples/core-language.xps) |
| `GoSub` | `GoSub label` | label in current procedure. | Calls a label block and returns with `Return`. | [core-language.xps](../samples/core-language.xps) |
| `Return` | `Return [value]` | optional return value/context. | Returns from GoSub or supported Evaluate/procedure contexts. | [evaluate-xpscript.xps](../samples/evaluate-xpscript.xps) |
| `On Error GoTo` | `On Error GoTo label` | error-handler label. | Installs procedure error handler. | [core-language.xps](../samples/core-language.xps) |
| `On Error Resume Next` | `On Error Resume Next` | none | Continues at the next statement after runtime errors. | [nested-resume-targets.xps](../samples/nested-resume-targets.xps) |
| `Resume` | `Resume [Next|label]` | optional resume target. | Resumes after a handled error. | [nested-resume-targets.xps](../samples/nested-resume-targets.xps) |
| `Error` | `Error number [, description]` | error number and optional description. | Raises an XPScript runtime error. | [core-language.xps](../samples/core-language.xps) |
| `Err` | `Err` | none | Returns current runtime error number. | [nested-resume-targets.xps](../samples/nested-resume-targets.xps) |
| `Error$` | `Error$` | none | Returns current runtime error description. | [nested-resume-targets.xps](../samples/nested-resume-targets.xps) |
| `Erl` | `Erl` | none | Returns physical source line for the active error context. | [erl-physical-source-line.xps](../samples/erl-physical-source-line.xps) |
| `With` | `With object ... End With` | object/value expression. | Shortens repeated member access. | [language-extensions.xps](../samples/language-extensions.xps) |

## Operators

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| arithmetic | `+ - * / \ Mod ^` | numeric operands; `+` also follows XPScript forgiving coercion rules. | Performs arithmetic. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| concatenation `&` | `left & right` | values converted to text. | Concatenates text. | [hello.xps](../demo/console/hello.xps) |
| comparisons | `= <> < <= > >=` | compatible values. | Compares values. | [date-comparisons-valid.xps](../samples/date-comparisons-valid.xps) |
| `Like` | `text Like pattern` | text and wildcard pattern. | Pattern comparison. | [compatibility.xps](../samples/compatibility.xps) |
| object identity `Is` | `left Is right` | object references. | Tests reference identity. | [module-object-references.xps](../samples/module-object-references.xps) |
| `And` | `a And b` | Boolean/coercible operands. | Logical AND. | [core-language.xps](../samples/core-language.xps) |
| `Or` | `a Or b` | Boolean/coercible operands. | Logical OR. | [core-language.xps](../samples/core-language.xps) |
| `Not` | `Not value` | Boolean/coercible operand. | Logical negation. | [core-language.xps](../samples/core-language.xps) |
| `Xor` | `a Xor b` | Boolean operands. | Exclusive OR. | [compatibility.xps](../samples/compatibility.xps) |
| `Eqv` | `a Eqv b` | Boolean operands. | Logical equivalence. | [compatibility.xps](../samples/compatibility.xps) |
| `Imp` | `a Imp b` | Boolean operands. | Logical implication. | [compatibility.xps](../samples/compatibility.xps) |

## Arrays and lists

Array-consuming operations normalize an object reference whose value is `Nothing` to a one-element String array containing `""`. `IsArray(Nothing)` remains `False` because the original value is still `Nothing`, not an allocated array.

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `ReDim` | `ReDim array(bounds)` | dynamic array and bounds. | Allocates/resizes array. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `ReDim Preserve` | `ReDim Preserve array(bounds)` | array and new bounds. | Resizes while preserving supported values. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `Erase` | `Erase array` | array. | Clears/deallocates array storage. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `LBound` | `LBound(array [, dimension])` | array and optional dimension. | Returns lower bound. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `UBound` | `UBound(array [, dimension])` | array and optional dimension. | Returns upper bound. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `Array` | `Array(value1, value2, ...)` | values. | Creates Variant array. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `Join` | `Join(array [, delimiter])` | array and optional delimiter. | Joins array values into text. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `Explode` | `Explode(text [, delimiter])` | text and optional delimiter. | Splits text into array. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `ArrayAppend` | `ArrayAppend(array, value)` | array and value. | Appends a value using array helper semantics. | [evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps) |
| `ArrayGetIndex` | `ArrayGetIndex(array, value)` | array and search value. | Finds matching index. | [evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps) |
| `ArrayUnique` | `ArrayUnique(array)` | array. | Returns values without duplicates. | [evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps) |
| `ArraySlice` | `ArraySlice(array, start [, count])` | array, start, optional count. | Returns slice. | [evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps) |
| `ArraySplice` | `ArraySplice(array, start, count [, replacement])` | array, range, optional replacement. | Removes/replaces range. | [evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps) |
| `IsElement` | `IsElement(list(tag))` | keyed list element. | Tests whether list element exists. | [lists-classes.xps](../samples/lists-classes.xps) |
| `ListTag` | `ListTag(list)` | list. | Returns list tags. | [lists-classes.xps](../samples/lists-classes.xps) |

## Conversion and inspection

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `CStr` | `CStr(value)` | value. | Converts to String. An object reference whose value is `Nothing` converts to `""`. | [general-dim-is-nothing.xps](../samples/general-dim-is-nothing.xps) |
| `CByte` | `CByte(value)` | value. | Converts to Byte. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CInt` | `CInt(value)` | value. | Converts to Integer. | [coercion.xps](../samples/coercion.xps) |
| `CLng` | `CLng(value)` | value. | Converts to Long. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CSng` | `CSng(value)` | value. | Converts to Single. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CDbl` | `CDbl(value)` | value. | Converts to Double. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CCur` | `CCur(value)` | value. | Converts to Currency. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CBool` | `CBool(value)` | value. | Converts to Boolean. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CDate` | `CDate(value)` | value. | Converts to Date. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `CVDate` | `CVDate(value)` | value. | Converts to Date using reference-runtime compatibility semantics. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CVar` | `CVar(value)` | value. | Returns Variant value. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CType` | `CType(value, typeName)` | value and XPScript target type name. | Converts using runtime type name. | [coercion.xps](../samples/coercion.xps) |
| `TypeName` | `TypeName(value)` | value. | Returns runtime type name. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `DataType` | `DataType(value)` | value. | Returns datatype code. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsArray` | `IsArray(value)` | value. | Tests array state. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsDate` | `IsDate(value)` | value. | Tests date compatibility. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsNull` | `IsNull(value)` | value. | Tests Null state. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsEmpty` | `IsEmpty(value)` | value. | Tests Empty state. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsNumeric` | `IsNumeric(value)` | value. | Tests numeric compatibility. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `IsObject` | `IsObject(value)` | value. | Tests object/reference state. | [language-extensions.xps](../samples/language-extensions.xps) |
| `IsScalar` | `IsScalar(value)` | value. | Tests scalar value state. | [compatibility.xps](../samples/compatibility.xps) |
| `IsList` | `IsList(value)` | value. | Tests keyed-list state. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsUnknown` | `IsUnknown(value)` | value. | Tests unknown/unset compatibility state. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |

## Strings and text

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Len` | `Len(text)` | text. | Returns character length. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `LenB` | `LenB(text)` | text. | Returns byte-oriented length using runtime byte encoding. | [compatibility.xps](../samples/compatibility.xps) |
| `Left` | `Left(text, count)` | text and character count. | Returns left substring. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `Right` | `Right(text, count)` | text and count. | Returns right substring. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `Mid` | `Mid(text, start [, count])` | text, one-based start, optional count. | Returns substring. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `Instr` | `Instr([start,] text, search [, compare])` | optional start, source text, search text, optional compare. | Finds substring position. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `InstrB` | `InstrB([start,] text, search)` | optional start, source, search. | Byte-oriented substring search. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `LeftB` | `LeftB(text, count)` | text and byte count. | Returns left byte-oriented substring. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `RightB` | `RightB(text, count)` | text and byte count. | Returns right byte-oriented substring. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `MidB` | `MidB(text, start [, count])` | text, one-based byte start, optional byte count. | Returns byte-oriented substring. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Replace` | `Replace(text, find, replacement)` | source text, find text, replacement. | Replaces matching text. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `LCase` | `LCase(text)` | text. | Converts to lower case. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `UCase` | `UCase(text)` | text. | Converts to upper case. | [hello.xps](../demo/console/hello.xps) |
| `LTrim` | `LTrim(text)` | text. | Removes leading whitespace. | [compatibility.xps](../samples/compatibility.xps) |
| `RTrim` | `RTrim(text)` | text. | Removes trailing whitespace. | [compatibility.xps](../samples/compatibility.xps) |
| `Trim` | `Trim(text)` | text. | Removes surrounding whitespace. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `StrReverse` | `StrReverse(text)` | text. | Reverses text. | [compatibility.xps](../samples/compatibility.xps) |
| `Space` | `Space(count)` | number of spaces. | Creates repeated spaces. | [compatibility.xps](../samples/compatibility.xps) |
| `String` | `String(count, character)` | repeat count and character/value. | Creates repeated-character string. | [compatibility.xps](../samples/compatibility.xps) |
| `Chr` | `Chr(code)` | character code. | Converts code to character. | [compatibility.xps](../samples/compatibility.xps) |
| `Asc` | `Asc(text)` | text. | Returns first character code. | [compatibility.xps](../samples/compatibility.xps) |
| `Val` | `Val(text)` | numeric text. | Parses leading numeric content. | [compatibility.xps](../samples/compatibility.xps) |
| `Str` | `Str(number)` | number. | Formats number as text. | [compatibility.xps](../samples/compatibility.xps) |
| `StrCompare` | `StrCompare(left, right [, compare])` | two strings and optional comparison mode. | Compares strings using XPScript compatibility semantics. | [compatibility.xps](../samples/compatibility.xps) |
| `StrConv` | `StrConv(text, conversion)` | text; `upper/lower/proper` or numeric 1/2/3. | Applies case conversion. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `StrLeft` | `StrLeft(text, delimiter)` | text and delimiter. | Text before first delimiter. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `StrLeftBack` | `StrLeftBack(text, delimiter)` | text and delimiter. | Text before last delimiter. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `StrRight` | `StrRight(text, delimiter)` | text and delimiter. | Text after first delimiter. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `StrRightBack` | `StrRightBack(text, delimiter)` | text and delimiter. | Text after last delimiter. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `StrToken` | `StrToken(text, delimiter, index)` | text, delimiter, one-based token index. | Returns selected token. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `LSet` | `LSet(value, width)` | value and field width. | Left-aligns/truncates/pads text. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `RSet` | `RSet(value, width)` | value and field width. | Right-aligns/truncates/pads text. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `UChr` | `UChr(codePoint)` | Unicode code point. | Returns Unicode rune as text. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Uni` | `Uni(text)` | text. | Returns first Unicode rune code point. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |

## Regular expressions and encoding

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `RegexValidate` | `RegexValidate(text, pattern)` | source text and regex pattern. | Returns whether regex matches; runtime enforces regex limits/timeouts. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `RegexMatch` | `RegexMatch(text, pattern)` | source text and regex pattern. | Returns matching strings as XPScript array. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Base64Encode` | `Base64Encode(text [, charset])` | text and optional charset. | Base64-encodes text. | [base64-binary.xps](../samples/base64-binary.xps) |
| `Base64Decode` | `Base64Decode(text [, charset])` | Base64 text and optional charset. | Decodes Base64 to text. | [base64-binary.xps](../samples/base64-binary.xps) |
| `Base64DecodeBinary` | `Base64DecodeBinary(text)` | Base64 text. | Returns decoded bytes as normal XPScript Byte array. | [base64-binary.xps](../samples/base64-binary.xps) |
| `ToBase64` | `ToBase64(text [, charset])` | text and optional charset. | Compatibility Base64 encoder. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `FromBase64` | `FromBase64(text [, charset])` | Base64 text and optional charset. | Compatibility Base64 decoder. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `UrlEncode` | `UrlEncode(text)` | text. | Percent-encodes URL component text. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `UrlDecode` | `UrlDecode(text)` | encoded text. | Decodes URL component text. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |

## Math and date/time

See [Date and time](date-time.md) for Date object extensions.

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Abs` | `Abs(number)` | number. | Absolute value. | [compatibility.xps](../samples/compatibility.xps) |
| `Int` | `Int(number)` | number. | Floors value. | [compatibility.xps](../samples/compatibility.xps) |
| `Fix` | `Fix(number)` | number. | Truncates toward zero. | [compatibility.xps](../samples/compatibility.xps) |
| `Round` | `Round(number [, digits])` | number and optional decimal digits. | Rounds value. | [compatibility.xps](../samples/compatibility.xps) |
| `Sqr` | `Sqr(number)` | non-negative number. | Square root. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `Sgn` | `Sgn(number)` | number. | Returns sign. | [compatibility.xps](../samples/compatibility.xps) |
| `Sin` | `Sin(radians)` | radians. | Sine. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `Cos` | `Cos(radians)` | radians. | Cosine. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `Tan` | `Tan(radians)` | radians. | Tangent. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `Rnd` | `Rnd([number])` | optional number. | Returns pseudo-random value. | [compatibility.xps](../samples/compatibility.xps) |
| `Randomize` | `Randomize [seed]` | optional seed. | Seeds random generator. | [compatibility.xps](../samples/compatibility.xps) |
| `Hex` | `Hex(number)` | integer value. | Formats value as hexadecimal text. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `Bin` | `Bin(number)` | integer value. | Formats value as binary text. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `Date` | `Date()` | none | Current local date. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `Now` | `Now()` | none | Current local date/time. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `DateNumber` | `DateNumber(year, month, day)` | year, month, day. | Creates Date. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `TimeNumber` | `TimeNumber(hour, minute, second)` | hour, minute, second. | Creates time value. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `DateAdd` | `DateAdd(interval, number, date)` | interval, signed amount, source Date. | Adds date interval. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `DateDiff` | `DateDiff(interval, date1, date2)` | interval and two dates. | Returns date difference. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Year` | `Year(date)` | Date. | Returns year. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `Month` | `Month(date)` | Date. | Returns month. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `Day` | `Day(date)` | Date. | Returns day. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `Date.Adjust` | `date.Adjust(years, months, days, hours, minutes, seconds)` | six signed integer components. | Returns adjusted Date. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `Date.Difference` | `date.Difference(otherDate)` | other Date. | Returns signed total seconds (`other-current`). | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `Date.OSDateFormatting` | `Date.OSDateFormatting` | none | Current OS/culture short-date format mask. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `Date.OSTimeFormatting` | `Date.OSTimeFormatting` | none | Current OS/culture long-time format mask. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |

## File and filesystem

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `FreeFile` | `FreeFile()` | none | Returns available file number. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Open` | `Open path For mode As #number` | file path, mode, file number. | Opens sequential/random/binary file according to mode. | [file-io-portability.xps](../samples/file-io-portability.xps) |
| `Close` | `Close [#number]` | optional file number. | Closes one/all open files. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Print #` | `Print #number, value` | file number and value. | Writes text to file. | [textio-console.xps](../samples/textio-console.xps) |
| `Line Input #` | `Line Input #number, variable` | file number and target variable. | Reads one text line. | [textio-console.xps](../samples/textio-console.xps) |
| `Input$` | `Input$(count, #number)` | character/byte count and file number. | Reads requested content from file, distinct from interactive Input. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Put` | `Put #number, position, value` | file number, position/record, value. | Writes binary/random data. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Get` | `Get #number, position, variable` | file number, position/record, target. | Reads binary/random data. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Lock` | `Lock #number [, start [, end]]` | file number and optional range. | Acquires OS-backed file/range lock according to file mode. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Unlock` | `Unlock #number [, start [, end]]` | file number and optional range. | Releases file/range lock. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Kill` | `Kill path` | file path. | Deletes file. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `FileCopy` | `FileCopy source, destination` | source and destination paths. | Copies file. | [file-io-portability.xps](../samples/file-io-portability.xps) |
| `MkDir` | `MkDir path` | directory path. | Creates directory. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `RmDir` | `RmDir path` | directory path. | Removes empty directory. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `Dir` | `Dir([pattern])` | optional path/pattern. | Enumerates directory entries. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `ChDrive` | `ChDrive drive` | Windows drive specifier. | Changes current drive on Windows; unsupported platforms report explicit behavior. | [file-io-portability.xps](../samples/file-io-portability.xps) |

## Console, environment and process

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Print` | `Print value` | value. | Writes standard output. An object reference whose value is `Nothing` is written as `Variable is Nothing`; `Null` is written as `Variable is null`. | [general-dim-is-nothing.xps](../samples/general-dim-is-nothing.xps), [null-empty-inspection-helpers.xps](../samples/null-empty-inspection-helpers.xps) |
| `Input` | `Input [prompt,] variable` | optional prompt and target variable. | Reads interactive console input. | [textio-console.xps](../samples/textio-console.xps) |
| `InputBox` | `InputBox(prompt [, title [, default]])` | prompt and optional UI text/default. | Reads interactive text input. | [hello.xps](../samples/hello.xps) |
| `Pause` | `Pause [prompt]` | optional prompt. | Waits for console input. | [textio-console.xps](../samples/textio-console.xps) |
| `Environ` | `Environ(name)` | environment variable name. | Reads environment variable. | [http-client.xps](../demo/http/http-client.xps) |
| `Sleep` | `Sleep milliseconds` | milliseconds. | Suspends execution. | [runtime-sax.xps](../samples/runtime-sax.xps) |
| `Platform` | `Platform()` | none | Returns stable platform name. | [platform-shell.xps](../samples/platform-shell.xps) |
| `Shell` | `Shell(command [, windowStyle])` | command and optional style. | Starts external process using platform-safe argument handling. | [platform-shell.xps](../samples/platform-shell.xps) |
| `Format` | `Format(value [, format])` | value and optional format mask. | Formats value. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |

## Classes, types and properties

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Enum` | `Enum Name ... End Enum` | enum name and members. | Declares enum. | [language-extensions.xps](../samples/language-extensions.xps) |
| `Type` | `Type Name ... End Type` | type name and fields. | Declares value-copy custom Type. | [type-value-copy.xps](../samples/type-value-copy.xps) |
| `Class` | `Class Name ... End Class` | class name and members. | Declares class/reference type. | [lists-classes.xps](../samples/lists-classes.xps) |
| `New` | `New ClassName(arguments)` | constructor arguments. | Creates object. | [lists-classes.xps](../samples/lists-classes.xps) |
| `Set` | `Set target = expression` | object target and expression. | Assigns object reference. | [module-object-references.xps](../samples/module-object-references.xps) |
| `Delete` | `Delete objectVariable` | object variable. | Applies XPScript object deletion/destructor semantics. | [lists-classes.xps](../samples/lists-classes.xps) |
| `Nothing` | `Nothing` | none | Represents no object reference. | [module-object-references.xps](../samples/module-object-references.xps) |
| `Me` | `Me` | none | Refers to current class instance. | [class-method-overloads.xps](../samples/class-method-overloads.xps) |
| `Property Get` | `Property Get Name(...) As Type` | property name, optional indexes, return type. | Declares readable property. | [indexed-properties.xps](../samples/indexed-properties.xps) |
| `Property Let` | `Property Let Name(...)` | property/index parameters and scalar value. | Declares scalar property assignment. | [indexed-properties.xps](../samples/indexed-properties.xps) |
| `Property Set` | `Property Set Name(...)` | property/index parameters and object value. | Declares object-reference property assignment. | [indexed-object-properties.xps](../samples/indexed-object-properties.xps) |

## Evaluate

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Evaluate` | `Evaluate(sourceText [, callvar])` | XPScript source text and optional restricted callvar snapshot. | Executes supported isolated dynamic XPScript and returns explicit `Return` value. | [evaluate-xpscript.xps](../samples/evaluate-xpscript.xps) |

See [Evaluate](evaluate.md) for isolation and supported-function details.

## Native and managed interop

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Declare Function` | `Declare Function Name Lib "library" (...) As Type` | native library, entry signature; native parameters explicitly `ByVal`. | Declares native function. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `Declare Sub` | `Declare Sub Name Lib "library" (...)` | native library and signature. | Declares native procedure. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `WindowsLib` | `WindowsLib "library"` | Windows native library. | Selects Windows native library for declaration. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `LinuxLib` | `LinuxLib "library"` | Linux native library. | Selects Linux native library. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `MacOSLib` | `MacOSLib "library"` | macOS native library. | Selects macOS native library. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| architecture library selectors | `WindowsX64Lib`, `WindowsArm64Lib`, `LinuxX64Lib`, `LinuxArm64Lib`, `MacOSX64Lib`, `MacOSArm64Lib` | architecture-specific library path/name. | Overrides native asset for exact OS/architecture target. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |
| `WindowsAlias` | `WindowsAlias "name"` | Windows entry-point name. | Selects Windows native alias. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `LinuxAlias` | `LinuxAlias "name"` | Linux entry-point name. | Selects Linux native alias. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `MacOSAlias` | `MacOSAlias "name"` | macOS entry-point name. | Selects macOS native alias. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `Reference` | `Reference "path.dll"` | application-local managed assembly path. | Stages/references managed .NET assembly. | [managed-reference.xps](../samples/managed-reference.xps) |
| `ReferenceNative` | `ReferenceNative "path" Runtime "rid"` | native dependency path and target RID. | Packages target-specific native dependency for managed reference/runtime. | [managed-reference-native.xps](../samples/managed-reference-native.xps) |

## Compiler CLI

See [Getting started](getting-started.md) for hosting commands and deployment packaging.

| Command/option | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| compile source | `xpscriptc source.xps -o output` | source file and output path. | Compiles XPScript. | [hello.xps](../demo/console/hello.xps) |
| run source | `xpscriptc run source.xps [-- scriptArgs...]` | source plus optional script arguments. | Compiles in isolated temp output and runs immediately. | [hello.xps](../demo/console/hello.xps) |
| `-o` | `-o path` | output path. | Selects compiler output. | [hello.xps](../demo/console/hello.xps) |
| `--runtime` | `--runtime RID` | runtime identifier such as `win-x64`, `linux-x64`, `osx-arm64`. | Selects target runtime. | [platform-shell.xps](../samples/platform-shell.xps) |
| `--framework-dependent` | `--framework-dependent` | none | Produces framework-dependent output. | [hello.xps](../demo/console/hello.xps) |
| `--result-format` | `--result-format text|json|xml` | result serialization format. | Selects compiler diagnostics/result format. | [compiler-errors.xps](../samples/compiler-errors.xps) |
| `--` | `-- scriptArg1 ...` | remaining values passed to script. | Ends compiler option parsing. | [application-runtime.xps](../samples/application-runtime.xps) |
| Kestrel `web` | `xpscript web --root PATH [options]` | web root plus host options. | Starts Kestrel web host. | [index.xps](../demo/kestrel/index.xps) |
| FastCGI | `xpscript fastcgi --root PATH --listen ADDRESS:PORT` | web root and private FastCGI endpoint. | Starts persistent FastCGI host. | [index.xps](../demo/fastcgi/index.xps) |
| WebIIS target | `xpscript compile source.xps --target webiis` | source route and `webiis` target. | Creates direct IIS deployment package/target output. | [main.xps](../demo/webiis/main.xps) |
