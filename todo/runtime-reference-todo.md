# XPScript runtime reference implementation TODO

(c) xpagedeveloper.com 2026

This file tracks implementation against the standalone runtime reference supplied for XPScript.

Development note: while GitHub workflows are disabled by user request, new implementation work is committed to branch `runtime-development-no-ci`. Items implemented there remain `[>]` until explicit test execution is re-enabled.

Status legend:

- `[x]` implemented and covered by CI or an existing verified regression sample
- `[-]` partially implemented; more semantics/tests are required
- `[>]` implemented/in progress but awaiting explicit verification
- `[ ]` not implemented yet

## 1. Core language and declarations

- [x] `Sub`, `Function`, `Call`, `Exit Sub`, `Exit Function`
- [x] scalar declarations: Variant, Boolean, Byte, Integer, Long, Single, Double, Currency, String, Date, Object
- [x] `Dim`, `Static`, `ByVal`, explicit `ByRef`, `Set`, `New`, `Delete`
- [x] `Optional` parameters including omitted trailing arguments, omitted slots and default values
- [>] module-level `Public` scalar variables implemented as generated static Script fields; custom/object/array module globals remain to verify/extend
- [>] module-level `Private` scalar variables implemented as generated static Script fields; custom/object/array module globals remain to verify/extend
- [-] user-defined `Type ... End Type`: scalar fields and automatic initialization are implemented; true value-copy semantics and array members remain
- [x] `Enum ... End Enum`, explicit values, auto-increment and qualified/unqualified members

## 2. Classes and properties

- [x] classes, methods, constructors, destructors, `Me`
- [x] parameterless `Property Get`
- [x] parameterless object `Property Set`
- [x] scalar `Property Let`
- [ ] parameterized/indexed properties

## 3. Control flow and error handling

- [x] `If`, `ElseIf`, `Else`
- [x] `Select Case`
- [x] `For/Next/Step`
- [x] `Do/Loop`, `Do While`, `Do Until`, `While/Wend`
- [x] `ForAll`
- [x] `GoTo`, `GoSub`, labels and `Return`
- [x] `On Error`, `Resume`, `Resume Next`, `Err`, `Error`, `Error$`, `Erl`
- [-] physical source-line accuracy for `Erl`
- [-] `Resume` into deeply nested generated scopes

## 4. Operators

- [x] comparisons `=`, `<>`, `<`, `>`, `<=`, `>=`
- [x] `Like` with `*`, `?`, `#`, sets, negated sets and ranges
- [x] object identity `Is`
- [x] `And`, `Or`, `Not`, `Xor`, `Eqv`, `Imp`
- [x] `+`, `-`, `*`, `/`, `\`, `Mod`, `^`
- [x] string concatenation with `&` and forgiving `+`
- [x] line continuation `_`

## 5. String functions

- [x] `Asc`, `Chr`, `Instr`, `LCase`, `UCase`, `Left`, `Right`, `Mid`, `Len`, `LenB`
- [x] `LTrim`, `RTrim`, `Trim`, `Replace`, `Space`, `String`, `Str`, `StrCompare`
- [x] `InstrB`, `LeftB`, `RightB`, `MidB`
- [x] `StrConv` supported conversions: upper, lower and proper case
- [x] `StrLeft`, `StrLeftBack`, `StrRight`, `StrRightBack`, `StrToken`
- [x] `LSet`, `RSet`
- [x] `UChr`, `Uni`

## 6. Conversion and inspection

- [x] `CBool`, `CByte`, `CCur`, `CDate`, `CDat`, `CDbl`, `CInt`, `CLng`, `CSng`, `CStr`, `Val`
- [x] `CType` for supported XPScript scalar type names
- [x] `CVDate`
- [x] `DataType`, `TypeName`, `IsArray`, `IsDate`, `IsNull`, `IsNumeric`, `IsObject`, `IsScalar`
- [x] list element presence support
- [x] `IsList`
- [x] `IsUnknown`

## 7. Math and date/time

- [x] math functions in the reference: `Abs`, `Atn`, `Cos`, `Exp`, `Fix`, `Hex`, `Int`, `Log`, `Oct`, `Rnd`, `Randomize`, `Round`, `Sgn`, `Sin`, `Sqr`, `Tan`
- [x] date/time functions in the reference: `Date`, `DateNumber`, `DateValue`, `Day`, `Hour`, `Minute`, `Month`, `MonthName`, `Now`, `Second`, `Time`, `TimeNumber`, `TimeValue`, `Timer`, `Today`, `Weekday`, `WeekdayName`, `Year`

## 8. Arrays and lists

- [x] typed dynamic arrays such as `Dim values() As String`
- [x] fixed arrays, multidimensional arrays and explicit bounds
- [x] `Array`, `ReDim`, `ReDim Preserve`, `Erase`, `LBound`, `UBound`
- [x] `Join`, `Explode`, `ArrayGetIndex`, `ArrayAppend`, `ArrayUnique`, `ArraySplice`, `ArraySlice`
- [x] keyed lists, iteration, tag lookup and erase

## 9. File I/O and filesystem

- [x] `FreeFile`, `Open`, `Close`, `Input #`, `Line Input`, `Print #`, `Write #`, `Get`, `Put`, `EOF`, `LOF`, `Loc`, `Seek`, `Reset`
- [x] Charset-aware Input/Output/Append
- [x] separate `Encoding "base64"` layer combinable with Charset
- [>] file form `Input$(count, #fileNumber)` implemented as file I/O, separate from console/user input; regression test added and awaiting explicit verification
- [>] `Lock` implemented using the underlying OS `FileStream.Lock`; Binary ranges are 1-based byte ranges, Random ranges use record length where statically known, sequential modes lock the whole file; cross-handle OS lock test added and awaiting explicit verification
- [>] `Unlock` implemented using matching OS `FileStream.Unlock` semantics; awaiting explicit verification
- [x] `ChDir`, `CurDir`, `Dir`, `FileCopy`, `FileDateTime`, `FileLen`, `Kill`, `MkDir`, rename/move, `RmDir`
- [>] `ChDrive` Windows implementation added; awaiting explicit verification
- [>] explicit `latin1` charset round-trip regression test added; awaiting explicit verification

