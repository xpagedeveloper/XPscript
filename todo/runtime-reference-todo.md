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
- [>] nested `Type` copy into module-level `Type` values uses detached copy-then-commit semantics and handles self-assignment; source: `samples/module-nested-type-value-copy.xps`
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

- [>] `Date.Adjust(years, months, days, hours, minutes, seconds)` implemented in runtime/preprocessor, awaiting verification
- [>] all `Adjust` components accept positive, zero or negative integer values
- [>] combined year/month/day/hour/minute/second adjustment supported
- [>] calendar-safe month/year semantics rely on .NET `DateTime.AddYears/AddMonths`
- [>] leap-year behavior follows .NET DateTime semantics
- [>] Date time component is preserved for date-only adjustments
- [>] `Adjust` returns a new Date value
- [>] `Date.Difference(otherDate)` implemented as signed total seconds (`otherDate - currentDate`)
- [>] Date comparison operators use full DateTime values where date typing is known
- [ ] negative type diagnostics for nonsensical Date comparisons
- [ ] runtime regression verification when test execution is re-enabled
- [ ] English documentation/examples under `docs/` and `examples/`

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

- [ ] remove all legacy formula-engine references from code/docs/samples/public terminology
- [>] `Evaluate(sourceText)` executes XPScript supplied as text through an isolated evaluator
- [>] `Evaluate(sourceText, callvar)` restricted parameter bridge implemented
- [>] scalar/Variant and defensive-copy array semantics implemented
- [>] evaluator scope isolated from caller locals
- [>] `Return expression` is explicit result path
- [>] no `Return` yields Nothing/Empty; source: `samples/evaluate-no-return.xps`
- [>] `TypeName`, `LBound`, `UBound` plus basic conversions/string/math helpers available inside Evaluate; source: `samples/evaluate-array-helpers.xps`
- [ ] remove old unused DataTable-based evaluator implementation
- [ ] broaden standard XPScript function coverage inside Evaluate
- [ ] full List/nested collection snapshot validation
- [ ] align evaluator coercion and diagnostics with main compiler/runtime
- [ ] safe-use documentation/examples
- [ ] detailed checklist: `todo/evaluate-callvar-todo.md`

## 16. Security review and isolation

- [ ] dedicated compiler/preprocessor/runtime/temp-build security review
- [>] compiler-generated `__*` names are reserved and runtime/public type names protected from user type declarations
- [>] reserved identifier validation runs before source rewrites
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

## 18. Cross-platform UI extension inventory

Design goal: add a small, platform-native UI extension for simple forms and dialogs on Windows, Linux and macOS. Keep the XPScript API stable across platforms while allowing the backend implementation to use the most appropriate UI toolkit for each operating system.

### 18.1 Core classes and data model

- [ ] define a top-level `UIForm` class for creating and showing simple forms
- [ ] define a document-style form data class, proposed name `UIData`, used as the backing store for all form field values
- [ ] reuse familiar document-style method names for value access, especially `GetItemValue`, `GetFirstItem`, `HasItem`, `ReplaceItemValue`, `RemoveItem`, `RemoveAllItems` and equivalent safe subset
- [ ] `GetItemValue(name)` returns all values for the named field as an XPScript array
- [ ] provide `GetItemValueString(name)` convenience method for a single text representation
- [ ] `ReplaceItemValue(name, value)` creates or replaces a field value and returns a field/item object where useful
- [ ] field names are case-insensitive unless a later compatibility decision explicitly requires otherwise
- [ ] values may be scalar or multivalue and preserve XPScript types where possible: String, Integer, Long, Double, Currency, Boolean, Date and arrays
- [ ] form field state must be independent per `UIForm`/`UIData` instance; no cross-form state leakage
- [ ] define `UIItem`/`UIFieldValue` object only if needed for metadata such as name, type, value, validation state and dirty state

### 18.2 Form lifecycle and layout

