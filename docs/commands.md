# XPScript command reference

This is the compact reference for XPScript language commands, functions, runtime objects and compiler options. Every row includes syntax, parameters, a short description and an executable repository example.

## Language and procedures

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Option Declare` | `Option Declare` | none | Requires variables to be declared. | [core-language.xps](../samples/core-language.xps) |
| `Option Base` | `Option Base 0` or `Option Base 1` | base | Sets the default lower array bound. | [type-array-option-base.xps](../samples/type-array-option-base.xps) |
| `DefInt` | `DefInt A-Z` | letter range | Sets default Integer typing for matching names. | [core-language.xps](../samples/core-language.xps) |
| `Dim` | `Dim name As Type` | name, type | Declares a variable. | [core-language.xps](../samples/core-language.xps) |
| `Static` | `Static name As Type` | name, type | Declares procedure-local persistent state. | [language-extensions.xps](../samples/language-extensions.xps) |
| `Sub` | `Sub Name(...) ... End Sub` | name, parameters | Declares a procedure without a return value. | [functions.xps](../samples/functions.xps) |
| `Function` | `Function Name(...) As Type ... End Function` | name, parameters, return type | Declares a procedure with a return value. | [functions.xps](../samples/functions.xps) |
| `Call` | `Call Procedure(arguments)` | arguments | Calls a procedure and discards any return value. | [functions.xps](../samples/functions.xps) |
| `ByRef` | `ByRef name As Type` | parameter | Passes a parameter by reference. | [functions.xps](../samples/functions.xps) |
| `ByVal` | `ByVal name As Type` | parameter | Passes a parameter by value. | [functions.xps](../samples/functions.xps) |
| `Optional` | `Optional name As Type = value` | parameter, default | Declares an optional parameter. | [functions.xps](../samples/functions.xps) |
| `If` | `If condition Then ... End If` | condition | Conditional execution. | [core-language.xps](../samples/core-language.xps) |
| `ElseIf` | `ElseIf condition Then` | condition | Adds another conditional branch. | [core-language.xps](../samples/core-language.xps) |
| `Else` | `Else` | none | Adds a fallback branch. | [core-language.xps](../samples/core-language.xps) |
| `Select Case` | `Select Case value ... End Select` | expression | Multi-branch selection. | [core-language.xps](../samples/core-language.xps) |
| `For` | `For i = start To finish [Step step]` | start, finish, step | Numeric loop. | [core-language.xps](../samples/core-language.xps) |
| `ForAll` | `ForAll item In collection` | item, collection | Iterates a list or collection. | [lists-classes.xps](../samples/lists-classes.xps) |
| `GoTo` | `GoTo label` | label | Jumps to a label in the current procedure. | [core-language.xps](../samples/core-language.xps) |
| `GoSub` | `GoSub label` | label | Calls a label block. | [core-language.xps](../samples/core-language.xps) |
| `Return` | `Return [value]` | optional value | Returns from a procedure, GoSub or Evaluate block. | [evaluate-xpscript.xps](../samples/evaluate-xpscript.xps) |
| `On Error` | `On Error GoTo label` | label or Resume Next | Installs error handling. | [core-language.xps](../samples/core-language.xps) |
| `Resume` | `Resume [Next|label]` | optional target | Continues after a handled error. | [nested-resume-targets.xps](../samples/nested-resume-targets.xps) |
| `Error` | `Error number [, description]` | number, description | Raises an XPScript runtime error. | [core-language.xps](../samples/core-language.xps) |
| `With` | `With object ... End With` | object | Shortens repeated member access. | [language-extensions.xps](../samples/language-extensions.xps) |

## Arrays and lists

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `ReDim` | `ReDim array(bounds)` | array, bounds | Allocates or resizes a dynamic array. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `ReDim Preserve` | `ReDim Preserve array(bounds)` | array, bounds | Resizes while preserving supported values. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `Erase` | `Erase array` | array | Clears or deallocates array storage. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `LBound` | `LBound(array [, dimension])` | array, dimension | Returns the lower array bound. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `UBound` | `UBound(array [, dimension])` | array, dimension | Returns the upper array bound. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `Array` | `Array(value1, value2, ...)` | values | Creates a Variant array. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `Join` | `Join(array [, delimiter])` | array, delimiter | Joins array values into text. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `Explode` | `Explode(text [, delimiter])` | text, delimiter | Splits text into an array. | [operators-arrays.xps](../samples/operators-arrays.xps) |
| `ArrayAppend` | `ArrayAppend(array, value)` | array, value | Appends a value using array helper semantics. | [evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps) |
| `ArrayGetIndex` | `ArrayGetIndex(array, value)` | array, value | Finds the matching array index. | [evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps) |
| `ArrayUnique` | `ArrayUnique(array)` | array | Removes duplicate values. | [evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps) |
| `ArraySlice` | `ArraySlice(array, start [, count])` | array, start, count | Returns a slice. | [evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps) |
| `ArraySplice` | `ArraySplice(array, start, count [, replacement])` | array, start, count, replacement | Removes or replaces a range. | [evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps) |
| `IsElement` | `IsElement(list(tag))` | list element | Tests whether a list element exists. | [lists-classes.xps](../samples/lists-classes.xps) |
| `ListTag` | `ListTag(list)` | list | Returns list tags. | [lists-classes.xps](../samples/lists-classes.xps) |

## Conversion and inspection

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `CStr` | `CStr(value)` | value | Converts to String. | [coercion.xps](../samples/coercion.xps) |
| `CByte` | `CByte(value)` | value | Converts to Byte. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CInt` | `CInt(value)` | value | Converts to Integer. | [coercion.xps](../samples/coercion.xps) |
| `CLng` | `CLng(value)` | value | Converts to Long. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CSng` | `CSng(value)` | value | Converts to Single. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CDbl` | `CDbl(value)` | value | Converts to Double. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CCur` | `CCur(value)` | value | Converts to Currency. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CBool` | `CBool(value)` | value | Converts to Boolean. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CDate` | `CDate(value)` | value | Converts to Date. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `CVar` | `CVar(value)` | value | Returns a Variant value. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `CType` | `CType(value, typeName)` | value, type name | Converts using an XPScript type name. | [coercion.xps](../samples/coercion.xps) |
| `TypeName` | `TypeName(value)` | value | Returns the runtime type name. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `DataType` | `DataType(value)` | value | Returns the runtime datatype code. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsArray` | `IsArray(value)` | value | Tests for an array. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsDate` | `IsDate(value)` | value | Tests date compatibility. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsNull` | `IsNull(value)` | value | Tests Null state. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsEmpty` | `IsEmpty(value)` | value | Tests Empty state. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsNumeric` | `IsNumeric(value)` | value | Tests numeric compatibility. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `IsObject` | `IsObject(value)` | value | Tests object/reference state. | [language-extensions.xps](../samples/language-extensions.xps) |
| `IsList` | `IsList(value)` | value | Tests list state. | [lists-classes.xps](../samples/lists-classes.xps) |

