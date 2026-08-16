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
- Variant `NULL` uses the private immutable `XPScriptNullRuntime` sentinel.
- Object `NOTHING` uses an `LSRef<T>` instance whose `Value` is null and whose `IsNothing` property is true.
- Object references therefore do not share the Variant EMPTY representation even though the wrapped object value is null.

Status:
- `[x]` implemented and verified
- `[>]` implemented/in progress, awaiting complete verification
- `[ ]` not implemented

## Runtime representation

- [x] Variant `EMPTY` has an explicit runtime contract: CLR `null` in Variant value storage, interpreted as EMPTY by Variant inspection semantics.
- [x] Variant `NULL` has a separate private immutable sentinel in `TypeCoercionRuntimeSource.cs`, verified on Windows, Ubuntu and macOS.
- [x] Object `NOTHING` is represented by `LSRef<T>.IsNothing`, not by the Variant value itself.
- [>] Ensure the NULL sentinel cannot leak as a normal user object across serialization/API boundaries; the runtime sentinel itself is private and immutable, while serialization/API review remains open.

## Variable initialization

- [x] Unassigned Variant locals use the EMPTY representation and are cross-platform verified by `samples/null-empty-semantics.xps`.
- [x] Variant module globals and Static values use the same EMPTY inspection semantics; cross-platform verified by `samples/variant-global-static-empty.xps` on Windows, Ubuntu and macOS.
- [x] Keep typed scalar defaults unchanged: numeric zero, Boolean false, String empty string and the existing Date default.
- [x] Class/object references initialize as empty `LSRef<T>` references with `IsNothing = true`.

## Literals and assignment

- [x] Compile `Null` to the Variant `NULL` sentinel in normal compiled expressions; string literals and comments are excluded from rewriting and the behavior is cross-platform verified.
- [x] `Set object = Nothing` clears the object reference through `LSRef<T>` semantics.
- [ ] Reject invalid assignment of `Nothing` to scalar/Variant value contexts where an object reference is required.
- [ ] Permit `Null` only where Variant-compatible semantics apply.

## Inspection functions

- [x] `IsEmpty(EMPTY)` returns true and is false for `NULL` in the normal runtime path.
- [x] `IsNull(NULL)` returns true and is false for `EMPTY` in the normal runtime path.
- [x] `DataType(EMPTY)` returns 0 through the Variant EMPTY representation.
- [x] `DataType(NULL)` returns 1 through `XPScriptNullRuntime.DataType`.
- [x] Object NOTHING remains an object-reference state through `LSRef<T>` rather than being reported as Variant NULL.
- [x] `TypeName(EMPTY)` returns `EMPTY`.
- [x] `TypeName(NULL)` returns `NULL`.
- [ ] Review `IsNumeric`, `IsScalar`, `IsObject`, `CVar` and related inspection/conversion helpers for NULL and EMPTY edge cases.

## Coercion and operators

- [x] EMPTY converts to zero in the existing numeric conversion paths used by forgiving arithmetic.
- [x] EMPTY converts to empty string through the existing `CStr(null)` path.
- [>] `NULL` propagates through forgiving Variant `+`; broader arithmetic/comparison/string propagation remains open.
- [>] `NULL` is not silently converted to EMPTY, zero, empty string or NOTHING in the verified Variant `+` path; broader coercion review remains open.
- [ ] Review Boolean conditions involving `NULL` and define bounded diagnostics where required.
- [>] Array/List snapshots containing EMPTY and NULL preserve both values across Evaluate; broader normal-runtime array/List mutation and conversion edge cases remain open.

## Object references

- [x] `Set object = Nothing` is reference clearing, not Variant NULL assignment; cross-platform covered by `samples/module-object-references.xps`.
- [x] Aliases remain valid when one reference is cleared; cross-platform covered by `samples/module-object-references.xps`.
- [x] Object-reference tests use `LSRef<T>.IsNothing` and object identity rather than Variant `IsNull`.
- [x] `Delete` and shared-reference cleanup are covered by `samples/module-object-references.xps`.

## Evaluate

- [x] EMPTY and NULL semantics are preserved across the `callvar` snapshot boundary for scalar, Array and List values; source: `samples/evaluate-callvar-null-empty.xps`.
- [x] `Return Null` returns the NULL sentinel instead of EMPTY; source: `samples/evaluate-null-empty-semantics.xps`.
- [x] `Return Nothing` is rejected in Evaluate value context and maps through the bounded XPScript error 5 diagnostic; source: `samples/evaluate-return-nothing-error.xps`.
- [x] Reaching the end without `Return` returns the Variant EMPTY representation.
- [x] Evaluate `IsEmpty`, `IsNull`, `DataType`, `TypeName` and Variant `+` use the same shared NULL/EMPTY runtime contract as normal execution.

## Serialization and APIs

- [ ] Define JSON behavior for EMPTY, NULL and NOTHING without accidentally leaking internal sentinel objects.
- [ ] Review HTTP, console/text formatting and file I/O conversion paths.
- [ ] Review managed/native interop conversion boundaries.

## Regression gate

- [x] Normal-runtime fixture covers initialization, NULL assignment, inspection and Variant `+` propagation: `samples/null-empty-semantics.xps`.
- [x] Module-global and Static Variant EMPTY initialization and Static persistence are covered by `samples/variant-global-static-empty.xps`.
- [x] Object-reference NOTHING, alias and Delete behavior are covered by `.github/workflows/null-empty-semantics.yml` using `samples/module-object-references.xps`.
- [x] Evaluate fixture covers no-return, `Return Null`, `Return Nothing` rejection and inspection parity.
- [x] Evaluate callvar fixture covers scalar, Array and List values containing EMPTY and NULL: `samples/evaluate-callvar-null-empty.xps`.
- [x] Focused Null/Empty/Nothing runtime and Evaluate gates execute on Windows, Ubuntu and macOS.
- [x] Corresponding completed Evaluate return/callvar semantics can now be closed in `todo/evaluate-callvar-todo.md`; remaining items in this file are independent normal-runtime/API edge cases.
