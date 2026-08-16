# XPScript Null, Empty and Nothing semantics

(c) xpagedeveloper.com 2026

This checklist defines the language contract required before the remaining Evaluate and memory/lifetime TODO items can be closed.

Compatibility direction follows the standalone language semantics:

- `EMPTY` is the initial value of an unassigned Variant.
- `NULL` is an explicit Variant value representing unknown or missing data.
- `NOTHING` is the unbound value of an object reference.
- These three states must not share one internal representation.

## Runtime representation

- [ ] Add an internal singleton sentinel for Variant `EMPTY`.
- [ ] Add an internal singleton sentinel for Variant `NULL`.
- [ ] Keep CLR `null` for object-reference `NOTHING`.
- [ ] Ensure sentinels cannot be confused with user objects or serialized as normal objects.

## Variable initialization

- [ ] Initialize unassigned Variant locals to `EMPTY` rather than CLR `null`.
- [ ] Initialize Variant module globals and Static values to `EMPTY`.
- [ ] Keep typed scalar defaults unchanged: numeric zero, Boolean false, String empty string, Date default according to the XPScript contract.
- [ ] Keep class/object references initialized to `NOTHING`/CLR `null`.

## Literals and assignment

- [ ] Compile `Null` to the Variant `NULL` sentinel.
- [ ] Compile `Nothing` only as the object-reference empty value.
- [ ] Reject invalid assignment of `Nothing` to scalar/Variant value contexts where an object reference is required.
- [ ] Permit `Null` only where Variant-compatible semantics apply.

## Inspection functions

- [ ] `IsEmpty(EMPTY)` returns true and is false for `NULL` and `NOTHING` object references.
- [ ] `IsNull(NULL)` returns true and is false for `EMPTY`.
- [ ] `DataType(EMPTY)` returns 0.
- [ ] `DataType(NULL)` returns 1.
- [ ] `DataType(NOTHING/object reference)` follows object-reference semantics.
- [ ] `TypeName(EMPTY)` returns `EMPTY`.
- [ ] `TypeName(NULL)` returns `NULL`.
- [ ] Review `IsNumeric`, `IsScalar`, `IsObject`, `CVar` and related inspection/conversion helpers.

## Coercion and operators

- [ ] Convert `EMPTY` to zero in numeric operations.
- [ ] Convert `EMPTY` to empty string in string operations.
- [ ] Propagate `NULL` through compatible arithmetic/comparison/string expressions according to the language contract.
- [ ] Ensure `NULL` is not silently converted to EMPTY, zero, empty string or NOTHING.
- [ ] Review Boolean conditions involving `NULL` and define bounded diagnostics where required.
- [ ] Review array/List elements containing EMPTY or NULL.

## Object references

- [ ] Keep `Set object = Nothing` as reference clearing, not Variant NULL assignment.
- [ ] Verify aliases remain valid when one reference is cleared.
- [ ] Verify object-reference tests use object identity/NOTHING semantics rather than `IsNull`.
- [ ] Review `Delete` and class cleanup interaction separately from reference clearing.

## Evaluate

- [ ] Preserve EMPTY and NULL sentinels when crossing the `callvar` snapshot boundary.
- [ ] `Return Null` returns NULL.
- [ ] `Return Nothing` follows the final object-reference/result contract.
- [ ] Reaching the end without `Return` returns EMPTY.
- [ ] Align Evaluate `IsEmpty`, `IsNull`, `DataType`, `TypeName`, coercion and diagnostics with normal runtime behavior.

## Serialization and APIs

- [ ] Define JSON behavior for EMPTY, NULL and NOTHING without accidentally leaking internal sentinel objects.
- [ ] Review HTTP, console/text formatting and file I/O conversion paths.
- [ ] Review managed/native interop conversion boundaries.

## Regression gate

- [ ] Add focused normal-runtime fixture covering initialization, assignment, inspection and coercion.
- [ ] Add object-reference NOTHING fixture.
- [ ] Add Evaluate fixture covering no-return, Return Null, callvar EMPTY/NULL and inspection parity.
- [ ] Add array/List fixture containing EMPTY and NULL.
- [ ] Run build/runtime checks on Windows, Ubuntu and macOS.
- [ ] Only after the cross-platform gate passes, close the corresponding Evaluate and memory/lifetime items in `todo/runtime-reference-todo.md` and `todo/evaluate-callvar-todo.md`.