## Strings

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Len` | `Len(text)` | text | Returns string length. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Left` | `Left(text, count)` | text, count | Returns characters from the left. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Right` | `Right(text, count)` | text, count | Returns characters from the right. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Mid` | `Mid(text, start [, count])` | text, start, count | Returns a substring. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Instr` | `Instr([start,] text, search [, compare])` | text, search, options | Finds a substring. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Replace` | `Replace(text, find, replacement)` | text, find, replacement | Replaces matching text. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `LCase` | `LCase(text)` | text | Converts to lower case. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `UCase` | `UCase(text)` | text | Converts to upper case. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Trim` | `Trim(text)` | text | Removes surrounding whitespace. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `StrReverse` | `StrReverse(text)` | text | Reverses a string. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Space` | `Space(count)` | count | Creates repeated spaces. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `String` | `String(count, character)` | count, character | Creates a repeated-character string. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Chr` | `Chr(code)` | code | Converts a character code to text. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Asc` | `Asc(text)` | text | Returns the first character code. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Val` | `Val(text)` | text | Parses leading numeric content. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Str` | `Str(number)` | number | Formats a number as text. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Base64Encode` | `Base64Encode(text [, charset])` | text, charset | Encodes text as Base64. | [base64-binary.xps](../samples/base64-binary.xps) |
| `Base64Decode` | `Base64Decode(text [, charset])` | Base64 text, charset | Decodes Base64 text. | [base64-binary.xps](../samples/base64-binary.xps) |
| `UrlEncode` | `UrlEncode(text)` | text | URL encodes text. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `UrlDecode` | `UrlDecode(text)` | text | URL decodes text. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |

## Math and date/time

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Abs` | `Abs(number)` | number | Returns absolute value. | [compatibility.xps](../samples/compatibility.xps) |
| `Int` | `Int(number)` | number | Floors a number. | [compatibility.xps](../samples/compatibility.xps) |
| `Fix` | `Fix(number)` | number | Truncates toward zero. | [compatibility.xps](../samples/compatibility.xps) |
| `Round` | `Round(number [, digits])` | number, digits | Rounds a number. | [compatibility.xps](../samples/compatibility.xps) |
| `Sqr` | `Sqr(number)` | number | Returns square root. | [compatibility.xps](../samples/compatibility.xps) |
| `Sgn` | `Sgn(number)` | number | Returns sign. | [compatibility.xps](../samples/compatibility.xps) |
| `Sin` | `Sin(radians)` | radians | Returns sine. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `Cos` | `Cos(radians)` | radians | Returns cosine. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `Tan` | `Tan(radians)` | radians | Returns tangent. | [evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps) |
| `Rnd` | `Rnd([number])` | optional number | Returns a pseudo-random value. | [compatibility.xps](../samples/compatibility.xps) |
| `Randomize` | `Randomize [seed]` | optional seed | Seeds the random generator. | [compatibility.xps](../samples/compatibility.xps) |
| `DateNumber` | `DateNumber(year, month, day)` | year, month, day | Creates a date. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `TimeNumber` | `TimeNumber(hour, minute, second)` | hour, minute, second | Creates a time value. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `DateAdd` | `DateAdd(interval, number, date)` | interval, number, date | Adds a date interval. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `DateDiff` | `DateDiff(interval, date1, date2)` | interval, dates | Returns a date difference. | [reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps) |
| `Year` | `Year(date)` | date | Returns year. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `Month` | `Month(date)` | date | Returns month. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |
| `Day` | `Day(date)` | date | Returns day. | [date-object-enhancements.xps](../samples/date-object-enhancements.xps) |

