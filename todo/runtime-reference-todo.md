# XPScript runtime reference implementation TODO

(c) xpagedeveloper.com 2026

Tracks implementation against the standalone XPScript runtime reference.

Development note: GitHub Actions verification is enabled. Features are marked `[x]` only after their applicable compiler/runtime regression gates pass.

Status:
- `[x]` implemented and verified
- `[-]` partially implemented
- `[>]` implemented/in progress, awaiting explicit verification
- `[ ]` not implemented

## 1. Core language and declarations

- [x] `Sub`, `Function`, `Call`, `Exit Sub`, `Exit Function`
- [x] scalar types: Variant, Boolean, Byte, Integer, Long, Single, Double, Currency, String, Date, Object
- [x] `Dim`, `Static`, `ByVal`, explicit `ByRef`, `Set`, `New`, `Delete`
- [x] `Optional` parameters, defaults, omitted trailing arguments and omitted slots
- [x] module-level `Public` scalar variables
- [x] module-level `Private` scalar variables
- [x] module-level fixed/dynamic arrays with `ReDim`, `ReDim Preserve`, indexed reads/writes, bounds and `Erase`; source: `samples/module-arrays.xps`
- [x] module-level custom `Type` values; source: `samples/module-type-values.xps`
- [x] module-level class/object references with `Set`, `New`, aliases, `Nothing`, identity, member access and `Delete`; source: `samples/module-object-references.xps`
- [x] `Type ... End Type`: scalar fields, auto initialization and scalar value-copy; source: `samples/type-value-copy.xps`
- [x] `Type` array fields: fixed/dynamic fields, indexing, `ReDim`, `Erase`, bounds and deep array-copy; source: `samples/type-array-members.xps`
- [x] nested `Type` deep-copy recursively clones nested values and nested array storage; source: `samples/type-nested-value-copy.xps`
- [x] cyclic nested `Type` copy graphs produce an explicit compiler diagnostic instead of unbounded clone generation; source: `samples/type-cycle-error.xps`
- [x] implicit lower bounds in `ReDim typeValue.arrayField(n)` honor active `Option Base`; source: `samples/type-array-option-base.xps`
- [x] nested `Type` copy into module-level `Type` values uses detached copy-then-commit semantics and handles self-assignment; source: `samples/module-nested-type-value-copy.xps`
- [x] `Enum ... End Enum`: explicit values, auto increment, qualified/unqualified members

## 2. Classes and properties

- [x] classes, methods, constructors, destructors, `Me`
- [x] parameterless `Property Get`
- [x] parameterless object `Property Set`
- [x] scalar `Property Let`
- [x] parameterized/indexed `Property Get`
- [x] parameterized/indexed `Property Let/Set`
- [x] indexed properties lower to typed methods so normal parameter diagnostics apply
- [x] indexed object getters/setters preserve `Set` reference semantics, including object-returning Function assignment; sources: `samples/indexed-properties.xps`, `samples/indexed-object-properties.xps`
- [x] positive scalar source: `samples/indexed-properties.xps`
- [x] negative type source: `samples/indexed-properties-error.xps`
- [x] class `Function`/`Sub` overload resolution for distinct scalar/object typed signatures, different arity, `Optional` specificity, typed object fallback, `Me`, explicit `Call` and bare member calls; source: `samples/class-method-overloads.xps`
- [x] overload diagnostics for duplicate effective signatures, no matching overload and ambiguous calls; sources: `samples/class-method-overloads-duplicate.xps`, `samples/class-method-overloads-no-match.xps`, `samples/class-method-overloads-ambiguous.xps`
- [x] class overload follow-ups for array/scalar overload end-to-end coverage and scalar `ByRef`; detailed checklist: `todo/function-sub-overloading-todo.md`

## 3. Control flow and error handling

- [x] `If`, `ElseIf`, `Else`, `Select Case`
- [x] support all valid `If` statement layouts consistently; source: `samples/if-layouts.xps`; permanent manual gate: `Control Flow and Error Handling Compatibility`:
  - [x] single-line `If condition Then statement`
  - [x] single-line branches such as `If condition Then statement Else statement` and block `ElseIf condition Then statement` forms
  - [x] `If condition Then` followed by statement(s) and `End If` on a later line
  - [x] split `If condition` / `Then` and `ElseIf condition` / `Then` forms while preserving physical source line count
  - [x] fully multiline block form with `If`, `Then`, body and `End If` on separate lines
  - [x] Date/comparison lowering and other preprocessors preserve single-line `If ... Then ...` syntax instead of producing `Unsupported statement` diagnostics; original regression discovered by `examples/date-comparisons.xps` testing
