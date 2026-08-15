# XPScript Documentation Index


## Core language

See [docs/core-language.md](core-language.md).

Covered samples:

- [samples/hello.xps](../samples/hello.xps)
- [samples/functions.xps](../samples/functions.xps)
- [samples/core-language.xps](../samples/core-language.xps)
- [samples/language-extensions.xps](../samples/language-extensions.xps)
- [samples/compiler-errors.xps](../samples/compiler-errors.xps)
- [samples/erl-physical-source-line.xps](../samples/erl-physical-source-line.xps)
- [samples/nested-resume-targets.xps](../samples/nested-resume-targets.xps)
- [samples/reserved-identifier-error.xps](../samples/reserved-identifier-error.xps)
- [samples/reserved-runtime-type-error.xps](../samples/reserved-runtime-type-error.xps)
- [samples/reserved-multiple-identifiers-error.xps](../samples/reserved-multiple-identifiers-error.xps)
- [samples/reserved-module-multiple-identifiers-error.xps](../samples/reserved-module-multiple-identifiers-error.xps)

Main subjects:

- `Option Declare`, `Option Base`, `DefInt`
- scalar declarations
- Sub, Function, Call
- ByRef / ByVal
- Optional parameters
- Static state
- If / Select Case
- GoTo / GoSub / Return
- On Error / Resume / Err / Error / Erl
- With
- native Declare basics

## Arrays, Lists and operators

See [docs/arrays-lists-operators.md](arrays-lists-operators.md).

Covered samples:

- [samples/operators-arrays.xps](../samples/operators-arrays.xps)
- [samples/lists-classes.xps](../samples/lists-classes.xps)
- [samples/module-arrays.xps](../samples/module-arrays.xps)
- [samples/evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps)
- [samples/evaluate-nested-collections.xps](../samples/evaluate-nested-collections.xps)

Main subjects:

- fixed/dynamic/multidimensional arrays
- ReDim / ReDim Preserve / Erase
- LBound / UBound
- Array / Join / Explode
- ArrayAppend / ArrayGetIndex / ArrayUnique / ArraySlice / ArraySplice
- Lists, IsElement, ForAll, ListTag
- Like
- arithmetic/logical/bitwise operators
- Option Compare NoCase

## Types, Classes, properties and module state

See [docs/types-classes-modules.md](types-classes-modules.md).

Covered samples:

- [samples/language-extensions.xps](../samples/language-extensions.xps)
- [samples/lists-classes.xps](../samples/lists-classes.xps)
- [samples/indexed-properties.xps](../samples/indexed-properties.xps)
- [samples/indexed-properties-error.xps](../samples/indexed-properties-error.xps)
- [samples/module-globals.xps](../samples/module-globals.xps)
- [samples/module-arrays.xps](../samples/module-arrays.xps)
- [samples/module-type-values.xps](../samples/module-type-values.xps)
- [samples/module-object-references.xps](../samples/module-object-references.xps)
- [samples/type-value-copy.xps](../samples/type-value-copy.xps)
- [samples/type-array-members.xps](../samples/type-array-members.xps)
- [samples/type-array-option-base.xps](../samples/type-array-option-base.xps)
- [samples/type-nested-value-copy.xps](../samples/type-nested-value-copy.xps)
- [samples/module-nested-type-value-copy.xps](../samples/module-nested-type-value-copy.xps)
- [samples/type-cycle-error.xps](../samples/type-cycle-error.xps)

Main subjects:

- Enum
- Type / nested Type / Type arrays
- Class / New / Set / Me / Delete / Nothing / Is
- Property Get / Let / Set
- indexed properties
- module globals, arrays, Type values and object references

## String, conversion, inspection and Base64 functions

See [docs/strings-conversion-base64.md](strings-conversion-base64.md).

Covered samples:

- [samples/reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps)
- [samples/coercion.xps](../samples/coercion.xps)
- [samples/compatibility.xps](../samples/compatibility.xps)
- [samples/base64-binary.xps](../samples/base64-binary.xps)
- [samples/language-extensions.xps](../samples/language-extensions.xps)
- [samples/evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps)
- [samples/evaluate-coercion-diagnostics.xps](../samples/evaluate-coercion-diagnostics.xps)

Main subjects:

