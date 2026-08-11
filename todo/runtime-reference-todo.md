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
- [>] module-level fixed/dynamic arrays with `ReDim`, `ReDim Preserve`, indexed reads/writes, bounds and `Erase`; source: `samples/module-arrays.xps`
- [>] module-level custom `Type` values; source: `samples/module-type-values.xps`
- [>] module-level class/object references with `Set`, `New`, aliases, `Nothing`, identity, member access and `Delete`; source: `samples/module-object-references.xps`
- [>] `Type ... End Type`: scalar fields, auto initialization and scalar value-copy; source: `samples/type-value-copy.xps`
- [>] `Type` array fields: fixed/dynamic fields, indexing, `ReDim`, `Erase`, bounds and deep array-copy; source: `samples/type-array-members.xps`
- [>] nested `Type` deep-copy recursively clones nested values and nested array storage; source: `samples/type-nested-value-copy.xps`
- [>] cyclic nested `Type` copy graphs produce an explicit compiler diagnostic instead of unbounded clone generation; source: `samples/type-cycle-error.xps`
- [>] implicit lower bounds in `ReDim typeValue.arrayField(n)` honor active `Option Base`; source: `samples/type-array-option-base.xps`
- [ ] verify nested `Type` copy when the destination itself is a module-level `Type` value
- [x] `Enum ... End Enum`: explicit values, auto increment, qualified/unqualified members

## 2. Classes and properties

- [x] classes, methods, constructors, destructors, `Me`
- [x] parameterless `Property Get`
- [x] parameterless object `Property Set`
- [x] scalar `Property Let`
- [>] parameterized/indexed `Property Get`
- [>] parameterized/indexed `Property Let/Set`
- [>] indexed properties lower to typed methods so normal parameter diagnostics apply
- [>] positive source: `samples/indexed-properties.xps`
- [>] negative type source: `samples/indexed-properties-error.xps`

## 3. Control flow and error handling

- [x] `If`, `ElseIf`, `Else`, `Select Case`
- [x] `For/Next/Step`, `Do/Loop`, `Do While`, `Do Until`, `While/Wend`, `ForAll`
- [x] `GoTo`, `GoSub`, labels, `Return`
- [x] `On Error`, `Resume`, `Resume Next`, `Err`, `Error`, `Error$`, `Erl`
- [-] physical source-line accuracy for `Erl`
- [-] deeply nested `Resume` targets

## 4. Operators

- [x] comparisons, `Like`, object identity `Is`
- [x] `And`, `Or`, `Not`, `Xor`, `Eqv`, `Imp`
- [x] arithmetic operators and `Mod`, `^`
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

- [x] scalar conversion functions including `CType`, `CVDate`
- [x] `DataType`, `TypeName`, `IsArray`, `IsDate`, `IsNull`, `IsNumeric`, `IsObject`, `IsScalar`, `IsList`, `IsUnknown`

## 7. Math and date/time

- [x] reference math functions
- [x] reference date/time functions

### Date object enhancements

- [ ] add `Date.Adjust(years, months, days, hours, minutes, seconds)` for increasing or decreasing an existing Date value
- [ ] all `Adjust` components accept positive, zero or negative integer values
- [ ] `Adjust` must support changing years, months, days, hours, minutes and seconds in one call
- [ ] define calendar-safe month/year adjustment semantics for month-end dates, e.g. adjusting January 31 by one month
- [ ] define and test leap-year behavior, including February 29 when adding/subtracting years
- [ ] preserve the Date value's time component when only year/month/day fields are adjusted
- [ ] return a Date value from `Adjust`; the original Date value must not be mutated unless XPScript Date semantics explicitly define value assignment back to the variable
- [ ] add `Date.Difference(otherDate)` returning the signed difference in seconds between the current Date value and `otherDate`
- [ ] define `Difference` sign as `otherDate - currentDate`: a later supplied date returns positive seconds and an earlier supplied date returns negative seconds
- [ ] `Difference` must include days, hours, minutes and seconds in the total returned seconds rather than returning only the Seconds component
- [ ] define the `Difference` return type large enough for long date ranges, preferably `Double` or `Long` after reviewing precision/range requirements
- [ ] Date comparison operators must work directly between Date values: `=`, `<>`, `<`, `<=`, `>`, `>=`
- [ ] Date comparison must compare the complete date/time value, not formatted strings or only the calendar date
- [ ] examples to support: `date1 > date2`, `date1 >= date2`, `date1 = date2`, `date1 <> date2`, `date1 < date2`, `date1 <= date2`
- [ ] compiler/type-coercion rules must reject nonsensical Date comparisons rather than silently comparing incompatible strings unless an existing documented Date conversion rule explicitly applies
- [ ] add positive and negative regression sources for `Date.Adjust`, `Date.Difference` and all Date comparison operators when test execution is re-enabled
- [ ] add English documentation and examples for Date object operations under `docs/` and `examples/`

