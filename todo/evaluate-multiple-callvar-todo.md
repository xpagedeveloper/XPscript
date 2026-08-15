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
- [>] support `Evaluate(sourceText, value1, value2)` and expose values as `callvar(0)` and `callvar(1)`
- [>] support three, four and five supplied values as `callvar(0)` through `callvar(4)`
- [ ] reject more than five supplied callvar values with a clear source-mapped compiler diagnostic

## Callvar array contract

- [>] when two or more caller values are supplied, `callvar` is a zero-based array
- [>] preserve caller argument order exactly
- [>] `LBound(callvar)` returns 0 for the generated multi-value array
- [>] `UBound(callvar)` returns supplied-value-count minus one
- [x] no public `callvar1`, `callvar2`, `callvar3`, `callvar4` or `callvar5` identifiers are introduced
- [x] `callvar` remains read-only as the Evaluate root value
- [x] `Dim callvar` remains rejected inside Evaluate

## Supported values

- [>] array elements support the same scalar and Variant-contained scalar types as existing callvar values
- [>] an element may itself be an XPScript Array
- [>] an element may itself be a List or nested List/Array graph
- [x] arbitrary mutable object types remain rejected by the existing Evaluate collection snapshot contract
- [>] mixed scalar + Array + List values are supported in one multi-value Evaluate invocation

## Snapshot and identity semantics

- [>] the generated callvar array is snapshotted before evaluated source begins execution
- [>] nested caller-owned arrays and Lists are never shared directly with evaluated code
- [ ] shared child references reachable from two different supplied values preserve identity inside the snapshot
- [x] returned arrays/Lists remain detached through the existing return snapshot path

## Resource budgets

- [x] maximum nesting depth remains 64
- [x] maximum element budget remains 100000
- [x] maximum estimated payload remains 16 MiB / 16777216 bytes
- [>] all elements of the generated callvar array share the existing single input snapshot budget
- [ ] aggregate budget regression across multiple supplied values
- [x] return snapshot keeps its own existing return budget

## Diagnostics and security

- [x] existing Evaluate sanitization remains unchanged
- [x] permission, overflow, divide-by-zero, type mismatch and generic Evaluate error mappings remain unchanged
- [x] caller locals, module globals and Static values remain inaccessible unless explicitly supplied
- [ ] concurrent multi-value Evaluate invocations remain isolated
- [ ] wrong Evaluate argument counts report the supported form as source text plus zero through five supplied values

## Runtime and compiler implementation

- [>] add runtime overloads for source text plus two through five supplied values
- [>] pack two through five supplied values into one `object?[]` callvar value in caller order
- [x] reuse the existing Evaluate collection snapshot/runtime contract
- [x] avoid static/global storage for supplied values
- [>] normal C# argument evaluation preserves left-to-right evaluation and evaluates each supplied expression once

## Permanent regression coverage

- [>] two-value regression using `callvar(0)` and `callvar(1)`
- [>] five-value regression using `callvar(0)` through `callvar(4)`
- [>] mixed scalar + Array + List regression
- [>] callvar array bounds regression
- [ ] nested mutation isolation regression
- [ ] aggregate budget regression
- [ ] shared-reference identity regression across two array elements
- [ ] concurrent multi-value isolation regression
- [ ] more-than-five-values negative compiler regression
- [>] Windows, Linux and macOS regression coverage

## Documentation

- [ ] update `docs/evaluate.md` with the multi-value callvar-array syntax
- [ ] document that multiple supplied values become a zero-based `callvar` array
- [ ] document that existing one-value `Evaluate(sourceText, callvar)` behavior is unchanged
- [ ] document that input budgets apply to the complete generated callvar array and all nested values

## Example contract

```xpscript
result = Evaluate("Return callvar(0) + callvar(1)", firstValue, secondValue)

result = Evaluate("Return callvar(0) + callvar(1) + callvar(2)", firstValue, secondValue, thirdValue)
```

For two through five supplied caller values, `callvar` is the only input identifier and contains those values as a zero-based array. Existing single-callvar calls keep their current behavior.
