# XPScript Null, Empty and Nothing semantics

(c) xpagedeveloper.com 2026

This checklist defines the language contract required before the remaining Evaluate and memory/lifetime TODO items can be closed.

Compatibility direction follows the standalone language semantics:

- `EMPTY` is the initial value of an unassigned Variant.
- `NULL` is an explicit Variant value representing unknown or missing data.
- `NOTHING` is the unbound value of an object reference.
- These three language states must remain distinguishable at runtime.

Implementation model:

- Variant `EMPTY` uses CLR `null` in Variant value storage.
- Variant `NULL` uses the private `XPScriptNullRuntime` sentinel.
- Object `NOTHING` uses an `LSRef<T>` instance whose `Value` is null and whose `IsNothing` property is true.
- Object references therefore do not share the Variant EMPTY representation even though the wrapped object value is null.

Status:
- `[x]` implemented and verified
- `[>]` implemented/in progress, awaiting complete verification
- `[ ]` not implemented

## Runtime representation

- [x] Variant `EMPTY` has an explicit runtime contract: CLR `null` in Variant value storage, interpreted as EMPTY by Variant inspection semantics.
- [>] Variant `NULL` has a separate singleton sentinel in `TypeCoercionRuntimeSource.cs`; Windows and Ubuntu verification have passed and macOS verification is pending in the `Null Empty Semantics` gate.
- [x] Object `NOTHING` is represented by `LSRef<T>.IsNothing`, not by the Variant value itself.
- [>] Ensure the NULL sentinel cannot be confused with user objects; the sentinel type is private to `XPScriptNullRuntime`, serialization/API review remains open.

## Variable initialization

- [>] Unassigned Variant locals use the EMPTY representation and are covered by `samples/null-empty-semantics.xps`; full cross-platform verification is pending macOS.
- [ ] Verify Variant module globals and Static values use the same EMPTY inspection semantics.
- [x] Keep typed scalar defaults unchanged: numeric zero, Boolean false, String empty string and the existing Date default.
- [x] Class/object references initialize as empty `LSRef<T>` references with `IsNothing = true`.

## Literals and assignment

- [>] Compile `Null` to the Variant `NULL` sentinel in normal compiled expressions; string literals and comments are excluded from rewriting, awaiting complete cross-platform verification.
- [x] `Set object = Nothing` clears the object reference through `LSRef<T>` semantics.
- [ ] Reject invalid assignment of `Nothing` to scalar/Variant value contexts where an object reference is required.
- [ ] Permit `Null` only where Variant-compatible semantics apply.

## Inspection functions

- [>] `IsEmpty(EMPTY)` returns true and is false for `NULL` in the normal runtime path; complete cross-platform verification is pending macOS.
- [>] `IsNull(NULL)` returns true and is false for `EMPTY` in the normal runtime path; complete cross-platform verification is pending macOS.
- [>] `DataType(EMPTY)` returns 0 through the Variant EMPTY representation; complete cross-platform verification is pending macOS.
- [>] `DataType(NULL)` returns 1 through `XPScriptNullRuntime.DataType`; complete cross-platform verification is pending macOS.
- [>] Object NOTHING remains an object-reference state through `LSRef<T>` rather than being reported as Variant NULL; the extended object regression gate is pending.
- [>] `TypeName(EMPTY)` returns `EMPTY`; complete cross-platform verification is pending macOS.
- [>] `TypeName(NULL)` returns `NULL`; complete cross-platform verification is pending macOS.
- [ ] Review `IsNumeric`, `IsScalar`, `IsObject`, `CVar` and related inspection/conversion helpers for NULL and EMPTY edge cases.

## Coercion and operators

- [x] EMPTY converts to zero in the existing numeric conversion paths used by forgiving arithmetic.
- [x] EMPTY converts to empty string through the existing `CStr(null)` path.
- [>] Propagate `NULL` through forgiving Variant `+`; broader arithmetic/comparison/string propagation remains open.
- [>] Ensure `NULL` is not silently converted to EMPTY, zero, empty string or NOTHING in the newly covered Variant `+` path; broader coercion review remains open.
- [ ] Review Boolean conditions involving `NULL` and define bounded diagnostics where required.
- [ ] Review array/List elements containing EMPTY or NULL.

## Object references

- [>] Keep `Set object = Nothing` as reference clearing, not Variant NULL assignment; covered by `samples/module-object-references.xps`, extended cross-platform gate pending.
- [>] Verify aliases remain valid when one reference is cleared; covered by `samples/module-object-references.xps`, extended cross-platform gate pending.
- [x] Object-reference tests use `LSRef<T>.IsNothing` and object identity rather than Variant `IsNull`.
- [>] `Delete` and shared-reference cleanup are covered by `samples/module-object-references.xps`; extended cross-platform gate pending.

## Evaluate

- [ ] Preserve EMPTY and NULL semantics when crossing the `callvar` snapshot boundary.
- [ ] `Return Null` returns the NULL sentinel instead of EMPTY.
- [ ] Define and enforce `Return Nothing` for Evaluate result contexts.
- [x] Reaching the end without `Return` currently returns the Variant EMPTY representation.
- [ ] Align Evaluate `IsEmpty`, `IsNull`, `DataType`, `TypeName`, coercion and diagnostics with normal runtime behavior. The current evaluator still maps both `Null` and `Nothing` to CLR null and calls the legacy inspection helpers.

## Serialization and APIs

- [ ] Define JSON behavior for EMPTY, NULL and NOTHING without accidentally leaking internal sentinel objects.
- [ ] Review HTTP, console/text formatting and file I/O conversion paths.
- [ ] Review managed/native interop conversion boundaries.

## Regression gate

- [>] Normal-runtime fixture covers initialization, NULL assignment, inspection and Variant `+` propagation: `samples/null-empty-semantics.xps`; Windows and Ubuntu passed, macOS pending.
- [>] Object-reference NOTHING, alias and Delete behavior are now part of `.github/workflows/null-empty-semantics.yml` using `samples/module-object-references.xps`; verification pending.
- [ ] Add Evaluate fixture covering no-return, Return Null, callvar EMPTY/NULL and inspection parity.
- [ ] Add array/List fixture containing EMPTY and NULL.
- [>] Run build/runtime checks on Windows, Ubuntu and macOS through `.github/workflows/null-empty-semantics.yml`.
- [ ] Only after the relevant gates pass, close the corresponding Evaluate and memory/lifetime items in `todo/runtime-reference-todo.md` and `todo/evaluate-callvar-todo.md`.
