# XPScript Null, Empty and Nothing semantics

(c) xpagedeveloper.com 2026

This checklist defines the language contract required before the remaining Null/Empty/Nothing edge cases can be closed.

Compatibility direction follows the standalone language semantics:

- `EMPTY` is the initial value of an unassigned Variant.
- `NULL` is an explicit Variant value representing unknown or missing data.
- `NOTHING` is the unbound value of an object reference.
- These three language states must remain distinguishable at runtime.

Implementation model:

- Variant `EMPTY` uses CLR `null` in Variant value storage.
- Variant `NULL` uses the CLR-standard immutable `System.DBNull.Value` marker so managed interop can distinguish it from EMPTY without exposing a private XPScript sentinel type.
- Object `NOTHING` uses an `LSRef<T>` instance whose `Value` is null and whose `IsNothing` property is true.
- Object references therefore do not share the Variant EMPTY representation even though the wrapped object value is null.

Status:
- `[x]` implemented and verified
- `[>]` implemented/in progress, awaiting complete verification
- `[ ]` not implemented

## Runtime representation

- [x] Variant `EMPTY` has an explicit runtime contract: CLR `null` in Variant value storage, interpreted as EMPTY by Variant inspection semantics.
- [x] Variant `NULL` has a separate immutable CLR representation using `System.DBNull.Value` in `TypeCoercionRuntimeSource.cs`; XPScript inspection semantics still report NULL as scalar and not object.
- [x] Object `NOTHING` is represented by `LSRef<T>.IsNothing`, not by the Variant value itself.
- [x] NULL/EMPTY/NOTHING representations are prevented from leaking as internal implementation objects across JSON, console/text, text-file, HTTP, managed .NET and native scalar ABI boundaries.

## Variable initialization

- [x] Unassigned Variant locals use the EMPTY representation and are cross-platform verified by `samples/null-empty-semantics.xps`.
- [x] Variant module globals and Static values use the same EMPTY inspection semantics; cross-platform verified by `samples/variant-global-static-empty.xps` on Windows, Ubuntu and macOS.
- [x] Keep typed scalar defaults unchanged: numeric zero, Boolean false, String empty string and the existing Date default.
- [x] Class/object references initialize as empty `LSRef<T>` references with `IsNothing = true`.

## Literals and assignment

- [x] Compile `Null` to the Variant `NULL` representation in normal compiled expressions; string literals and comments are excluded from rewriting and the behavior is cross-platform verified.
- [x] `Set object = Nothing` clears the object reference through `LSRef<T>` semantics.
- [x] Reject invalid `Nothing` value assignment outside object-reference `Set`; `Variant = Nothing` is rejected by `SourceTypeValidator` instead of silently becoming EMPTY. Cross-platform fixture: `samples/nothing-variant-assignment-error.xps`.
- [x] Permit `Null` only for Variant-compatible direct assignments and parameters where the source type validator can resolve the target type; typed local and module-global scalar assignments are rejected with bounded XPScript diagnostics. Cross-platform fixtures: `samples/null-integer-assignment-error.xps`, `samples/null-module-global-assignment-error.xps`, `samples/null-variant-parameter.xps` and `samples/null-integer-parameter-error.xps`.

## Inspection functions

- [x] `IsEmpty(EMPTY)` returns true and is false for `NULL` in the normal runtime path.
- [x] `IsNull(NULL)` returns true and is false for `EMPTY` in the normal runtime path.
- [x] `DataType(EMPTY)` returns 0 through the Variant EMPTY representation.
- [x] `DataType(NULL)` returns 1 through `XPScriptNullRuntime.DataType`.
- [x] Object NOTHING remains an object-reference state through `LSRef<T>` rather than being reported as Variant NULL.
- [x] `TypeName(EMPTY)` returns `EMPTY`.
- [x] `TypeName(NULL)` returns `NULL`.
- [x] `IsNumeric`, `IsScalar`, `IsObject`, `IsDate`, `IsArray` and `CVar` preserve the NULL/EMPTY distinction for the verified edge cases; cross-platform source: `samples/null-empty-inspection-helpers.xps`.

## Coercion and operators

- [x] EMPTY converts to zero in the existing numeric conversion paths used by forgiving arithmetic.
- [x] EMPTY converts to empty string through the existing `CStr(null)` path.
- [>] `NULL` propagates through forgiving Variant `+`; broader arithmetic/comparison/string propagation remains open.
- [>] `NULL` is not silently converted to EMPTY, zero, empty string or NOTHING in the verified Variant `+` path; broader coercion review remains open.
- [x] Boolean conditions involving `NULL` and `EMPTY` use the shared `XPScriptNullRuntime.ConditionValue` contract: both evaluate as false in `If`/`ElseIf`/`While`/`Do`/`Loop` conditions, while invalid non-convertible values produce a bounded diagnostic. Cross-platform source: `samples/null-boolean-conditions.xps` and `.github/workflows/null-boolean-conditions.yml`.
- [>] Array/List snapshots containing EMPTY and NULL preserve both values across Evaluate; broader normal-runtime array/List mutation and conversion edge cases remain open.

## Object references

- [x] `Set object = Nothing` is reference clearing, not Variant NULL assignment; cross-platform covered by `samples/module-object-references.xps`.
- [x] Aliases remain valid when one reference is cleared; cross-platform covered by `samples/module-object-references.xps`.
- [x] Object-reference tests use `LSRef<T>.IsNothing` and object identity rather than Variant `IsNull`.
- [x] `Delete` and shared-reference cleanup are covered by `samples/module-object-references.xps`.
- [x] Object-reference wrappers cannot fall back to CLR type-name text conversion; implicit `LSRef<T>.ToString()` now raises a type mismatch instead of exposing implementation details.