- CStr / CByte / CInt / CLng / CSng / CDbl / CCur / CBool / CDate / CVar / CType / CVDate
- DataType / TypeName / IsArray / IsDate / IsNull / IsNumeric / IsObject / IsScalar / IsList / IsUnknown
- Len / LenB
- Left / Right / Mid / Instr / Replace
- InstrB / LeftB / RightB / MidB
- StrLeft / StrLeftBack / StrRight / StrRightBack / StrToken
- LSet / RSet / UChr / Uni / StrConv
- Val / Str / Bin / Hex / Oct
- Base64Encode / Base64Decode / Base64DecodeBinary / ToBase64 / FromBase64
- UrlEncode / UrlDecode

## Math functions

See [docs/math-functions.md](math-functions.md).

Covered samples:

- [samples/compatibility.xps](../samples/compatibility.xps)
- [samples/operators-arrays.xps](../samples/operators-arrays.xps)
- [samples/evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps)
- [samples/evaluate-coercion-diagnostics.xps](../samples/evaluate-coercion-diagnostics.xps)

Main subjects:

- Abs / Int / Fix / Round / Sqr / Sgn
- Sin / Cos / Tan / ATn / ATn2 / ASin / ACos
- Exp / Log / Fraction
- Rnd / Randomize
- arithmetic operators, integer division, Mod and exponentiation

## Date and time

See [docs/date-time.md](date-time.md).

Covered samples:

- [samples/date-object-enhancements.xps](../samples/date-object-enhancements.xps)
- [samples/date-comparisons-valid.xps](../samples/date-comparisons-valid.xps)
- [samples/date-comparisons-invalid.xps](../samples/date-comparisons-invalid.xps)
- [samples/reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps)
- [samples/evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps)

Main subjects:

- CDate / CDat / CVDate
- DateNumber / TimeNumber
- DateValue / TimeValue
- Year / Month / Day / Hour / Minute / Second
- DateAdd / DateDiff / DatePart
- Date.Adjust
- Date.Difference
- Date comparisons and diagnostics

## File I/O and filesystem

See [docs/file-io-filesystem.md](file-io-filesystem.md) and [docs/text-io-console.md](text-io-console.md).

Covered samples:

- [samples/file-io-extensions.xps](../samples/file-io-extensions.xps)
- [samples/file-io-portability.xps](../samples/file-io-portability.xps)
- [samples/filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps)
- [samples/file-charset-bom.xps](../samples/file-charset-bom.xps)
- [samples/file-delete-open-semantics.xps](../samples/file-delete-open-semantics.xps)
- [samples/file-lock-holder.xps](../samples/file-lock-holder.xps)
- [samples/file-lock-contender.xps](../samples/file-lock-contender.xps)
- [samples/textio-console.xps](../samples/textio-console.xps)
- file portions of [samples/core-language.xps](../samples/core-language.xps)

Main subjects:

- FreeFile / Open / Close
- Input / Output / Append / Binary / Random
- Charset / Encoding
- Print # / Line Input # / file Input$
- Put / Get / Loc
- Lock / Unlock
- Kill / FileCopy / Name / FileLen / FileDateTime
- GetFileAttr / SetFileAttr
- MkDir / RmDir / ChDir / CurDir / ChDrive / Dir
- cross-platform path, newline, encoding and filesystem semantics

## Console, process, environment and formatting

See [docs/console-process-formatting.md](console-process-formatting.md).

Covered samples:

- [samples/textio-console.xps](../samples/textio-console.xps)
- [samples/platform-shell.xps](../samples/platform-shell.xps)
- standalone runtime-function portions of [samples/runtime-sax.xps](../samples/runtime-sax.xps)
- [samples/file-io-extensions.xps](../samples/file-io-extensions.xps)

Main subjects:

- Print / Print$
- Input / Input$ / Pause
- Command / Environ
- Sleep / Shell
- MessageBox / InputBox / Beep / Stop
- Format / Format$ / FormatNumber / FormatPercent
- Error / Error$

## Platform, Shell and native libraries

See [docs/platform-native.md](platform-native.md).

Covered samples:

- [samples/platform-shell.xps](../samples/platform-shell.xps)
- [samples/platform-native-library.xps](../samples/platform-native-library.xps)
- [samples/native-architecture-assets.xps](../samples/native-architecture-assets.xps)
- [samples/native-dependency-packaging.xps](../samples/native-dependency-packaging.xps)
- [samples/native-loader-diagnostics.xps](../samples/native-loader-diagnostics.xps)

Main subjects:

- Platform()
- cross-platform Shell
- Declare Function / Declare Sub
- WindowsLib / LinuxLib / MacOSLib
- WindowsAlias / LinuxAlias / MacOSAlias
- x64/arm64 native Lib/Alias selectors
- local native dependency packaging
- native loader diagnostics
- Reference / ReferenceNative