- [x] audit all documented control-flow/declaration statement layouts for the same line-shape assumption; source: `samples/statement-layout-audit.xps`; detailed checklist: `todo/done/statement-layout-audit-todo.md`; permanent manual gate: `Control Flow and Error Handling Compatibility`:
  - [x] verify `_` line continuation remains accepted for long expressions, argument lists, procedure headers and control-flow expressions
  - [x] verify `ElseIf` / `Else` supported layouts and nested single-line/block combinations
  - [x] verify `Select Case`, `Case`, `With`, `For/Next`, `ForAll`, `Do/Loop`, `While/Wend`, procedure/property/class headers and native declarations do not produce false `Unsupported statement` errors for documented/valid multiline layouts
  - [x] add regression samples for every newly identified valid alternate layout; arbitrary unsupported keyword splitting is not treated as valid grammar
- [x] `For/Next/Step`, `Do/Loop`, `Do While`, `Do Until`, `While/Wend`, `ForAll`
- [x] `GoTo`, `GoSub`, labels, `Return`
- [x] `On Error`, `Resume`, `Resume Next`, `Err`, `Error`, `Error$`, `Erl`
- [x] physical source-line accuracy for `Erl`; source: `samples/erl-physical-source-line.xps`
- [x] deeply nested `Resume` targets use stacked per-error-context resume frames so nested procedure calls preserve the innermost failing statement; source: `samples/nested-resume-targets.xps`

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

- [x] `Date.Adjust(years, months, days, hours, minutes, seconds)`
- [x] all `Adjust` components accept positive, zero or negative integer values
- [x] combined year/month/day/hour/minute/second adjustment supported
- [x] calendar-safe month/year semantics rely on .NET `DateTime.AddYears/AddMonths`
- [x] leap-year behavior follows .NET DateTime semantics
- [x] Date time component is preserved for date-only adjustments
- [x] `Adjust` returns a new Date value
- [x] `Date.Difference(otherDate)` returns signed total seconds (`otherDate - currentDate`)
- [x] Date comparison operators use full DateTime values where date typing is known
- [x] Date/String/numeric/Variant coercion paths are regression-tested, including mixed equality
- [x] negative type diagnostics reject statically known nonsensical Date comparisons (Boolean, Object, arrays, Class/Type values); sources: `samples/date-comparisons-valid.xps`, `samples/date-comparisons-invalid.xps`
- [x] `Date.OSDateFormatting` exposes the current OS/culture short-date mask in `Format`/`Format$` syntax
- [x] `Date.OSTimeFormatting` exposes the current OS/culture long-time mask in `Format`/`Format$` syntax
- [x] runtime regression verification via Language Extensions Compatibility
- [x] English documentation in `docs/date-time.md` with reusable `examples/` coverage for Adjust, Difference, comparisons and OS date/time formatting

## 8. Arrays and lists

- [x] typed dynamic arrays
- [x] fixed/multidimensional arrays and explicit bounds
- [x] `Array`, `ReDim`, `ReDim Preserve`, `Erase`, `LBound`, `UBound`
- [x] array helper functions and keyed lists
- [x] arrays as `Type` members including deep-copy of array storage
- [x] `Variant` values containing runtime XPScript arrays support indexed reads and writes; discovered by the `Base64DecodeBinary()` regression and covered by `samples/variant-runtime-array-indexing.xps`

## 9. File I/O and filesystem

- [x] standard file open/read/write/seek/reset operations
- [x] Charset-aware Input/Output/Append and independent Base64 encoding layer
- [x] file `Input$(count, #fileNumber)` distinct from interactive input; verified by `File IO Extensions Compatibility`
- [x] OS `Lock` / `Unlock` with Binary byte ranges, Random record ranges and sequential whole-file semantics; verified from a second operating-system file handle
- [x] standard filesystem operations
- [x] `ChDrive` on Windows; non-Windows behavior is explicit and cross-platform verified under section 14
- [x] explicit Latin-1 regression source; verified by `samples/file-io-extensions.xps`

File input and interactive input are distinct APIs. `Lock/Unlock` is regression-tested from a second operating-system process/handle on Windows, Linux and macOS; detailed completed portability evidence is archived under section 14's checklist.

## 10. Formatting, process and console

- [x] formatting functions
- [x] `Environ`, current `Shell`, `Sleep`
- [x] console Print/Input/Pause and message-box helpers

## 11. Base64 and URL

- [x] `ToBase64`, `FromBase64`, `UrlEncode`, `UrlDecode`
- [x] `Base64Encode`, `Base64Decode`
- [x] binary-return Base64 decode via `Base64DecodeBinary()` returning a normal XPScript Byte array; source: `samples/base64-binary.xps`

## 12. Native HTTP API

