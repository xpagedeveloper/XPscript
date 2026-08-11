# XPScript runtime reference implementation TODO

(c) xpagedeveloper.com 2026

Tracks implementation against the standalone XPScript runtime reference.

Development note: while GitHub workflows are disabled by user request, implementation work is committed to `runtime-development-no-ci`. No workflow is started by this work.

Status:
- `[x]` implemented and previously verified
- `[-]` partially implemented
- `[>]` implemented/in progress, awaiting explicit verification
- `[ ]` not implemented

## 1. Core language and declarations

- [x] `Sub`, `Function`, `Call`, `Exit Sub`, `Exit Function`
- [x] scalar types: Variant, Boolean, Byte, Integer, Long, Single, Double, Currency, String, Date, Object
- [x] `Dim`, `Static`, `ByVal`, explicit `ByRef`, `Set`, `New`, `Delete`
- [x] `Optional` parameters, defaults, omitted trailing arguments and omitted slots
- [>] module-level `Public` scalar variables
- [>] module-level `Private` scalar variables
- [>] module-level fixed/dynamic arrays, including shared state across procedures, `ReDim`, `ReDim Preserve`, indexed reads/writes, `LBound`, `UBound`, and `Erase`; source: `samples/module-arrays.xps`
- [ ] module-level custom `Type` values
- [ ] module-level class/object references
- [-] `Type ... End Type`: scalar fields + auto initialization implemented; value-copy and array fields remain
- [x] `Enum ... End Enum`: explicit values, auto increment, qualified/unqualified members

## 2. Classes and properties

- [x] classes, methods, constructors, destructors, `Me`
- [x] parameterless `Property Get`
- [x] parameterless object `Property Set`
- [x] scalar `Property Let`
- [>] parameterized/indexed `Property Get`
- [>] parameterized/indexed `Property Let/Set`
- [>] indexed property calls are lowered to normal typed methods so existing parameter type diagnostics apply
- [>] positive source: `samples/indexed-properties.xps`
- [>] negative type source: `samples/indexed-properties-error.xps`

## 3. Control flow and error handling

- [x] `If`, `ElseIf`, `Else`
- [x] `Select Case`
- [x] `For/Next/Step`
- [x] `Do/Loop`, `Do While`, `Do Until`, `While/Wend`
- [x] `ForAll`
- [x] `GoTo`, `GoSub`, labels, `Return`
- [x] `On Error`, `Resume`, `Resume Next`, `Err`, `Error`, `Error$`, `Erl`
- [-] physical source-line accuracy for `Erl`
- [-] deeply nested `Resume` targets

## 4. Operators

- [x] comparisons `=`, `<>`, `<`, `>`, `<=`, `>=`
- [x] `Like`: `*`, `?`, `#`, sets, negated sets, ranges
- [x] object identity `Is`
- [x] `And`, `Or`, `Not`, `Xor`, `Eqv`, `Imp`
- [x] `+`, `-`, `*`, `/`, `\`, `Mod`, `^`
- [x] `&` and forgiving `+`
- [x] line continuation `_`

## 5. String functions

- [x] `Asc`, `Chr`, `Instr`, `LCase`, `UCase`, `Left`, `Right`, `Mid`, `Len`, `LenB`
- [x] `LTrim`, `RTrim`, `Trim`, `Replace`, `Space`, `String`, `Str`, `StrCompare`
- [x] `InstrB`, `LeftB`, `RightB`, `MidB`
- [x] `StrConv`: upper/lower/proper case
- [x] `StrLeft`, `StrLeftBack`, `StrRight`, `StrRightBack`, `StrToken`
- [x] `LSet`, `RSet`, `UChr`, `Uni`

## 6. Conversion and inspection

- [x] `CBool`, `CByte`, `CCur`, `CDate`, `CDat`, `CDbl`, `CInt`, `CLng`, `CSng`, `CStr`, `Val`
- [x] `CType`
- [x] `CVDate`
- [x] `DataType`, `TypeName`, `IsArray`, `IsDate`, `IsNull`, `IsNumeric`, `IsObject`, `IsScalar`
- [x] `IsList`, `IsUnknown`

## 7. Math and date/time

- [x] reference math functions
- [x] reference date/time functions

## 8. Arrays and lists

- [x] typed dynamic arrays
- [x] fixed/multidimensional arrays and explicit bounds
- [x] `Array`, `ReDim`, `ReDim Preserve`, `Erase`, `LBound`, `UBound`
- [x] `Join`, `Explode`, `ArrayGetIndex`, `ArrayAppend`, `ArrayUnique`, `ArraySplice`, `ArraySlice`
- [x] keyed lists, iteration, tag lookup, erase
- [ ] arrays as `Type` members

## 9. File I/O and filesystem

- [x] `FreeFile`, `Open`, `Close`, `Input #`, `Line Input`, `Print #`, `Write #`, `Get`, `Put`, `EOF`, `LOF`, `Loc`, `Seek`, `Reset`
- [x] Charset-aware Input/Output/Append
- [x] independent `Encoding "base64"` layer
- [>] file `Input$(count, #fileNumber)` implemented separately from interactive input
- [>] OS `Lock` with Binary byte ranges, Random record ranges, sequential whole-file semantics
- [>] matching OS `Unlock`
- [x] `ChDir`, `CurDir`, `Dir`, `FileCopy`, `FileDateTime`, `FileLen`, `Kill`, `MkDir`, rename/move, `RmDir`
- [>] `ChDrive`
- [>] explicit Latin-1 regression source