### File input separation

`Input$(count, #fileNumber)` is the file function and must never be rewritten as interactive input. Console/user input remains a separate XPScript extension (`Input variable`, `Input "prompt", variable`, and console `Input$ variable`).

### File locking semantics

`Lock`/`Unlock` are operating-system file locks, not XPScript-only state. Tests must verify that a second OS file handle cannot lock the same region while XPScript holds the lock. Binary ranges map to bytes. Random ranges map to records multiplied by the configured record length. Sequential Input/Output/Append lock the whole file.

## 10. Formatting, process and console

- [x] `Format`, `Format$`, `FormatNumber`, `FormatPercent`
- [x] `Environ`, `Shell`, `Sleep`
- [x] console `Print`, `Print$`, interactive `Input`, interactive `Input$`, `Pause`
- [x] `InputBox`, `MessageBox`, `MsgBox`, `Beep`

## 11. Base64 and URL helpers

- [x] `ToBase64`, `FromBase64`, `UrlEncode`, `UrlDecode`
- [x] aliases `Base64Encode`, `Base64Decode`
- [ ] binary-return form for Base64 decode

## 12. Standalone HTTP API

Native HTTP is implemented in `NativeHttpRuntimeSource` and exposed through the XPScript names below. It is independent from the previous compatibility facade. Verification is intentionally deferred while workflows are disabled.

- [>] `HttpClient`
- [>] `HttpClient.Get`
- [>] `HttpClient.Post`
- [>] `HttpClient.Put`
- [>] `HttpClient.Patch`
- [>] `HttpClient.Delete`
- [>] `HttpClient.SetHeader`
- [>] `HttpClient.RemoveHeader`
- [>] `HttpClient.ClearHeaders`
- [>] `HttpClient.Timeout` (seconds)
- [>] `HttpResponse.StatusCode`
- [>] `HttpResponse.StatusText`
- [>] `HttpResponse.Body`
- [>] `HttpResponse.ContentType`
- [>] `HttpResponse.Headers`
- [>] `HttpResponse.IsSuccess`

## 13. Standalone JSON API

Native JSON is implemented with `System.Text.Json.Nodes` in `NativeJsonRuntimeSource`. Public `.xps` syntax is normalized by `NativeHttpJsonPreprocessor`. Verification is intentionally deferred while workflows are disabled.

- [>] `JsonDocument.Parse`, `JsonDocument.Stringify`
- [>] `JsonObject.Get`, `Set`, `Remove`, `Contains`, `Count`
- [>] `JsonArray.Add`, `Get`, `Set`, `RemoveAt`, `Count`
- [>] `JsonElement.Type`, `JsonElement.Value`
- [>] `JsonParse`
- [>] `JsonStringify`
- [>] `JsonEncode`
- [>] `JsonDecode`

## 14. Quality gates

Every completed item above should satisfy these gates:

1. XPScript syntax is accepted by the compiler.
2. The generated project builds with .NET 10.
3. Runtime behavior is verified by a `.xps` regression sample.
4. Negative/type-error cases produce XPScript diagnostics where applicable.
5. Existing compatibility, class/list, core, HTTP/JSON, text I/O and operator/array tests remain green.
6. New public API names use XPScript branding only.
7. File `Lock`/`Unlock` tests must verify contention from a second operating-system file handle, not merely internal runtime state.
8. While workflows are disabled, `[>]` means implemented but not yet promoted to verified `[x]`.