## File, console and process

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `FreeFile` | `FreeFile()` | none | Returns an available file number. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Open` | `Open path For mode As #number` | path, mode, file number | Opens a file. | [file-io-portability.xps](../samples/file-io-portability.xps) |
| `Close` | `Close [#number]` | optional file number | Closes files. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Print #` | `Print #number, value` | file number, value | Writes text to a file. | [textio-console.xps](../samples/textio-console.xps) |
| `Line Input #` | `Line Input #number, variable` | file number, variable | Reads one line. | [textio-console.xps](../samples/textio-console.xps) |
| `Put` | `Put #number, position, value` | file number, position, value | Writes binary/random data. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Get` | `Get #number, position, variable` | file number, position, variable | Reads binary/random data. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Kill` | `Kill path` | path | Deletes a file. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `FileCopy` | `FileCopy source, destination` | source, destination | Copies a file. | [file-io-portability.xps](../samples/file-io-portability.xps) |
| `MkDir` | `MkDir path` | path | Creates a directory. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `RmDir` | `RmDir path` | path | Removes an empty directory. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `Dir` | `Dir([pattern])` | optional pattern | Enumerates directory entries. | [filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps) |
| `Print` | `Print value` | value | Writes to standard output. | [textio-console.xps](../samples/textio-console.xps) |
| `Input` | `Input [prompt,] variable` | prompt, variable | Reads console input. | [textio-console.xps](../samples/textio-console.xps) |
| `Pause` | `Pause [prompt]` | optional prompt | Waits for console input. | [textio-console.xps](../samples/textio-console.xps) |
| `Environ` | `Environ(name)` | environment variable name | Reads an environment variable. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |
| `Sleep` | `Sleep milliseconds` | milliseconds | Suspends execution. | [runtime-sax.xps](../samples/runtime-sax.xps) |
| `Platform` | `Platform()` | none | Returns the current platform name. | [platform-shell.xps](../samples/platform-shell.xps) |
| `Shell` | `Shell(command [, windowStyle])` | command, optional style | Starts an external process. | [platform-shell.xps](../samples/platform-shell.xps) |
| `Format` | `Format(value [, format])` | value, format | Formats a value. | [file-io-extensions.xps](../samples/file-io-extensions.xps) |