- [x] `XPHttpClient`
- [x] `Get`, `Post`, `Put`, `Patch`, `Delete`
- [x] `SetHeader`, `RemoveHeader`, `ClearHeaders`, `Timeout`
- [x] `XPHttpResponse.StatusCode`, `StatusText`, `Body`, `ContentType`, `Headers`, `IsSuccess`
- [x] end-to-end loopback regression: `samples/native-http-regression.xps`, `tests/native_http_server.py`; manual gate: `Native HTTP Compatibility`

## 13. Native JSON API

- [x] `XPJsonDocument.Parse`, `XPJsonDocument.Stringify`
- [x] `XPJsonObject.Get`, `Set`, `Remove`, `Contains`, `Count`
- [x] `XPJsonArray.Add`, `Get`, `Set`, `RemoveAt`, `Count`
- [x] `XPJsonElement.Type`, `XPJsonElement.Value`
- [x] `JsonParse`, `JsonStringify`, `JsonEncode`, `JsonDecode`
- [x] end-to-end regression: `samples/native-json-regression.xps`; manual gate: `Native JSON Compatibility`; implementation uses .NET `System.Text.Json`

## 14. Cross-platform compiler and runtime

- [x] publish-target support for Windows, Linux and macOS is runtime-verified
- [x] compiler runtime targets: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`
- [x] default target follows the compiler host OS and x64/arm64 process architecture
- [x] `Platform()` returns stable runtime names including `Windows`, `Linux`, `MacOS`
- [x] platform-specific branching using `Platform()`
- [x] cross-platform `Shell()` implementation
- [x] Windows: `.exe`, `.cmd`, `.bat`, `.ps1` and commands
- [x] Linux/macOS: binaries, executable/shebang scripts, `.sh`/`.bash`, and `.ps1` through `pwsh`
- [x] argument handling uses `ProcessStartInfo.ArgumentList` where possible to avoid unintended shell re-parsing
- [x] clear runtime errors for unsupported/unexecutable targets
- [x] target-selected native library declarations with `WindowsLib`, `LinuxLib`, `MacOSLib`
- [x] target-selected native entry points with `WindowsAlias`, `LinuxAlias`, `MacOSAlias`
- [x] multiline platform-specific `Declare` statements with `_`; source: `samples/platform-native-library.xps`
- [x] application-local native `.dll`, `.so`, `.dylib` paths are validated and copied beside generated output; system-library names remain OS-resolved; path escape, missing-file, output-name collision and executable-overwrite checks are implemented; source: `samples/native-dependency-packaging.xps`
- [x] architecture-specific native assets and aliases are selected by exact target RID for x64/arm64, with OS/base fallback; source: `samples/native-architecture-assets.xps`
- [x] managed .NET assembly references are separate from native `Declare`: `Reference "path.dll"` stages a project-local managed assembly and repeatable `ReferenceNative "path" Runtime "rid"` packages RID-specific native dependencies; detailed syntax/security rules: `todo/done/cross-platform-runtime-todo.md`
- [x] validate native-library search paths and loading behavior on Windows, Linux and macOS; verified with real system-library calls by `Cross Platform Native Loader Compatibility` across all six supported RIDs; detailed evidence: `todo/done/cross-platform-runtime-todo.md`
- [x] validate file-I/O portability across Windows, Linux and macOS including text round-trip, charset/BOM, Latin-1 byte identity, newline behavior, executable-bit preservation, roots/drives/UNC/long paths, symlinks, broader permissions, cross-filesystem behavior, sharing, rename/delete semantics and locking; detailed evidence: `todo/done/cross-platform-runtime-todo.md`
- [x] validate OS file locks and byte/record range locks separately on Windows, Linux and macOS, including cross-process overlap conflicts
- [x] keep `ChDrive` explicitly Windows-only and provide clear behavior/error semantics elsewhere
- [x] detailed portability checklist completed and archived: `todo/done/cross-platform-runtime-todo.md`

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

- [x] explicit memory semantics are defined: managed memory follows .NET GC after the last strong reference disappears; deterministic cleanup is required for owned OS/unmanaged resources
- [x] `Nothing` is an empty object-reference state and remains distinct from Variant `Null` and Variant `Empty`
- [x] `Set object = Nothing` clears only that reference; when no other strong reference remains the managed object becomes GC-eligible; covered by `samples/module-object-references.xps`
- [x] aliases remain alive when one reference is cleared and are invalidated together only when the shared reference is explicitly `Delete`d; cross-platform regression covered
- [x] `Delete` invokes `Sub Delete` and clears the shared reference cell; clearing one reference with `Set ... = Nothing` does not delete aliases; Delete clearing is exception-safe through `finally`
- [x] locals are ordinary CLR locals; generated ByRef wrappers are call-path objects without static caches and error state stores only scalar/string diagnostic data
- [x] module globals and Static values intentionally retain strong references for their declared lifetime; reassignment/clear releases the replaced reference unless another alias remains
- [x] dynamic/fixed `Erase`, `ReDim`, `ReDim Preserve`, List `Erase` and List `Clear` release removed backing references; validated by section-17 source audits
- [x] `Type`/UDT copies and array clones create replacement values without compiler-owned runtime alias caches; explicit object-reference fields retain normal reference semantics
- [x] HTTP/JSON lifetime reviewed: JSON wrappers use managed ownership without process-global object caches; HTTP request/response/network resources are disposed and response bodies are copied before return
- [x] deterministic cleanup reviewed for files, streams, HTTP/socket resources, locks, process wrappers and native interop; COM is not currently exposed by the standalone runtime and future COM support requires explicit ownership/release
- [x] `Close` disposes reader/writer/stream, `Unlock` releases byte-range locks, HTTP/process wrappers dispose owned OS resources, and Reset-style state APIs do not substitute for `Close` when a file handle is owned
- [x] class cleanup policy defined: `Sub Delete` is explicit deterministic language cleanup; XPScript does not synthesize CLR finalizers or automatically dispose arbitrary externally-owned CLR objects at scope exit
- [x] `IDisposable` / `IAsyncDisposable` ownership reviewed; runtime-owned disposable resources use deterministic/idempotent cleanup and externally supplied objects retain their documented owner
- [x] production compiler/runtime source is gated against `GC.Collect()` as normal language behavior
- [x] memory/lifetime regression and bounded stress gate executes object alias/Delete behavior 40 times and audits managed/OS-resource lifetime contracts on Windows, Ubuntu and macOS
- [x] `Nothing`, `Null`, `Delete`, `Erase`, scope, GC eligibility and unmanaged-resource cleanup are documented in `docs/memory-resource-lifetime.md`

Design direction: managed memory is reclaimed by .NET GC after the last strong reference disappears. `Nothing`/`Null` can make objects eligible for collection but do not mean immediate deallocation. OS/unmanaged resources require deterministic cleanup. Verified by `.github/workflows/memory-resource-lifetime.yml`.

## 18. Web runtime and server

- [ ] Implement only after the existing compiler/language/runtime backlog is complete and stable.
- [ ] Complete the architecture/security review before production implementation.
- [ ] Provide shared XPScript web runtime semantics for standalone Kestrel and FastCGI hosting.
- [ ] Detailed architecture, object model, runtime compilation/cache, routing, CGI, FastCGI and security checklist: `todo/web-runtime-server-todo.md`.
- [ ] Follow dependency-reuse rules in `todo/development-guidelines.md`; prefer ASP.NET Core/.NET and vetted maintained NuGet packages over custom low-level protocol/parser implementations where suitable.
- [ ] This section must be completed before the cross-platform UI extension work begins.

## 19. Cross-platform UI extension inventory

Design goal: add a small, platform-native UI extension for simple forms and dialogs on Windows, Linux and macOS. Keep the XPScript API stable across platforms while allowing the backend implementation to use the most appropriate UI toolkit for each operating system.

### 19.1 Core classes and data model

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

### 19.2 Form lifecycle and layout

- [ ] create form with title, optional width/height and optional resizable flag
- [ ] modal `ShowDialog()` returning a stable result such as `OK`, `Cancel`, `Yes`, `No`
- [ ] optionally support non-modal `Show()` later; modal dialogs are MVP
- [ ] close/cancel behavior consistent across Windows, Linux and macOS
- [ ] simple layout abstraction that avoids requiring pixel-perfect platform-specific coordinates

### 19.3 UI element inventory

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

### 19.4 Validation

- [ ] required
- [ ] min/max text length
- [ ] numeric min/max
- [ ] date min/max
- [ ] regular expression
- [ ] allowed values
- [ ] custom XPScript validation callback
- [ ] field-level validation errors and form-level validation before OK/submit

### 19.5 Dialog inventory

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

### 19.6 Data binding semantics

- [ ] form starts with an isolated working copy of `UIData`
- [ ] user edits update only the working copy while the dialog is open
- [ ] OK commits form values to returned/form Data
- [ ] Cancel discards working-copy changes unless explicitly configured otherwise
- [ ] values preserve scalar/multivalue XPScript types where possible

### 19.7 Cross-platform backend inventory

- [ ] investigate Windows backend
- [ ] investigate Linux backend such as GTK or equivalent
- [ ] investigate macOS backend
- [ ] evaluate whether one cross-platform .NET UI toolkit can provide consistent behavior without excessive runtime size
- [ ] prefer native file/message dialogs where practical
- [ ] define UI thread/event-loop integration
- [ ] detect headless/server environment and return clear runtime errors
- [ ] architecture-specific dependencies for x64/arm64
- [ ] package UI dependencies only when generated program actually uses the UI extension where feasible

### 19.8 UI security/lifetime

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
