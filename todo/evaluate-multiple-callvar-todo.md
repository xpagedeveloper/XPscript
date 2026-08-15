# XPScript Evaluate callvar array TODO

(c) xpagedeveloper.com 2026

Goal: allow one Evaluate invocation to receive multiple explicit caller values while exposing them through one isolated `callvar` array instead of separate `callvar2`, `callvar3`, etc. identifiers.

Status:
- `[x]` implemented and verified
- `[>]` implemented/in progress, awaiting verification
- `[ ]` not implemented

## Public syntax and compatibility

- [x] keep existing `Evaluate(sourceText)` behavior backward compatible
- [x] keep existing `Evaluate(sourceText, callvar)` behavior backward compatible
- [x] support `Evaluate(sourceText, value1, value2)` and expose values as `callvar(0)` and `callvar(1)`
- [>] support any supplied-value count that fits the normal Evaluate array snapshot budgets
- [x] do not impose a separate fixed callvar value-count limit

## Callvar array contract

- [x] when two or more caller values are supplied, `callvar` is a zero-based array
- [x] preserve caller argument order exactly
- [x] `LBound(callvar)` returns 0 for the generated multi-value array
- [x] `UBound(callvar)` returns supplied-value-count minus one
- [x] no public `callvar1`, `callvar2`, `callvar3`, `callvar4` or `callvar5` identifiers are introduced
- [x] `callvar` remains read-only as the Evaluate root value
- [x] `Dim callvar` remains rejected inside Evaluate

## Supported values

- [x] array elements support the same scalar and Variant-contained scalar types as existing callvar values
- [x] an element may itself be an XPScript Array
- [x] an element may itself be a List or nested List/Array graph
- [x] arbitrary mutable object types remain rejected by the existing Evaluate collection snapshot contract
- [x] mixed scalar + Array + List values are supported in one multi-value Evaluate invocation

## Snapshot and identity semantics

- [x] the generated callvar array is snapshotted before evaluated source begins execution
- [x] nested caller-owned arrays and Lists are never shared directly with evaluated code
- [x] shared child references reachable from two different supplied values preserve identity inside the snapshot; verified by `samples/evaluate-callvar-array.xps`
- [x] returned arrays/Lists remain detached through the existing return snapshot path

## Resource budgets

- [x] maximum nesting depth remains 64
- [x] maximum element budget remains 100000
- [x] maximum estimated payload remains 16 MiB / 16777216 bytes
- [x] all elements of the generated callvar array share the existing single input snapshot budget
- [ ] aggregate budget regression across multiple supplied values
- [x] return snapshot keeps its own existing return budget

The multi-value callvar array has no separate fixed value-count limit. Its effective capacity is constrained by the existing Evaluate input snapshot limits above.

## Diagnostics and security

- [x] existing Evaluate sanitization remains unchanged
- [x] permission, overflow, divide-by-zero, type mismatch and generic Evaluate error mappings remain unchanged
- [x] caller locals, module globals and Static values remain inaccessible unless explicitly supplied
- [ ] concurrent multi-value Evaluate invocations remain isolated

## Runtime and compiler implementation

- [>] use one variadic runtime overload for source text plus two or more supplied values
- [>] pack supplied values into one zero-based `LSArray` in caller order
- [x] reuse the existing Evaluate collection snapshot/runtime contract
- [x] avoid static/global storage for supplied values
- [x] normal C# argument evaluation preserves left-to-right evaluation and evaluates each supplied expression once

## Permanent regression coverage

- [x] two-value regression using `callvar(0)` and `callvar(1)`
- [x] five-value regression using `callvar(0)` through `callvar(4)`
- [>] more-than-five-values positive regression
- [x] mixed scalar + Array + List regression
- [x] callvar array bounds regression
- [ ] nested mutation isolation regression
- [ ] aggregate budget regression
- [x] shared-reference identity regression across two array elements; `MULTI-SHARED-REFERENCE-IDENTITY=OK`
- [ ] concurrent multi-value isolation regression
- [>] Windows, Linux and macOS regression coverage

## Documentation

- [ ] update `docs/evaluate.md` with the multi-value callvar-array syntax
- [ ] document that multiple supplied values become a zero-based `callvar` array
- [ ] document that existing one-value `Evaluate(sourceText, callvar)` behavior is unchanged
- [ ] document that input budgets apply to the complete generated callvar array and all nested values
- [ ] document that there is no separate fixed value-count limit

## Example contract

```xpscript
result = Evaluate("Return callvar(0) + callvar(1)", firstValue, secondValue)

result = Evaluate("Return callvar(0) + callvar(7)", value1, value2, value3, value4, value5, value6, value7, value8)
```

For two or more supplied caller values, `callvar` is the only input identifier and contains those values as a zero-based array. Existing single-callvar calls keep their current behavior. The array is constrained by the normal Evaluate snapshot depth, element-count and payload-size budgets rather than a separate fixed argument-count limit.