## Evaluate, classes and native integration

| Command | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Evaluate` | `Evaluate(sourceText [, callvar])` | source text, optional callvar | Executes supported dynamic XPScript. | [evaluate-xpscript.xps](../samples/evaluate-xpscript.xps) |
| `Enum` | `Enum Name ... End Enum` | name, members | Declares an Enum. | [language-extensions.xps](../samples/language-extensions.xps) |
| `Type` | `Type Name ... End Type` | name, fields | Declares a value type. | [type-value-copy.xps](../samples/type-value-copy.xps) |
| `Class` | `Class Name ... End Class` | name, members | Declares a class. | [lists-classes.xps](../samples/lists-classes.xps) |
| `New` | `New ClassName(...)` | constructor arguments | Creates an object. | [lists-classes.xps](../samples/lists-classes.xps) |
| `Set` | `Set target = expression` | object target, expression | Assigns an object reference. | [module-object-references.xps](../samples/module-object-references.xps) |
| `Delete` | `Delete objectVariable` | object variable | Applies XPScript object deletion semantics. | [lists-classes.xps](../samples/lists-classes.xps) |
| `Nothing` | `Nothing` | none | Represents no object reference. | [lists-classes.xps](../samples/lists-classes.xps) |
| `Property Get` | `Property Get Name As Type` | property name, type | Declares a readable property. | [indexed-properties.xps](../samples/indexed-properties.xps) |
| `Property Let` | `Property Let Name As Type` | property name, type | Declares scalar property assignment. | [indexed-properties.xps](../samples/indexed-properties.xps) |
| `Property Set` | `Property Set Name(...)` | property name, object value | Declares object-reference assignment. | [indexed-properties.xps](../samples/indexed-properties.xps) |
| `Declare Function` | `Declare Function Name Lib "library" (...) As Type` | library, signature | Declares a native function. Native parameters must currently be explicitly `ByVal`. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `Declare Sub` | `Declare Sub Name Lib "library" (...)` | library, signature | Declares a native procedure. | [platform-native-library.xps](../samples/platform-native-library.xps) |
| `WindowsX64Lib` | `WindowsX64Lib "library"` | library | Selects a Windows x64 native library. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |
| `LinuxArm64Lib` | `LinuxArm64Lib "library"` | library | Selects a Linux ARM64 native library. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |
| `MacOSArm64Lib` | `MacOSArm64Lib "library"` | library | Selects a macOS ARM64 native library. | [native-architecture-assets.xps](../samples/native-architecture-assets.xps) |
| `XPHttpClient` | `Dim client As New XPHttpClient` | none | Creates the native HTTP client. | [native-http-json.xps](../samples/native-http-json.xps) |
| `XPHttpClient.Get` | `client.Get(url)` | URL | Sends HTTP GET. | [native-http-json.xps](../samples/native-http-json.xps) |
| `XPHttpClient.Post` | `client.Post(url, body [, contentType])` | URL, body, content type | Sends HTTP POST. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonParse` | `JsonParse(text)` | JSON text | Parses JSON. | [native-http-json.xps](../samples/native-http-json.xps) |
| `JsonStringify` | `JsonStringify(value)` | value | Serializes JSON. | [native-http-json.xps](../samples/native-http-json.xps) |
| `XPDBSQLite` | `Dim db As New XPDBSQLite(path [, readOnly])` | relative path, optional read-only flag | Opens a local SQLite database. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.Execute` | `db.Execute(sql [, parameters])` | SQL, optional `XPJsonObject` | Executes parameterized non-query SQL and returns affected rows. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.Query` | `db.Query(sql [, parameters])` | SQL, optional `XPJsonObject` | Returns rows in a `XPJsonDocument` array. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.Scalar` | `db.Scalar(sql [, parameters])` | SQL, optional `XPJsonObject` | Returns the first value or null. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.BeginTransaction` | `db.BeginTransaction()` | none | Starts one transaction on the connection. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.Commit` | `db.Commit()` | none | Commits the active transaction. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.Rollback` | `db.Rollback()` | none | Rolls back the active transaction. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDBSQLite.Close` | `db.Close()` | none | Rolls back an active transaction and closes the database. | [xpdb-sqlite.xps](../samples/xpdb-sqlite.xps) |
| `XPDbMsSql` | `Dim db As New XPDbMsSql(connectionString)` | SQL Server connection string | Opens SQL Server or SQL Server Express. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPDbMsSql.Execute` | `db.Execute(sql [, parameters])` | SQL, optional `XPJsonObject` | Executes parameterized non-query SQL and returns affected rows. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPDbMsSql.Query` | `db.Query(sql [, parameters])` | SQL, optional `XPJsonObject` | Returns rows in a `XPJsonDocument` array. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPDbMsSql.Scalar` | `db.Scalar(sql [, parameters])` | SQL, optional `XPJsonObject` | Returns the first value or null. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPDbMsSql.BeginTransaction` | `db.BeginTransaction()` | none | Starts one transaction on the connection. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPDbMsSql.Commit` | `db.Commit()` | none | Commits the active transaction. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPDbMsSql.Rollback` | `db.Rollback()` | none | Rolls back the active transaction. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPDbMsSql.Close` | `db.Close()` | none | Rolls back an active transaction and closes the connection. | [xpdb-mssql.xps](../samples/xpdb-mssql.xps) |
| `XPAi` | `Dim ai As New XPAi(endpoint [, apiKey])` | endpoint, optional API key | Creates an OpenAI-compatible AI client. | [xpai.xps](../samples/xpai.xps) |
| `XPAi preset` | `Dim ai As New XPAi(preset, apiKey [, providerConfiguration])` | provider, key, optional Azure resource | Configures OpenAI, Claude, OpenRouter or Azure OpenAI. | [xpai.xps](../samples/xpai.xps) |
| `XPAi.AddMessage` | `ai.AddMessage(role, content)` | role, content | Adds a system, user or assistant message. | [xpai.xps](../samples/xpai.xps) |
| `XPAi.Complete` | `ai.Complete([messages [, model]])` | optional messages and model | Sends a non-streaming AI request. | [xpai.xps](../samples/xpai.xps) |
| `XPAi.Stream` | `ai.Stream([messages,] callback [, model])` | callback, optional messages and model | Streams text chunks to a module callback. | [xpai.xps](../samples/xpai.xps) |
| `XPAi.SetOption` | `ai.SetOption(name, value)` | JSON property and value | Adds an optional provider-compatible request property. | [xpai.xps](../samples/xpai.xps) |
| `XPAi.SetHeader` | `ai.SetHeader(name, value)` | header name and value | Adds or replaces a provider header. | [xpai.xps](../samples/xpai.xps) |
| `XPAi.Cancel` | `ai.Cancel()` | none | Cancels the active request or stream. | [xpai.xps](../samples/xpai.xps) |