File input and interactive input are distinct APIs. `Lock/Unlock` must be verified from a second operating-system file handle when tests are re-enabled.

## 10. Formatting, process and console

- [x] `Format`, `Format$`, `FormatNumber`, `FormatPercent`
- [x] `Environ`, `Shell`, `Sleep`
- [x] console `Print`, `Print$`, interactive `Input`, interactive `Input$`, `Pause`
- [x] `InputBox`, `MessageBox`, `MsgBox`, `Beep`

## 11. Base64 and URL

- [x] `ToBase64`, `FromBase64`, `UrlEncode`, `UrlDecode`
- [x] `Base64Encode`, `Base64Decode`
- [ ] binary-return Base64 decode

## 12. Native HTTP API

Implemented but intentionally unverified while workflows are disabled:

- [>] `HttpClient`
- [>] `Get`, `Post`, `Put`, `Patch`, `Delete`
- [>] `SetHeader`, `RemoveHeader`, `ClearHeaders`, `Timeout`
- [>] `HttpResponse.StatusCode`, `StatusText`, `Body`, `ContentType`, `Headers`, `IsSuccess`
- [>] source: `samples/native-http-json.xps`

## 13. Native JSON API

Implemented but intentionally unverified while workflows are disabled:

- [>] `JsonDocument.Parse`, `JsonDocument.Stringify`
- [>] `JsonObject.Get`, `Set`, `Remove`, `Contains`, `Count`
- [>] `JsonArray.Add`, `Get`, `Set`, `RemoveAt`, `Count`
- [>] `JsonElement.Type`, `JsonElement.Value`
- [>] `JsonParse`, `JsonStringify`, `JsonEncode`, `JsonDecode`

## 14. Cross-platform compiler and runtime

- [ ] support publishing generated executables for Windows, Linux and macOS
- [ ] add compiler target/runtime selection for at least `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`
- [ ] decide/default target behavior when no target platform is supplied
- [ ] add `Platform` command/function that returns the current runtime platform name
- [ ] define stable `Platform` return values suitable for branching in XPScript code, e.g. `Windows`, `Linux`, `MacOS`
- [ ] document conditional platform-specific code patterns using `Platform`
- [ ] make `Shell` platform-aware
- [ ] Windows `Shell`: execute `.exe`, `.cmd`, `.bat`, `.ps1` and normal commands using appropriate Windows process handling
- [ ] Linux `Shell`: execute binaries, shell scripts and commands using executable bit/shebang or an appropriate shell when needed
- [ ] macOS `Shell`: execute binaries, shell scripts and commands using executable bit/shebang or an appropriate shell when needed
- [ ] preserve argument quoting and avoid accidental shell re-parsing unless explicitly required
- [ ] return clear runtime errors when a program/script cannot be executed on the current platform