## Evaluate

- [x] EMPTY and NULL semantics are preserved across the `callvar` snapshot boundary for scalar, Array and List values; source: `samples/evaluate-callvar-null-empty.xps`.
- [x] `Return Null` returns the NULL representation instead of EMPTY; source: `samples/evaluate-null-empty-semantics.xps`.
- [x] `Return Nothing` is rejected in Evaluate value context and maps through the bounded XPScript error 5 diagnostic; source: `samples/evaluate-return-nothing-error.xps`.
- [x] Reaching the end without `Return` returns the Variant EMPTY representation.
- [x] Evaluate `IsEmpty`, `IsNull`, `DataType`, `TypeName` and Variant `+` use the same shared NULL/EMPTY runtime contract as normal execution.

## Serialization and APIs

- [x] JSON behavior is defined and cross-platform verified: EMPTY, NULL and NOTHING serialize as JSON `null`; internal NULL/object-reference runtime representations do not leak. JSON `null` deserializes to Variant EMPTY because JSON cannot preserve the three-way XPScript distinction.
- [x] Console/text formatting is cross-platform verified: `CStr(EMPTY)` and `CStr(NULL)` both produce empty text and the NULL implementation marker does not leak.
- [x] Text file output preserves the distinction where the file format can represent it: `Print #` uses empty text for EMPTY/NULL, while `Write #` emits an empty field for EMPTY and `#NULL#` for NULL. Dynamic single-EMPTY `params` binding is handled safely.
- [x] HTTP request conversion is defined and cross-platform verified: EMPTY request body remains content-less, NULL becomes an explicit empty text body, EMPTY/NULL header values become empty text, EMPTY/NULL URLs are rejected by URL validation, and object references including NOTHING cannot leak CLR wrapper text. HTTP responses contain normal HTTP scalar/binary values and do not synthesize XPScript NULL/NOTHING states.
- [x] Managed .NET interop preserves the distinction: EMPTY crosses as CLR `null`, while NULL crosses as `System.DBNull.Value`; no private XPScript sentinel type is exposed.
- [x] Native scalar interop performs XPScript coercion before P/Invoke: EMPTY follows numeric coercion, NULL is rejected with bounded type mismatch semantics, and NOTHING/object references are rejected before a native scalar ABI value is produced.

## Regression gate

- [x] Normal-runtime fixture covers initialization, NULL assignment, inspection and Variant `+` propagation: `samples/null-empty-semantics.xps`.
- [x] Inspection/conversion helper fixture covers `IsNumeric`, `IsScalar`, `IsObject`, `IsDate`, `IsArray` and `CVar` for EMPTY and NULL: `samples/null-empty-inspection-helpers.xps`.
- [x] Invalid assignment fixtures verify `Nothing` cannot be used as a value and typed scalar targets reject `Null` before generated C# compilation: `samples/nothing-variant-assignment-error.xps`, `samples/null-integer-assignment-error.xps`, `samples/null-module-global-assignment-error.xps`.
- [x] Parameter fixtures verify `Null` is preserved for Variant parameters and rejected for typed scalar parameters: `samples/null-variant-parameter.xps` and `samples/null-integer-parameter-error.xps`.
- [x] Module-global and Static Variant EMPTY initialization and Static persistence are covered by `samples/variant-global-static-empty.xps`.
- [x] Object-reference NOTHING, alias and Delete behavior are covered by `.github/workflows/null-empty-semantics.yml` using `samples/module-object-references.xps`.
- [x] Boolean-condition semantics are covered on Windows, Ubuntu and macOS by `.github/workflows/null-boolean-conditions.yml`.
- [x] JSON EMPTY/NULL/NOTHING serialization boundaries are covered on Windows, Ubuntu and macOS by `.github/workflows/native-json-build.yml` and JSON security gates.
- [x] Console/text and text-file boundaries are covered on Windows, Ubuntu and macOS by `.github/workflows/null-text-boundaries.yml`, including internal representation non-leakage.
- [x] HTTP EMPTY/NULL/NOTHING request boundaries are covered on Windows, Ubuntu and macOS by `.github/workflows/native-http-build.yml` using `samples/native-http-null-boundaries.xps` and the local request-inspection server.
- [x] Managed NULL interop is covered on Windows, Ubuntu and macOS by `.github/workflows/managed-null-interop.yml` using the referenced fixture assembly.
- [x] Native EMPTY/NULL/NOTHING scalar ABI behavior is covered by `.github/workflows/native-scalar-abi.yml` across Windows x64/ARM64, Linux x64/ARM64 and macOS x64/ARM64.
- [x] Evaluate fixture covers no-return, `Return Null`, `Return Nothing` rejection and inspection parity.
- [x] Evaluate callvar fixture covers scalar, Array and List values containing EMPTY and NULL: `samples/evaluate-callvar-null-empty.xps`.
- [x] Focused Null/Empty/Nothing runtime and Evaluate gates execute on Windows, Ubuntu and macOS.
- [x] Corresponding completed Evaluate return/callvar semantics can now be closed in `todo/evaluate-callvar-todo.md`; remaining items in this file are independent normal-runtime edge cases.