Suggested surface examples:

```xpscript
Dim startDate As Date
Dim adjustedDate As Date
Dim endDate As Date
Dim seconds As Double

startDate = DateNumber(2026, 1, 31)
adjustedDate = startDate.Adjust(0, 1, 0, 0, 0, 0)

endDate = adjustedDate.Adjust(0, 0, 1, 2, 30, 15)
seconds = adjustedDate.Difference(endDate)

If endDate > adjustedDate Then
    Print "endDate is later"
End If
```

## 8. Arrays and lists

- [x] typed dynamic arrays
- [x] fixed/multidimensional arrays and explicit bounds
- [x] `Array`, `ReDim`, `ReDim Preserve`, `Erase`, `LBound`, `UBound`
- [x] array helper functions and keyed lists
- [>] arrays as `Type` members including deep-copy of array storage

## 9. File I/O and filesystem

- [x] standard file open/read/write/seek/reset operations
- [x] Charset-aware Input/Output/Append and independent Base64 encoding layer
- [>] file `Input$(count, #fileNumber)` distinct from interactive input
- [>] OS `Lock` / `Unlock` with Binary byte ranges, Random record ranges and sequential whole-file semantics
- [x] standard filesystem operations
- [>] `ChDrive`
- [>] explicit Latin-1 regression source

File input and interactive input are distinct APIs. `Lock/Unlock` must be verified from a second operating-system file handle when tests are re-enabled.

## 10. Formatting, process and console

- [x] formatting functions
- [x] `Environ`, current `Shell`, `Sleep`
- [x] console Print/Input/Pause and message-box helpers

## 11. Base64 and URL

- [x] `ToBase64`, `FromBase64`, `UrlEncode`, `UrlDecode`
- [x] `Base64Encode`, `Base64Decode`
- [>] binary-return Base64 decode via `Base64DecodeBinary()`; source: `samples/base64-binary.xps`

## 12. Native HTTP API

- [>] `HttpClient`
- [>] `Get`, `Post`, `Put`, `Patch`, `Delete`
- [>] `SetHeader`, `RemoveHeader`, `ClearHeaders`, `Timeout`
- [>] `HttpResponse.StatusCode`, `StatusText`, `Body`, `ContentType`, `Headers`, `IsSuccess`
- [>] source: `samples/native-http-json.xps`

## 13. Native JSON API

- [>] `JsonDocument.Parse`, `JsonDocument.Stringify`
- [>] `JsonObject.Get`, `Set`, `Remove`, `Contains`, `Count`
- [>] `JsonArray.Add`, `Get`, `Set`, `RemoveAt`, `Count`
- [>] `JsonElement.Type`, `JsonElement.Value`
- [>] `JsonParse`, `JsonStringify`, `JsonEncode`, `JsonDecode`

## 14. Cross-platform compiler and runtime