## 15. Evaluate

- [ ] remove all references to `@Formula` from source code, documentation, samples and public terminology
- [ ] redefine `Evaluate(sourceText)` to execute only XPScript code supplied as a string
- [ ] `Evaluate` must not expose or emulate any external formula language
- [ ] define whether `Evaluate` accepts an expression, statements, or a complete XPScript snippet; preferred target is XPScript expressions/statements only
- [ ] isolate evaluated code from compiler/runtime internals unless explicitly exposed
- [ ] return XPScript values using normal XPScript type/coercion rules
- [ ] propagate syntax/type/runtime errors using normal XPScript diagnostics/error handling
- [ ] document examples of safe `Evaluate` use

## 16. Security review and isolation

- [ ] perform a dedicated security review of compiler, preprocessors, generated runtime and temp-build handling
- [ ] verify that values/variables from one scope cannot overwrite unrelated variables through generated-name collisions
- [ ] verify generated internal identifiers cannot collide with user-defined identifiers
- [ ] reserve or safely namespace all compiler-generated identifiers
- [ ] verify procedure locals, module globals, class fields, `Static` variables, arrays, lists and ByRef cells are isolated correctly
- [ ] verify one compiled source/module cannot overwrite another module's state unexpectedly
- [ ] verify concurrent compiler invocations use separate temporary directories/files and cannot overwrite each other
- [ ] verify generated output paths cannot overwrite unrelated files through path traversal or malformed source/output names
- [ ] review temporary file permissions and cleanup
- [ ] review `Shell`, file I/O, HTTP, dynamic `Evaluate`, P/Invoke and COM-related functionality for command/path/code injection risks
- [ ] review JSON/HTTP header/body handling for injection and unsafe implicit conversions
- [ ] review file `Lock/Unlock` behavior for race conditions and incorrect cross-process assumptions
- [ ] add negative security regression sources/tests once workflow execution is re-enabled
- [ ] document security boundaries and intentionally unsafe/powerful language features

## 17. Documentation and examples

- [ ] create complete English documentation for every supported XPScript statement, function, class, property and operator
- [ ] store all end-user documentation under `docs/`
- [ ] create/use an `examples/` directory for reusable `.xps` example programs
- [ ] migrate reusable examples from `samples/` to `examples/` where appropriate; keep test-only fixtures separate
- [ ] every documented function/statement/class should include at least one practical example or link to an example under `examples/`
- [ ] create `docs/index.md` as the documentation entry point
- [ ] create a language-reference index grouped by declarations, control flow, operators, strings, math, date/time, arrays/lists, file I/O, HTTP, JSON, process/platform and diagnostics
- [ ] create per-function/per-feature documentation pages or logically grouped pages with syntax, parameters, return value, errors and examples
- [ ] document type coercion rules and forgiving conversion behavior
- [ ] document compiler CLI including output format, target platform/runtime and exit codes
- [ ] document file `Input$` separately from interactive console input
- [ ] document OS file locking semantics for `Lock/Unlock`
- [ ] document `Platform`, cross-platform `Shell`, and target-platform publishing
- [ ] document `Evaluate` as XPScript-only dynamic code execution
- [ ] ensure public documentation contains XPScript branding only and no legacy product names or `@Formula` references

## 18. Quality gates

A feature is promoted from `[>]` to `[x]` only when requested verification is enabled and it passes:

1. XPScript parsing/transpilation.
2. Generated .NET 10 build.
3. `.xps` positive runtime regression.
4. Negative/type diagnostic regression where applicable.
5. Existing language/runtime regressions.
6. XPScript-only public branding.
7. OS cross-handle verification for `Lock/Unlock`.
8. Cross-platform features must be validated on their target operating system when workflow/test execution is re-enabled.
9. Security-sensitive features require negative/adversarial regression coverage before being marked `[x]`.
10. Documentation work is complete only when the documented item links to a valid `examples/` source or contains an equivalent inline example.
