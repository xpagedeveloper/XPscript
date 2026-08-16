# XPScript Evaluate callvar array TODO

(c) xpagedeveloper.com 2026

Goal: allow one Evaluate invocation to receive multiple explicit caller values through one `callvar` array instead of separate `callvar2`, `callvar3`, etc. identifiers.

Status: completed and verified.

## Public syntax and compatibility

- [x] `Evaluate(sourceText)` remains backward compatible
- [x] one-value `Evaluate(sourceText, callvar)` remains backward compatible
- [x] two or more supplied values are exposed as `callvar(0)`, `callvar(1)`, etc.
- [x] supplied-value count has no separate fixed limit
- [x] effective ByVal capacity is constrained by normal Evaluate snapshot budgets

## Parameter semantics

- [x] supplied variables are ByRef by default
- [x] explicit `ByVal` creates an isolated copy
- [x] multi-value calls may mix ByRef and ByVal arguments
- [x] `Dim callvar` remains rejected inside Evaluate

## Callvar array contract

- [x] two or more supplied values produce a zero-based `LSArray`
- [x] caller argument order is preserved exactly
- [x] `LBound(callvar)` returns 0
- [x] `UBound(callvar)` returns supplied-value-count minus one
- [x] no public `callvar1`, `callvar2`, `callvar3`, `callvar4` or `callvar5` identifiers exist

## Supported values and isolation

- [x] scalar and Variant-contained scalar values are supported
- [x] nested XPScript Array and List graphs are supported
- [x] mixed scalar + Array + List values are supported
- [x] ByVal nested collections are detached from caller-owned state
- [x] shared child references across ByVal values preserve identity inside the shared snapshot
- [x] returned arrays/Lists are detached through the return snapshot path
- [x] arbitrary unsupported mutable object types remain rejected for ByVal snapshots

## Resource budgets

- [x] maximum nesting depth is 64
- [x] maximum element budget is 100000
- [x] maximum estimated payload is 16 MiB / 16777216 bytes
- [x] all ByVal values in one multi-value invocation share one aggregate input snapshot budget
- [x] aggregate multi-value budget boundary has permanent regression coverage
- [x] return snapshot retains its own return budget

## Diagnostics and security

- [x] existing Evaluate sanitization is preserved
- [x] permission, overflow, divide-by-zero, type mismatch and generic error mappings are preserved
- [x] caller locals, module globals and Static values remain inaccessible unless explicitly supplied
- [x] concurrent multi-value Evaluate invocations remain isolated

## Runtime and compiler implementation

- [x] variadic runtime path supports two or more supplied values
- [x] supplied values are packed into one zero-based `LSArray`
- [x] ByVal values share one snapshot identity map and one input budget
- [x] no static/global storage is used for supplied values
- [x] caller expressions retain left-to-right evaluation and exactly-once evaluation

## Permanent regression coverage

- [x] two-value regression
- [x] five-value regression
- [x] more-than-five-values positive regression
- [x] mixed scalar + Array + List regression
- [x] array bounds regression
- [x] nested ByVal mutation isolation regression
- [x] aggregate budget regression
- [x] shared-reference identity regression
- [x] concurrent multi-value isolation regression
- [x] Windows, Linux and macOS coverage

## Documentation

- [x] `docs/evaluate.md` documents multi-value syntax
- [x] multiple supplied values are documented as one zero-based `callvar` array
- [x] one-value compatibility is documented
- [x] ByRef default and explicit ByVal copy semantics are documented
- [x] aggregate snapshot budgets are documented
- [x] absence of a separate fixed supplied-value limit is documented

## Example contract

```xpscript
result = Evaluate("Return callvar(0) + callvar(1)", firstValue, secondValue)

result = Evaluate("Return callvar(0) + callvar(7)", value1, value2, value3, value4, value5, value6, value7, value8)
```

For two or more supplied caller values, `callvar` is the only public input identifier and contains those values in a zero-based array. ByRef is the default parameter mode. Explicit `ByVal` copies the selected input using the bounded Evaluate snapshot contract.