- [ ] create form with title, optional width/height and optional resizable flag
- [ ] modal `ShowDialog()` returning a stable result such as `OK`, `Cancel`, `Yes`, `No`
- [ ] optionally support non-modal `Show()` later; modal dialogs are MVP
- [ ] close/cancel behavior consistent across Windows, Linux and macOS
- [ ] simple layout abstraction that avoids requiring pixel-perfect platform-specific coordinates

### 18.3 UI element inventory

- [ ] Label
- [ ] TextField
- [ ] PasswordField
- [ ] TextArea
- [ ] NumberField
- [ ] DateField
- [ ] TimeField
- [ ] DateTimeField
- [ ] CheckBox
- [ ] RadioButton/RadioGroup
- [ ] ComboBox
- [ ] ListBox
- [ ] MultiListBox
- [ ] Button
- [ ] Separator/spacer
- [ ] per-control default value, required/read-only/enabled/visible state, tooltip, placeholder and size hints where appropriate

### 18.4 Validation

- [ ] required
- [ ] min/max text length
- [ ] numeric min/max
- [ ] date min/max
- [ ] regular expression
- [ ] allowed values
- [ ] custom XPScript validation callback
- [ ] field-level validation errors and form-level validation before OK/submit

### 18.5 Dialog inventory

- [ ] MessageBox with stable XPScript parameters/return codes across platforms
- [ ] OK
- [ ] OK/Cancel
- [ ] Yes/No
- [ ] Yes/No/Cancel
- [ ] Retry/Cancel
- [ ] question/confirm
- [ ] text input dialog
- [ ] password input dialog
- [ ] single-select list dialog
- [ ] multi-select list dialog
- [ ] file-open dialog
- [ ] multi-file-open dialog
- [ ] file-save dialog
- [ ] folder selection dialog
- [ ] file filters, initial directory, default filename, overwrite confirmation and correct Cancel semantics

### 18.6 Data binding semantics

- [ ] form starts with an isolated working copy of `UIData`
- [ ] user edits update only the working copy while the dialog is open
- [ ] OK commits form values to returned/form Data
- [ ] Cancel discards working-copy changes unless explicitly configured otherwise
- [ ] values preserve scalar/multivalue XPScript types where possible

### 18.7 Cross-platform backend inventory

- [ ] investigate Windows backend
- [ ] investigate Linux backend such as GTK or equivalent
- [ ] investigate macOS backend
- [ ] evaluate whether one cross-platform .NET UI toolkit can provide consistent behavior without excessive runtime size
- [ ] prefer native file/message dialogs where practical
- [ ] define UI thread/event-loop integration
- [ ] detect headless/server environment and return clear runtime errors
- [ ] architecture-specific dependencies for x64/arm64
- [ ] package UI dependencies only when generated program actually uses the UI extension where feasible

### 18.8 UI security/lifetime

- [ ] isolate all form/data instances
- [ ] ensure password values are not logged in diagnostics or default debug output
- [ ] validate callbacks cannot overwrite unrelated runtime/compiler state
- [ ] deterministically release windows/dialog/native handles
- [ ] close/dispose event loops and native UI resources correctly
- [ ] include UI objects in memory/lifetime and security reviews

## 19. Documentation and examples

- [ ] complete English docs for every statement, function, class, property and operator
- [ ] all end-user docs under `docs/`
- [ ] reusable `.xps` programs under `examples/`; keep test fixtures under `samples/`
- [ ] every documented API links to an example or contains an equivalent inline example
- [ ] `docs/index.md`
- [ ] language-reference index by declarations/control/operators/strings/math/date/arrays/files/HTTP/JSON/process/platform/UI/diagnostics
- [ ] grouped or per-feature pages with syntax, parameters, return value, errors and examples
- [ ] type coercion documentation
- [ ] compiler CLI including output format, target RID/platform and exit codes
- [ ] separate file `Input$` versus console input docs
- [ ] OS `Lock/Unlock` semantics
- [ ] `Platform`, cross-platform `Shell` and publishing
- [ ] XPScript-only `Evaluate`
- [ ] UI extension documentation and examples
- [ ] XPScript branding only; no legacy product names or formula-engine terminology

## 20. Quality gates

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
12. UI backend validation on each supported desktop platform for UI features.