- [>] publish-target support for Windows, Linux and macOS has been added but is not yet runtime-verified
- [>] compiler runtime targets: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`
- [>] default target follows the compiler host OS and x64/arm64 process architecture
- [>] `Platform()` returns stable runtime names including `Windows`, `Linux`, `MacOS`
- [>] platform-specific branching using `Platform()`
- [>] cross-platform `Shell()` implementation
- [>] Windows: `.exe`, `.cmd`, `.bat`, `.ps1` and commands
- [>] Linux/macOS: binaries, executable/shebang scripts, `.sh`/`.bash`, and `.ps1` through `pwsh`
- [>] argument handling uses `ProcessStartInfo.ArgumentList` where possible to avoid unintended shell re-parsing
- [>] clear runtime errors for unsupported/unexecutable targets
- [>] target-selected native library declarations with `WindowsLib`, `LinuxLib`, `MacOSLib`
- [>] target-selected native entry points with `WindowsAlias`, `LinuxAlias`, `MacOSAlias`
- [>] multiline platform-specific `Declare` statements with `_`; source: `samples/platform-native-library.xps`
- [ ] package/copy application-local `.dll`, `.so`, `.dylib` dependencies alongside generated output when required
- [ ] support architecture-specific native assets for x64 vs arm64
- [ ] define managed .NET assembly references separately from native-library declarations, including assemblies with RID-specific native dependencies
- [ ] validate native-library search paths and loading behavior on Windows, Linux and macOS
- [ ] validate file-I/O portability across Windows, Linux and macOS: path separators, roots/drives, case sensitivity, permissions, symlinks, file sharing, delete-open-file semantics, rename/move behavior, newline handling, charset/BOM and binary identity
- [ ] validate OS file locks and region locks separately on Windows, Linux and macOS
- [ ] keep `ChDrive` explicitly Windows-only and provide clear behavior/error semantics elsewhere
- [ ] detailed portability checklist: `todo/cross-platform-runtime-todo.md`

## 15. Evaluate

- [ ] remove all `@Formula` references from code/docs/samples/public terminology
- [ ] `Evaluate(sourceText)` executes only XPScript supplied as text
- [ ] no external formula-language compatibility
- [ ] define expression/statements/snippet grammar for `Evaluate`
- [ ] isolate evaluated code from compiler/runtime internals
- [ ] normal XPScript values, coercion and diagnostics
- [ ] safe-use documentation/examples

## 16. Security review and isolation

- [ ] dedicated compiler/preprocessor/runtime/temp-build security review
- [ ] prevent user/generated identifier collisions; reserve or namespace internal identifiers including `__xp_*`
- [ ] verify scope isolation for locals, globals, statics, arrays, lists and ByRef
- [ ] verify modules cannot overwrite unrelated module state
- [ ] verify concurrent compiler builds use isolated temp paths
- [ ] prevent output path traversal/unrelated-file overwrite
- [ ] review temp permissions and cleanup
- [ ] review `Shell`, file I/O, HTTP, `Evaluate`, P/Invoke and COM for injection risks
- [ ] review JSON/HTTP conversions and header/body handling
- [ ] review `Lock/Unlock` races and cross-process assumptions
- [ ] negative/adversarial regression tests when execution is re-enabled
- [ ] document security boundaries and powerful/unsafe features

## 17. Memory management and object/resource lifetime

- [ ] determine any explicit memory semantics needed beyond .NET GC
- [ ] define `Nothing` versus `Null`
- [ ] verify `Set object = Nothing` releases that reference and allows unreachable objects to become GC-eligible
- [ ] verify aliases remain alive until the last reference is cleared
- [ ] define/review `Delete` versus clearing one reference
- [ ] ensure locals are not retained by generated closures/error contexts/ByRef/static caches
- [ ] ensure globals/statics retain and release references intentionally
- [ ] ensure `Erase`, `ReDim` and replacement release removed array/list references
- [ ] verify `Type` copies retain no hidden compiler-owned aliases
- [ ] review HTTP/JSON objects and static caches for unnecessary lifetime extension
- [ ] deterministic cleanup for files, streams, sockets, HTTP responses, locks, process handles, COM and native allocations
- [ ] ensure `Close`, `Reset`, `Unlock`, disposal and process cleanup release OS resources
- [ ] define optional class disposal/finalization behavior in addition to `Sub Delete`
- [ ] inspect `IDisposable` / `IAsyncDisposable` ownership and exactly-once disposal
- [ ] do not use `GC.Collect()` as normal language behavior
- [ ] memory/lifetime regression and leak/stress tests when execution is enabled
- [ ] document `Nothing`, `Null`, `Delete`, `Erase`, scope and unmanaged-resource cleanup

Design direction: managed memory is reclaimed by .NET GC after the last strong reference disappears. `Nothing`/`Null` can make objects eligible for collection but do not mean immediate deallocation. OS/unmanaged resources require deterministic cleanup.

## 18. Documentation and examples

- [ ] complete English docs for every statement, function, class, property and operator
- [ ] all end-user docs under `docs/`
- [ ] reusable `.xps` programs under `examples/`; keep test fixtures under `samples/`
- [ ] every documented API links to an example or contains an equivalent inline example
- [ ] `docs/index.md`
- [ ] language-reference index by declarations/control/operators/strings/math/date/arrays/files/HTTP/JSON/process/platform/diagnostics
- [ ] grouped or per-feature pages with syntax, parameters, return value, errors and examples
- [ ] type coercion documentation
- [ ] compiler CLI including output format, target RID/platform and exit codes
- [ ] separate file `Input$` versus console input docs
- [ ] OS `Lock/Unlock` semantics
- [ ] `Platform`, cross-platform `Shell` and publishing
- [ ] XPScript-only `Evaluate`
- [ ] XPScript branding only; no legacy product names or `@Formula`

## 19. Quality gates

A feature becomes `[x]` only after requested verification is re-enabled and passes:

1. XPScript parsing/transpilation.
2. Generated .NET 10 build.
3. positive `.xps` runtime regression.
4. negative/type diagnostics where applicable.
5. existing language/runtime regressions.
6. XPScript-only public branding.
7. OS cross-handle verification for `Lock/Unlock`.
8. target-OS validation for cross-platform features.
9. adversarial coverage for security-sensitive features.
10. reference-release and deterministic OS-resource cleanup tests for memory/lifetime changes.
11. valid linked `examples/` source or equivalent inline example for documentation work.
