# XPScript Evaluate multiple callvar parameters TODO

(c) xpagedeveloper.com 2026

Goal: extend `Evaluate` so one invocation can receive up to five explicit callvar parameters without weakening existing isolation, snapshot, budget or diagnostic guarantees.

Status:
- `[x]` implemented and verified
- `[>]` implemented/in progress, awaiting verification
- `[ ]` not implemented

## Public syntax and compatibility

- [x] keep existing `Evaluate(sourceText)` behavior backward compatible
- [x] keep existing `Evaluate(sourceText, callvar)` behavior backward compatible
- [ ] support `Evaluate(sourceText, callvar1, callvar2)`
- [ ] support `Evaluate(sourceText, callvar1, callvar2, callvar3)`
- [ ] support `Evaluate(sourceText, callvar1, callvar2, callvar3, callvar4)`
- [ ] support `Evaluate(sourceText, callvar1, callvar2, callvar3, callvar4, callvar5)`
- [ ] reject more than five callvar arguments with a clear source-mapped compiler diagnostic

## Names visible inside Evaluate

- [ ] `callvar1`, `callvar2`, `callvar3`, `callvar4` and `callvar5` always exist inside Evaluate
- [ ] an omitted callvar argument has the XPScript value `Null`
- [ ] if no callvar arguments are supplied, `callvar1` through `callvar5` are all `Null`
- [ ] with one supplied value, `callvar1` contains that value and `callvar2` through `callvar5` are `Null`
- [ ] keep `callvar` as a backward-compatible alias for `callvar1`
- [ ] all numbered callvars and the `callvar` alias are read-only root values inside Evaluate
- [ ] reject `Dim callvar`, `Dim callvar1`, `Dim callvar2`, `Dim callvar3`, `Dim callvar4` and `Dim callvar5` inside Evaluate
- [ ] reject assignments that attempt to replace any callvar root value
- [ ] `IsNull(callvarN)` returns True for an omitted parameter once final XPScript Null semantics are active

## Supported values

- [ ] each supplied callvar supports the same scalar and Variant-contained scalar types as the existing single `callvar`
- [ ] each supplied callvar supports XPScript arrays with rank, bounds and element type preserved
- [ ] each supplied callvar supports Lists and nested List/array graphs
- [ ] arbitrary mutable object types remain rejected
- [ ] mixed calls such as scalar + Array + List are supported in the same Evaluate invocation

## Snapshot and identity semantics

- [ ] all supplied non-Null callvar parameters are snapshotted before evaluated source begins execution
- [ ] omitted Null callvars do not create collection snapshots or consume collection budget
- [ ] use one snapshot identity map across all supplied input parameters so a shared child object passed through multiple callvars remains one shared object inside the evaluator snapshot
- [ ] caller-owned mutable values are never shared directly with evaluated code
- [ ] returned arrays/Lists remain detached from evaluator-owned storage
- [ ] caller values remain unchanged after Evaluate, including when evaluated code mutates nested values through dynamic indexing

## Resource budgets

- [ ] maximum nesting depth remains 64
- [ ] maximum element budget remains 100000
- [ ] maximum estimated payload remains 16 MiB / 16777216 bytes
- [ ] input element and payload budgets are aggregated across all five callvar slots in one Evaluate invocation
- [ ] five callvar slots must not multiply the configured input budget by five
- [ ] Null/omitted callvar slots consume no element or payload budget
- [ ] shared references reached through multiple callvars are budgeted once per snapshot object identity
- [ ] return snapshot uses its own existing return budget and does not reuse consumed input budget state
- [ ] exceeding aggregate input budget produces controlled XPScript error 5 without partially exposing input state

## Diagnostics and security

- [ ] no callvar value may be echoed in compiler structured output, Evaluate error text, stdout or stderr solely because multiple parameters are used
- [ ] wrong Evaluate argument counts report that the supported form is source text plus zero through five callvar arguments
- [ ] permission, overflow, divide-by-zero, type mismatch and generic Evaluate error mappings remain unchanged
- [ ] caller locals, module globals and Static values remain inaccessible unless explicitly supplied as a callvar argument
- [ ] concurrent Evaluate invocations with different callvar sets remain isolated

## Runtime and compiler implementation

- [ ] extend `XPScriptEvaluatePreprocessor` parsing/lowering for source text plus zero through five callvar arguments
- [ ] extend `XPScriptEvaluateRuntime` invocation state to expose five stable callvar slots
- [ ] initialize omitted slots to XPScript `Null`
- [ ] preserve `callvar` as an alias for slot 1 rather than creating a separate snapshot/value
- [ ] avoid static/global storage for callvar values
- [ ] reuse existing collection snapshot/runtime contracts instead of reflection-based bridges
- [ ] evaluate each caller argument exactly once and preserve left-to-right argument evaluation order

## Permanent regression coverage

- [ ] no-callvar regression: `callvar1` through `callvar5` are Null
- [ ] one-callvar regression: existing `callvar` and `callvar1` expose the same value, slots 2 through 5 are Null
- [ ] two-callvar regression using `callvar1` and `callvar2`
- [ ] five-callvar regression reading all five values
- [ ] mixed scalar + Array + List regression
- [ ] omitted trailing arguments are Null and safe to test with `IsNull`
- [ ] read-only negative regression for each numbered callvar root
- [ ] `Dim callvarN` negative regression
- [ ] aggregate budget regression across multiple callvars
- [ ] shared-reference identity regression where the same child collection is supplied through two different callvar slots
- [ ] concurrent multi-callvar isolation regression
- [ ] more-than-five-arguments negative compiler regression

## Documentation

- [ ] update `docs/evaluate.md` with the zero-to-five callvar syntax
- [ ] document that all five numbered slots always exist and omitted values are `Null`
- [ ] document that `callvar` is an alias for `callvar1`
- [ ] document that input budgets are shared across all callvar parameters in one invocation

## Example contract

```xpscript
result = Evaluate("Return callvar1 + callvar2", firstValue, secondValue)

result = Evaluate("Return IsNull(callvar5)", firstValue)

result = Evaluate("Return callvar", firstValue)
' callvar is the backward-compatible alias for callvar1
```
