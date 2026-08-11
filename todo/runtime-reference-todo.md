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
- [ ] module-level fixed/dynamic arrays
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

## 14. Quality gates

A feature is promoted from `[>]` to `[x]` only when requested verification is enabled and it passes:

1. XPScript parsing/transpilation.
2. Generated .NET 10 build.
3. `.xps` positive runtime regression.
4. Negative/type diagnostic regression where applicable.
5. Existing language/runtime regressions.
6. XPScript-only public branding.
7. OS cross-handle verification for `Lock/Unlock`.