## Native HTTP and JSON

See [docs/native-http-json.md](native-http-json.md).

Covered samples:

- [samples/native-http-json.xps](../samples/native-http-json.xps)
- [samples/native-http-header-validation.xps](../samples/native-http-header-validation.xps)

Main subjects:

- HttpClient / HttpResponse
- Timeout / headers
- HTTP header validation and CR/LF injection rejection
- absolute http/https URL validation
- GET / POST / PUT / PATCH / DELETE
- JsonDocument
- JsonObject
- JsonArray
- JsonElement
- JsonParse / JsonStringify / JsonEncode / JsonDecode

[samples/json-http.xps](../samples/json-http.xps) is an older compatibility fixture and is not the preferred API for new standalone XPScript programs. See [docs/http-json-compatibility.md](http-json-compatibility.md) only when maintaining compatibility code.

## Evaluate

See [docs/evaluate.md](evaluate.md).

Covered samples:

- [samples/evaluate-xpscript.xps](../samples/evaluate-xpscript.xps)
- [samples/evaluate-callvar.xps](../samples/evaluate-callvar.xps)
- [samples/evaluate-array-helpers.xps](../samples/evaluate-array-helpers.xps)
- [samples/evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps)
- [samples/evaluate-nested-collections.xps](../samples/evaluate-nested-collections.xps)
- [samples/evaluate-no-return.xps](../samples/evaluate-no-return.xps)
- [samples/evaluate-callvar-readonly-error.xps](../samples/evaluate-callvar-readonly-error.xps)
- [samples/evaluate-scope-error.xps](../samples/evaluate-scope-error.xps)
- [samples/evaluate-coercion-diagnostics.xps](../samples/evaluate-coercion-diagnostics.xps)
- [samples/evaluate-function-arity-errors.xps](../samples/evaluate-function-arity-errors.xps)
- [samples/evaluate-collection-element-budget.xps](../samples/evaluate-collection-element-budget.xps)
- [samples/evaluate-collection-payload-budget.xps](../samples/evaluate-collection-payload-budget.xps)
- [samples/evaluate-diagnostic-sanitization.xps](../samples/evaluate-diagnostic-sanitization.xps)

Main subjects:

- Evaluate(sourceText)
- Evaluate(sourceText, callvar)
- explicit Return
- scalar/array/List callvar
- isolation and snapshots
- supported functions
- resource budgets
- coercion/error parity
- sanitized diagnostics

## Security

See [docs/security.md](security.md) and the implementation checklist in `todo/security-review-todo.md`.

Relevant samples include:

- [samples/reserved-identifier-error.xps](../samples/reserved-identifier-error.xps)
- [samples/reserved-runtime-type-error.xps](../samples/reserved-runtime-type-error.xps)
- [samples/reserved-multiple-identifiers-error.xps](../samples/reserved-multiple-identifiers-error.xps)
- [samples/reserved-module-multiple-identifiers-error.xps](../samples/reserved-module-multiple-identifiers-error.xps)
- [samples/native-http-header-validation.xps](../samples/native-http-header-validation.xps)
- [samples/evaluate-diagnostic-sanitization.xps](../samples/evaluate-diagnostic-sanitization.xps)
- [samples/file-lock-holder.xps](../samples/file-lock-holder.xps)
- [samples/file-lock-contender.xps](../samples/file-lock-contender.xps)
- [samples/native-loader-diagnostics.xps](../samples/native-loader-diagnostics.xps)

Main subjects:

- compiler-owned identifiers and runtime type names
- compiler temp isolation
- project-local dependency boundaries
- Shell/process trust boundaries
- filesystem authority
- Evaluate isolation and non-sandbox semantics
- HTTP header/URL validation and SSRF boundary
- native-code trust boundary
- diagnostic secret exposure

## Compatibility and older fixtures

The repository still contains samples used for compatibility/regression work, including:

- [samples/compatibility.xps](../samples/compatibility.xps)
- [samples/json-http.xps](../samples/json-http.xps)
- [samples/runtime-sax.xps](../samples/runtime-sax.xps)

These may contain older compatibility names or behavior that should not automatically be presented as the preferred standalone XPScript API. New documentation should favor XPScript-native APIs and branding.

## Fixture policy

`samples/` is the source-fixture directory. Documentation examples should reuse syntax already represented there whenever possible. Negative samples intentionally demonstrate compiler/runtime errors and must not be copied as successful usage examples without explaining the expected failure.