## Application runtime

| Command/property | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `Application.ArgCount` | `Application.ArgCount` | none | Number of command-line arguments. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.Args(index)` | `Application.Args(index)` | index | Returns one command-line argument. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.CommandLine` | `Application.CommandLine` | none | Returns the convenience command-line string. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.ExecutablePath` | `Application.ExecutablePath` | none | Full executable path. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.ExecutableDirectory` | `Application.ExecutableDirectory` | none | Executable directory. | [application-runtime.xps](../samples/application-runtime.xps) |
| `Application.TempPath` | `Application.TempPath` | none | OS temporary directory. | [application-runtime.xps](../samples/application-runtime.xps) |

## Compiler CLI

| Command/option | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| Compile | `xpscriptc source.xps -o output` | source, output | Compiles an XPScript source file. | [hello.xps](../samples/hello.xps) |
| Run | `xpscriptc run source.xps` | source, script arguments | Compiles and runs a source file. | [hello.xps](../samples/hello.xps) |
| `--runtime` | `--runtime win-x64` | RID | Selects target runtime. | [platform-shell.xps](../samples/platform-shell.xps) |
| `--framework-dependent` | `--framework-dependent` | none | Creates framework-dependent output. | [hello.xps](../samples/hello.xps) |
| `--result-format` | `--result-format text|json|xml` | format | Selects compiler result serialization. | [compiler-errors.xps](../samples/compiler-errors.xps) |

For hosting parameters and web runtime objects, see [Getting started](getting-started.md) and [Web](web.md). For class details, see [Classes and types](classes.md). For dynamic evaluation, see [Evaluate](evaluate.md).
