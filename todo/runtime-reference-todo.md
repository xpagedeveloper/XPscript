# XPScript runtime reference implementation TODO

(c) xpagedeveloper.com 2026

This file tracks implementation against the standalone runtime reference supplied for XPScript.

Status legend:

- `[x]` implemented and covered by CI or an existing regression sample
- `[-]` partially implemented; more semantics/tests are required
- `[>]` implementation currently in progress
- `[ ]` not implemented yet

## 1. Core language and declarations

- [x] `Sub`, `Function`, `Call`, `Exit Sub`, `Exit Function`
- [x] scalar declarations: Variant, Boolean, Byte, Integer, Long, Single, Double, Currency, String, Date, Object
- [x] `Dim`, `Static`, `ByVal`, explicit `ByRef`, `Set`, `New`, `Delete`
- [ ] `Optional` parameters including omitted argument behavior and default values
- [ ] module-level `Public` variables
- [ ] module-level `Private` variables
- [ ] user-defined `Type ... End Type`
- [ ] `Enum ... End Enum`

## 2. Classes and properties

- [x] classes, methods, constructors, destructors, `Me`
- [x] parameterless `Property Get`
- [x] parameterless object `Property Set`
- [ ] scalar `Property Let`
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
- [>] `InstrB`, `LeftB`, `RightB`, `MidB`
- [ ] `StrConv`
- [>] `StrLeft`, `StrLeftBack`, `StrRight`, `StrRightBack`, `StrToken`
- [>] `LSet`, `RSet`
- [>] `UChr`, `Uni`

## 6. Conversion and inspection

- [x] `CBool`, `CByte`, `CCur`, `CDate`, `CDat`, `CDbl`, `CInt`, `CLng`, `CSng`, `CStr`, `Val`
- [>] `CType`
- [>] `CVDate`
- [x] `DataType`, `TypeName`, `IsArray`, `IsDate`, `IsNull`, `IsNumeric`, `IsObject`, `IsScalar`
- [x] list element presence support
- [>] `IsList`
- [>] `IsUnknown`

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

- [x] `FreeFile`, `Open`, `Close`, `Input`, `Line Input`, `Print #`, `Write #`, `Get`, `Put`, `EOF`, `LOF`, `Loc`, `Seek`, `Reset`
- [x] Charset-aware Input/Output/Append
- [x] separate `Encoding "base64"` layer combinable with Charset
- [ ] file form `Input$(count, #fileNumber)`
- [ ] `Lock`
- [ ] `Unlock`
- [x] `ChDir`, `CurDir`, `Dir`, `FileCopy`, `FileDateTime`, `FileLen`, `Kill`, `MkDir`, rename/move, `RmDir`
- [ ] `ChDrive`
- [ ] explicit `latin1` charset regression test

## 10. Formatting, process and console

- [x] `Format`, `Format$`, `FormatNumber`, `FormatPercent`
- [x] `Environ`, `Shell`, `Sleep`
- [x] console `Print`, `Print$`, `Input`, `Input$`, `Pause`
- [x] `InputBox`, `MessageBox`, `MsgBox`, `Beep`

## 11. Base64 and URL helpers

- [x] `ToBase64`, `FromBase64`, `UrlEncode`, `UrlDecode`
- [>] aliases `Base64Encode`, `Base64Decode`
- [ ] binary-return form for Base64 decode

## 12. Standalone HTTP API

The current compatibility HTTP implementation is functional, but the reference defines a new XPScript-native public API. Keep the old compatibility facade only as a migration layer while adding the new public surface.

- [ ] `HttpClient`
- [ ] `HttpClient.Get`
- [ ] `HttpClient.Post`
- [ ] `HttpClient.Put`
- [ ] `HttpClient.Patch`
- [ ] `HttpClient.Delete`
- [ ] `HttpClient.SetHeader`
- [ ] `HttpClient.RemoveHeader`
- [ ] `HttpClient.ClearHeaders`
- [ ] `HttpClient.Timeout`
- [ ] `HttpResponse.StatusCode`
- [ ] `HttpResponse.StatusText`
- [ ] `HttpResponse.Body`
- [ ] `HttpResponse.ContentType`
- [ ] `HttpResponse.Headers`
- [ ] `HttpResponse.IsSuccess`

## 13. Standalone JSON API

The current JSON compatibility implementation is functional, but the reference defines XPScript-native names and helper functions.

- [ ] `JsonDocument.Parse`, `JsonDocument.Stringify`
- [ ] `JsonObject.Get`, `Set`, `Remove`, `Contains`, `Count`
- [ ] `JsonArray.Add`, `Get`, `Set`, `RemoveAt`, `Count`
- [ ] `JsonElement.Type`, `JsonElement.Value`
- [ ] `JsonParse`
- [ ] `JsonStringify`
- [ ] `JsonEncode`
- [ ] `JsonDecode`

## 14. Quality gates

Every completed item above should satisfy these gates:

1. XPScript syntax is accepted by the compiler.
2. The generated project builds with .NET 10.
3. Runtime behavior is verified by a `.xps` regression sample.
4. Negative/type-error cases produce XPScript diagnostics where applicable.
5. Existing compatibility, class/list, core, HTTP/JSON, text I/O and operator/array tests remain green.
6. New public API names use XPScript branding only.
