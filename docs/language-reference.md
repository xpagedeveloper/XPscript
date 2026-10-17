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
- [Native and managed interop](#native-and-managed-interop)
- [Compiler CLI](#compiler-cli)

## Identifier scopes and reserved names

XPScript resolves identifiers by syntactic scope. Language keywords such as `If`, `Class`, `Function`, and `End` are syntax and cannot be reused where the grammar requires an identifier. Names beginning with `__` are reserved for compiler-generated state. Runtime type names are reserved only for declarations that would collide with generated/runtime types, and runtime value identifiers such as `Application` and `Body` are reserved where they denote the built-in runtime value.

Runtime function names are not globally reserved identifiers. An unqualified call such as `JsonParse(text)`, `SHA256(text)`, or `StrLeftBack(text, 2)` can resolve to the corresponding runtime function, while the same spelling remains legal for a class member when the grammar permits it. Receiver syntax always belongs to the receiver's scope, so `item.JsonParse(...)` is a member call and must not be rewritten as the global `JsonParse(...)` function.

This distinction also applies to properties, methods, parameters, locals, enum members, and symbols in different classes: sharing a spelling with a runtime API does not by itself reserve that spelling. Only actual syntax keywords and compiler/runtime names with a real declaration-scope collision are restricted.

## Declarations and procedures

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Option Declare` | `Option Declare` | none | Requires variables to be declared before use. | [core-language.xps](../samples/core-language.xps) |
| `Option Base` | `Option Base 0` or `Option Base 1` | `0` or `1`: default lower array bound. | Sets the implicit lower bound for arrays. | [type-array-option-base.xps](../samples/type-array-option-base.xps) |
| `DefInt` | `DefInt A-Z` | letter range. | Sets default Integer typing for matching undeclared type suffix/name ranges. | [core-language.xps](../samples/core-language.xps) |
| `Dim` | `Dim name As Type` | variable `name` and `Type`. | Declares a variable. | [hello.xps](../demo/console/hello.xps) |
| `Static` | `Static name As Type` | local variable name/type. | Declares procedure-local persistent state. Experimental AST currently supports scalar declarations without initializers; see [AST emission](ast-csharp-emitter.md). | [language-extensions.xps](../samples/language-extensions.xps) |
| `Public` | `Public name As Type` | module variable name/type. | Declares module-visible state. | [module-object-references.xps](../samples/module-object-references.xps) |
| `Private` | `Private name As Type` | module variable name/type. | Declares module-private state. | [xpai.xps](../samples/xpai.xps) |
| `Sub` | `Sub Name(parameters) ... End Sub` | procedure name and parameters. | Declares a procedure with no return value. | [functions.xps](../samples/functions.xps) |
| `Function` | `Function Name(parameters) As Type ... End Function` | name, parameters, return type. | Declares a value-returning procedure. | [functions.xps](../samples/functions.xps) |
| `Call` | `Call Procedure(arguments)` | procedure arguments. | Calls a procedure and discards a return value. | [functions.xps](../samples/functions.xps) |
| `Exit Sub` | `Exit Sub` | none | Returns immediately from the current Sub. | [statement-layout-audit.xps](../samples/statement-layout-audit.xps) |
| `Exit Function` | `Exit Function` | none | Returns immediately from the current Function using its current return value. Assigning to the function name stores that value and continues execution; reaching `End Function` returns it too. | [statement-layout-audit.xps](../samples/statement-layout-audit.xps) |
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
| `GoSub` | `GoSub label` | label in current procedure. | Legacy compiler: calls a label block and returns with `Return`. Experimental AST rejects GoSub; use a Sub or Function call instead. See [AST emission](ast-csharp-emitter.md). | [core-language.xps](../samples/core-language.xps) |
| `Return` | `Return` | none | Returns from a GoSub label block to the calling statement. | [core-language.xps](../samples/core-language.xps) |
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
| `Erase` | `Erase array`, `Erase list`, `Erase list(tag)` | array, List, or keyed List element. | Clears/deallocates array storage, clears a List, or removes one tag. Experimental AST supports fixed one-dimensional declared arrays and declared Lists; dynamic/multidimensional array lifecycle remains migration work. | [operators-arrays.xps](../samples/operators-arrays.xps), [lists-classes.xps](../samples/lists-classes.xps) |
| `LBound` | `LBound(array [, dimension])` | array and optional dimension. | Returns lower bound. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `UBound` | `UBound(array [, dimension])` | array and optional dimension. | Returns upper bound. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `Array` | `Array(value1, value2, ...)` | values. | Creates Variant array. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `Join` | `Join(array [, delimiter])` | array and optional delimiter. | Joins array values into text. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `Implode` | `Implode(array [, delimiter])` | array and optional delimiter. | Alias for Join. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `Split` | `Split(text [, delimiter [, count [, compMethod]]])` | text, optional delimiter, count, and comparison mode. | Splits text into an array. | [compatibility.xps](../samples/compatibility.xps) |
| `Explode` | `Explode(text [, delimiter])` | text and optional delimiter. | Splits text into array. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `ArrayAppend` | `ArrayAppend(array, value)` | array and value. | Appends a value using array helper semantics. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `ArrayGetIndex` | `ArrayGetIndex(array, value)` | array and search value. | Finds matching index. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `ArrayUnique` | `ArrayUnique(array)` | array. | Returns values without duplicates. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `ArraySlice` | `ArraySlice(array, start [, count])` | array, start, optional count. | Returns slice. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `ArraySplice` | `ArraySplice(array, start, count [, replacement])` | array, range, optional replacement. | Removes/replaces range. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `IsElement` | `IsElement(list(tag))` | keyed list element. | Tests whether a List tag exists without reading its value. Experimental AST supports declared Lists; see [AST emission](ast-csharp-emitter.md). | [lists-classes.xps](../samples/lists-classes.xps) |
| `ListTag` | `ListTag(alias)` | ForAll List alias. | Returns the current element's tag. | [lists-classes.xps](../samples/lists-classes.xps) |

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
| `IsNumeric` | `IsNumeric(value)` | value. | Tests numeric compatibility. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsObject` | `IsObject(value)` | value. | Tests object/reference state. | [language-extensions.xps](../samples/language-extensions.xps) |
| `IsScalar` | `IsScalar(value)` | value. | Tests scalar value state. | [compatibility.xps](../samples/compatibility.xps) |
| `IsList` | `IsList(value)` | value. | Tests keyed-list state. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsUnknown` | `IsUnknown(value)` | value. | Tests unknown/unset compatibility state. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |

## Strings and text

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Len` | `Len(text)` | text. | Returns character length. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `LenB` | `LenB(text)` | text. | Returns byte-oriented length using runtime byte encoding. | [compatibility.xps](../samples/compatibility.xps) |
| `Left` | `Left(text, count)` | text and character count. | Returns left substring. | [compatibility.xps](../samples/compatibility.xps) |
| `Right` | `Right(text, count)` | text and count. | Returns right substring. | [compatibility.xps](../samples/compatibility.xps) |
| `Mid` | `Mid(text, start [, count])` | text, one-based start, optional count. | Returns substring. | [compatibility.xps](../samples/compatibility.xps) |
| `Instr` | `Instr([start,] text, search [, compare])` | optional start, source text, search text, optional compare. | Finds substring position. | [compatibility.xps](../samples/compatibility.xps) |
| `InstrB` | `InstrB([start,] text, search)` | optional start, source, search. | Byte-oriented substring search. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `LeftB` | `LeftB(text, count)` | text and byte count. | Returns left byte-oriented substring. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `RightB` | `RightB(text, count)` | text and byte count. | Returns right byte-oriented substring. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `MidB` | `MidB(text, start [, count])` | text, one-based byte start, optional byte count. | Returns byte-oriented substring. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `MidBP` | `MidBP(text, start [, count])` | text, one-based byte start, optional byte count. | Returns byte-position substring. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Replace` | `Replace(text, find, replacement)` | source text, find text, replacement. | Replaces matching text. | [compatibility.xps](../samples/compatibility.xps) |
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
| `Sqr` | `Sqr(number)` | non-negative number. | Square root. | [compatibility.xps](../samples/compatibility.xps) |
| `Sgn` | `Sgn(number)` | number. | Returns sign. | [compatibility.xps](../samples/compatibility.xps) |
| `Sin` | `Sin(radians)` | radians. | Sine. | [compatibility.xps](../samples/compatibility.xps) |
| `Cos` | `Cos(radians)` | radians. | Cosine. | [compatibility.xps](../samples/compatibility.xps) |
| `Tan` | `Tan(radians)` | radians. | Tangent. | [compatibility.xps](../samples/compatibility.xps) |
| `Rnd` | `Rnd([number])` | optional number. | Returns pseudo-random value. | [compatibility.xps](../samples/compatibility.xps) |
| `Randomize` | `Randomize [seed]` | optional seed. | Seeds random generator. | [compatibility.xps](../samples/compatibility.xps) |
| `Hex` | `Hex(number)` | integer value. | Formats value as hexadecimal text. | [compatibility.xps](../samples/compatibility.xps) |
| `Bin` | `Bin(number)` | integer value. | Formats value as binary text. | [compatibility.xps](../samples/compatibility.xps) |
| `Date` | `Date()` | none | Current local date. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `Now` | `Now()` | none | Current local date/time. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `DateNumber` | `DateNumber(year, month, day)` | year, month, day. | Creates Date. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `TimeNumber` | `TimeNumber(hour, minute, second)` | hour, minute, second. | Creates time value. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `DateAdd` | `DateAdd(interval, number, date)` | interval, signed amount, source Date. | Adds date interval. | [compatibility.xps](../samples/compatibility.xps) |
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
| `ShellId` | `ShellId(program [, windowStyle])` | program and optional style. | Starts a program and returns its task identifier. | [platform-shell.xps](../samples/platform-shell.xps) |
| `ShellArgs` | `ShellArgs(executable, arguments [, windowStyle])` | executable, one-dimensional argument array/list, optional style. | Starts an external process without waiting. Structured arguments are passed without command-string re-parsing where possible. | [shell-structured-arguments.xps](../samples/shell-structured-arguments.xps) |
| `ShellExecute` | `ShellExecute(executable, arguments [, timeoutMilliseconds])` | executable, one-dimensional argument array/list, optional non-negative timeout in milliseconds; `0` means wait indefinitely. | Waits for completion and returns `XPShellResult`. `ExitCode` is the child exit code, `Output`/`Error` capture stdout/stderr, and `TimedOut` reports timeout. A timeout kills the process tree and returns `ExitCode=-1`; process-start failures remain runtime errors. `ShellExecute` has no external cancellation parameter; use `timeoutMilliseconds` when bounded execution is required. | [shellexecute-basic.xps](../samples/shellexecute-basic.xps) |
| `Format` | `Format(value [, format])` | value and optional format mask. | Formats value. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |

## Classes, types and properties

Experimental AST constructor parameters may be declared `ByVal values List As Long` or `ByRef values List As Box`, including trailing Optional Lists. ByVal shares the List but replacing its parameter does not replace the caller's variable. ByRef aliases the actual exact-type slot, including duplicate aliases and changes made before an error. List element types are invariant. Omitted List references start as Nothing; omitted ByRef slots are fresh and factories/dependent defaults use the existing conversion rules. Lists borrow their elements, including images; only owned typed image fields are cleaned on failure/Delete. See [typed List constructors](ast-csharp-emitter.md#typed-list-constructor-parameters) and `class-constructor-list.xps`/`image-constructor-list.xps`.

Experimental AST recognizes ranked array parameter declarations such as `values()` and `values(,)`, but array constructor/procedure/method execution remains unsupported (XPS3001). Ranks above eight and combined array/List parameters are syntax errors. This parser prerequisite does not change existing array storage or enable whole-array ByRef replacement. See [array parameter migration](ast-csharp-emitter.md#array-parameter-syntax-migration-prerequisite).

Experimental AST constructors support Variant and shared JSON/XML/CSV/spreadsheet/HTTP parameters, including trailing Optional ByVal/ByRef parameters. ByVal preserves a reference snapshot; ByRef requires exact-type writable storage and preserves caller writes on errors. Omitted native references are Nothing, omitted Variant values Empty; omitted ByRef storage is fresh. Native references and Variant-held images remain borrowed, with explicit HTTP Dispose; only the instance's owned typed image fields are cleaned on failure/Delete. Declared XPImage and array parameter signatures remain deferred. See [constructor reference contracts](ast-csharp-emitter.md#variant-and-shared-native-constructor-references) and the constructor regression fixtures.

Experimental AST also supports `Optional ByRef value As Box = New Box()` and `Optional value As Object`: omitted references get fresh exact-type storage, with Nothing as the implicit default. Supplied references may be typed locals/parameters/fields, initializer-free typed Object/user-class module variables, matching array/List elements or List ForAll aliases. Arrays retain declared element types and bounds. Replacement/clearing and duplicate aliases remain live through New/Parent.New and errors; ByVal stays a snapshot. Do not pass an accessor, incompatible element, or initialized compatibility module variable by reference. Input/default user objects retain their own image ownership. See [reference storage](ast-csharp-emitter.md#optional-byref-references-and-typed-reference-storage).

Experimental AST supports trailing `Optional ByVal value As Object` and concrete user-class constructor parameters, for example `Sub New(Optional ByVal value As Box = New Box())`. Omitting the initializer means Nothing; New defaults create a fresh object per call. Defaults run once in order before bodies and may read earlier parameters/module routines, not instance state or later parameters. New/Parent.New and implicit all-Optional bases use the same rules. Reference mutations are shared; replacing the ByVal parameter stays local. Default-created user objects are borrowed, not recursively owned: retain them for explicit cleanup of their image fields. Optional ByRef Object/user-class parameters are also supported as described above. See [Optional reference constructors](ast-csharp-emitter.md#optional-byval-object-and-user-class-constructor-parameters).

Experimental AST accepts required `ByRef value As Object` or a concrete user-class constructor parameter, including unmodified ByRef. Pass an exactly typed local, parameter or real user-class field: `New Record(value)` and `Call Parent.New(value)` can replace/clear the caller's reference. Duplicate arguments share one live slot, including updates from defaults and failures. Subclass-to-base and class-to-Object ByRef conversions are not allowed; use an exactly declared variable or ByVal instead. Required references may precede scalar Optional parameters, but initialized compatibility module storage and XPImage/array signatures remain deferred. Replacing an image-owning user object does not Delete it or its fields; keep an alias for explicit cleanup. See [ByRef reference constructors](ast-csharp-emitter.md#required-byref-object-and-user-class-constructor-parameters), `class-constructor-reference-byref.xps` and its image regression. Normal ByRef constructor behavior still differs; existing ByVal parity is tested separately.

Experimental AST accepts required `ByVal` constructor parameters declared `As Object` or as a concrete user class, for example `Sub New(ByVal value As Box)` with `New Record(box)` / `Call Parent.New(box)`. Mutations share the referenced object; `Set value = Nothing` changes only the parameter snapshot. Existing Nothing, user-class upcasts and Variant conversions apply; unrelated classes/downcasts and statically scalar Object arguments are rejected. Required references may precede Optional scalar parameters, whose defaults can read earlier reference members. Declared XPImage/array signatures remain deferred. Argument objects are borrowed, even when they contain XPImage fields: constructor failure must not Delete or clear their fields. See [ByVal reference constructors](ast-csharp-emitter.md#required-byval-object-and-user-class-constructor-parameters), `class-constructor-reference.xps` and `image-constructor-reference.xps`.

Experimental AST supports scalar `Optional ByRef` constructor parameters, including implicit ByRef (`Optional value As Long = 1`). `New Record()` gets fresh omitted storage on every call; `New Record(value)` shares the caller's exact-type writable location. Later defaults may read/change earlier omitted or supplied references. Mixed ByRef/ByVal signatures, Parent.New and implicit all-Optional bases use the same rules. Defaults run once before bodies and failures preserve old assignment targets. See [Optional ByRef constructor details](ast-csharp-emitter.md#optional-byref-constructor-parameters) and `class-constructor-optional-byref.xps`; XPImage/array signatures remain deferred.

For mixed signatures, write `Sub New(ByRef seed As Long, Optional ByVal extra As Long = NextValue(seed))` and call `New Record(seed)` or `Call Parent.New(seed)`. Defaults run once and can mutate earlier required references without breaking aliases. Supplied ByVal values remain snapshots. See [mixed AST constructor details](ast-csharp-emitter.md#mixed-required-byref-and-optional-byval-constructors) and `class-constructor-mixed.xps` / `image-constructor-mixed.xps`. Optional ByRef, including an Optional declaration without explicit ByVal, is also supported with fresh omitted storage.

Experimental AST supports required scalar `ByRef` constructor parameters followed by trailing `Optional ByVal` parameters, for example `Sub New(ByRef seed As Long)` and `Dim item As New Record(seed)`. Unmodified required parameters are also ByRef. Pass exact-type writable locals/parameters, user-class fields, initializer-free typed module scalars or typed array/List elements/ForAll aliases. New and Parent.New preserve shared aliases and updates through errors; they do not use a separate copy-back step. Failed construction keeps an old assignment target and cleans owned image fields without user Delete. ByVal parameters remain snapshots. XPImage/array parameter signatures and accessor-backed references remain unsupported. Normal compilation currently fails the simple ByRef constructor at runtime; this is AST capability coverage. See [ByRef constructor details](ast-csharp-emitter.md#required-scalar-byref-constructor-parameters), `class-constructor-byref.xps` and `image-constructor-byref.xps`.

Experimental AST also accepts trailing scalar `Optional ByVal` constructor parameters, for example `Sub New(ByVal seed As Long, Optional ByVal label As String = "default")`. Omit a trailing suffix in `New Record(4)` or `Call Parent.New(4)`. Defaults run once in order before bodies, after supplied arguments; they may use earlier parameters/module routines, not the instance or later parameters. With no explicit default, scalars use zero/False/empty String. Errors before body entry propagate to the caller, preserving an old assignment target. XPImage/array parameters and explicit empty slots remain unsupported. Normal compilation still requires full constructor arity; see [AST Optional constructor details](ast-csharp-emitter.md#optional-byval-constructor-parameters) and `class-constructor-optional.xps`.

Experimental AST constructor subset: a root class may declare `Sub New(ByVal value As Long)` (or a parameterless New), with required or trailing Optional scalar ByRef/ByVal arguments. Use `Dim item As New ClassName(...)` or `Set item = New ClassName(...)`; a plain typed Dim starts as Nothing. Bodies support Me, private helpers, Exit Sub and On Error. Failed construction cleans typed image fields without invoking user Delete and preserves an existing assignment target. XPImage/array parameters remain unsupported (XPS3001). See [the AST constructor contract](ast-csharp-emitter.md#root-class-constructors) and executable `tests/ast-compile-probe/class-constructor.xps`. Derived constructors use a first-statement `Call Parent.New(...)` or an accessible parameterless base; arguments see parameters/module routines but not the instance. This restriction concerns experimental AST, not the normal compiler's full class syntax.

For derived AST classes, `Delete item` invokes destructor bodies from most-derived to root. After each body its own typed image fields are cleared, then the base chain runs automatically in finally, including after Exit Sub or errors. Do not add explicit Parent.Delete forwarding. A body's guard is per class and instance, preventing re-entry/repeated aliases from rerunning it. Derived bodies can use own private fields and inherited public fields/properties/helpers, including Me/With and matching scalar ByRef storage; base-private members remain inaccessible. Ordinary derived method/accessor bodies are still deferred. If several destructors throw, the later ancestor error wins by standard finally precedence, while remaining ancestors and caller-reference cleanup still run. See `class-destructor-derived.xps`, `class-destructor-derived-parity.xps` and `image-destructor-derived.xps`; advanced constructor signatures and GC/scope-exit cleanup remain separate work.

Experimental AST supports one parameterless `Sub Delete()` per class. Use `Delete objectVariable` to invoke it, including through a derived instance; do not call a GC finalizer or depend on leaving scope. The body runs at most once per instance, even through surviving aliases or after a failed call. It may use private helpers, Me, Exit Sub and ordinary On Error handling. The named caller variable is cleared even if the body throws, while the error propagates normally and typed image fields are recycled. Other AST aliases still refer to the object; they do not become Nothing automatically. Ordinary methods and accessors authored in derived classes remain unsupported with XPS3001; parameters, duplicate destructors and Function/Property Delete forms are invalid. Advanced constructor signatures and full lifecycle migration remain separate work. See `class-destructor.xps`, `class-destructor-parity.xps` and `image-destructor.xps`.

Experimental AST `Delete owner` clears and recycles typed XPImage fields through the virtual deletion chain, including private and inherited storage. It clears the named caller variable; other object aliases retain the object with empty image fields. Original source images and borrowed Variant fields remain untouched. Repeated deletion is safe. This is explicit Delete cleanup, not GC finalization, scope-exit cleanup or constructor forwarding. See `image-field-delete.xps` and [image ownership](xpimage/index.md).

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

Experimental `ast-compile` supports trailing Optional scalar indexes in Get/Let, for example `Property Get Item(Optional ByVal index As Long = 4) As Long` and `Property Let Item(Optional ByVal index As Long = 4, ByVal incoming As Long)`. Call `box.Item()` or assign `box.Item() = 10` to omit every Optional index. The setter value is always required. Paired accessors must agree on index types, modes and Optional positions. Each accessor evaluates its own defaults once, in order, after the supplied arguments (and setter RHS); defaults can reference instance members and earlier indexes. Omitted ByRef defaults have fresh storage; supplied ByRef arguments retain aliases. Explicit empty slots remain unsupported in AST. See [AST emitter contract](ast-csharp-emitter.md) and [executable Optional-index regression](../tests/ast-compile-probe/indexed-optional-runtime.xps).

Experimental AST also supports indexed and non-indexed Object/user-class Get/Set. Use `Set store.Item(index) = objectValue` for a Property Set and ordinary assignment for Property Let; the binder diagnoses the opposite mode and scalar/incompatible class values. A typed getter preserves the declared class, so `store.Item(index).Value` is supported. Reference getter results start as Nothing and share object identity with stored references. The setter receives the assigned reference value; changing its incoming parameter does not reassign the source variable. Scalar ByRef indexes preserve aliases, including with Optional indexes and handled errors. See [indexed object sample](../samples/indexed-object-properties.xps) and [executable reference regression](../tests/ast-compile-probe/indexed-object-runtime.xps). Typed JSON/XML/CSV/spreadsheet/HTTP reference accessors are supported; other native families and derived accessor bodies remain migration work.

Experimental AST accepts a reference to a derived user class wherever a compatible base reference is required by a supported value assignment, initializer, property or ByVal call. For example, with `Class Leaf Extend Root`, `Set store.Current = leaf` works when Current is declared As Root, and preserves the same object. Transitive ancestry is supported. Passing a value to a property setter is a reference snapshot even if the accessor's incoming parameter is ByRef; this does not allow a Leaf variable to alias an ordinary ByRef Root parameter. ByRef storage, arrays and Lists remain invariant. Implicit downcasts and sibling conversions are rejected; Variant casts retain their existing runtime checks. Unknown base classes and inheritance cycles are diagnosed before inherited members are traversed. Ordinary method/accessor bodies authored in derived classes and complete constructor/lifecycle dispatch remain migration work. See [reference inheritance regression](../tests/ast-compile-probe/inherited-reference-runtime.xps).

## Native and managed interop

XPImage Set uses snapshot-owned typed class fields, unlike shared JSON/XML/CSV/spreadsheet/HTTP references. Field assignment clones before recycling the previous value; Nothing clears the field and a failed clone preserves it. ByVal setter values clone at entry; ByRef values are borrowed reference snapshots whose reassignment cannot replace or recycle the source variable. Mutations and explicit Recycle through borrowed references remain shared. Release typed image fields explicitly or Delete their owner; no GC/scope-exit cleanup is supplied. Variant fields do not acquire image ownership. See [the storage contract](xpimage/index.md) and `image-property-set.xps`.

Experimental AST also supports XPImage Property Get factories, including scalar Optional/ByRef indexes. Capture the result in a typed XPImage local so normal caller-scope cleanup owns it. Returned images survive the getter; other locals, replaced results and results in a throwing getter are recycled. A missing result is Nothing. This is the image-function ownership contract, not shared-reference Get/Set: no automatic getter clone is introduced. Clone explicitly when exposing an image that must remain owned by another scope, and do not discard an owning result in a member chain. Image Set uses the snapshot-storage contract above. See [XPImage accessor regression](../tests/ast-compile-probe/image-property-get.xps) and [image lifetime guidance](xpimage/index.md).

Experimental AST Get/Set properties may declare any native HTTP client/request/response/part/file/collection reference type, including scalar Optional/ByRef indexes. Getter member chains stay typed; setters snapshot the incoming reference and preserve shared mutation. Unassigned results are Nothing. Client disposal remains explicit and affects all aliases; properties do not clone or automatically own clients. All HTTP security and read-only-member checks still apply. Object-valued indexes and automatic class destruction remain unsupported. See [HTTP accessor runtime regression](../tests/ast-compile-probe/http-reference-properties.xps) and [HTTP reference contract](http-client.md).

Experimental AST user properties also accept XPCsvDocument and CSV header/row/column collection/item references, plus XPSpreadsheet, XPWorksheet, XPCell and XPRange. Get results and Set incoming values retain their native types, identity and shared mutations. Scalar Optional/ByRef indexes and Nothing follow the same accessor rules. FromBytes is restricted to a writable document variable; read a property into a local and explicitly Set the replacement back. See [tabular accessor runtime regression](../tests/ast-compile-probe/tabular-reference-properties.xps) and [complete tabular signature coverage](../tests/ast-compile-probe/tabular-reference-signatures.xps).

Experimental AST Get/Set properties can also declare XPJsonDocument, XPJsonObject, XPJsonArray, XPJsonElement or XPXml reference types, including node/attribute collections and validation results/errors. Use Set for assignment. Values share native references; XML element-to-node value conversion is supported. Scalar Optional/ByRef indexes follow the same rules as user-reference properties. Unassigned getters return Nothing. Other native Get/Set families remain unsupported; XPImage follows its separate owning-result and snapshot-storage contract. See [native accessor runtime regression](../tests/ast-compile-probe/native-reference-properties.xps) and [signature coverage](../tests/ast-compile-probe/native-reference-signatures.xps).

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Declare Function` | `Declare Function Name Lib "library" (...) As Type` | native library, entry signature; native parameters explicitly `ByVal`. | Declares native function. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `Declare Sub` | `Declare Sub Name Lib "library" (...)` | native library and signature. | Declares native procedure. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `WindowsLib` | `WindowsLib "library"` | Windows native library. | Selects Windows native library for declaration. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `LinuxLib` | `LinuxLib "library"` | Linux native library. | Selects Linux native library for declaration. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `MacOSLib` | `MacOSLib "library"` | macOS native library. | Selects macOS native library for declaration. | [platform-native-library.xps](../samples/platform-native-library.xps) |
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
| run source | `xpscript run source.xps [--Args "arg1 arg2 ..."]` | source plus optional script arguments. | Compiles in isolated temp output and runs immediately. | [hello.xps](../demo/console/hello.xps) |
| `-o` | `-o path` | output path. | Selects compiler output. | [hello.xps](../demo/console/hello.xps) |
| `--platform` / `--rid` | `--platform RID` or `--rid RID` | runtime identifier such as `win-x64`, `linux-x64`, `osx-arm64`. | Selects target runtime. | [platform-shell.xps](../samples/platform-shell.xps) |
| `--runtime=false` | `--runtime=false` | none | Produces framework-dependent output. | [hello.xps](../demo/console/hello.xps) |
| `--result-format` | `--result-format text|json|xml` | result serialization format. | Selects compiler diagnostics/result format. | [compiler-errors.xps](../samples/compiler-errors.xps) |
| `--` | `-- scriptArg1 ...` | remaining values passed to script. | Ends compiler option parsing. | [application-runtime.xps](../samples/application-runtime.xps) |
| XPScript WebServer `web` | `xpscript web --root PATH [options]` | web root plus host options. | Starts XPScript WebServer web host. | [index.xps](../demo/kestrel/index.xps) |
| FastCGI | `xpscript fastcgi --root PATH --listen ADDRESS:PORT` | web root and private FastCGI endpoint. | Starts persistent FastCGI host. | [index.xps](../demo/fastcgi/index.xps) |
| WebIIS target | `xpscript compile source.xps --target webiis` | source route and `webiis` target. | Creates direct IIS deployment package/target output. | [main.xps](../demo/webiis/main.xps) |

### Byte arrays

XPScript supports an explicit `Byte[]` variable for binary data returned by APIs such as image, file, HTTP, archive, and encoding operations.

```xpscript
Dim bytes As Byte[]
bytes = img.ToBytes("png")
Print Len(bytes)
```

`Byte[]` is a binary byte array. `Len(bytes)` returns the number of bytes in the array. `LenB(bytes)` also returns the number of bytes. A newly declared `Byte[]` is an empty array, so its length is zero. Use `Variant` when code intentionally needs a dynamically typed value instead of an explicitly typed byte array.

