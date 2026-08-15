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
- [ ] support `Evaluate(sourceText, callvar, callvar2)`
- [ ] support `Evaluate(sourceText, callvar, callvar2, callvar3)`
- [ ] support `Evaluate(sourceText, callvar, callvar2, callvar3, callvar4)`
- [ ] support `Evaluate(sourceText, callvar, callvar2, callvar3, callvar4, callvar5)`
- [ ] reject more than five callvar arguments with a clear source-mapped compiler diagnostic

## Names visible inside Evaluate

- [ ] the first supplied slot is named exactly `callvar`; there is no public `callvar1` identifier
- [ ] additional slots are named `callvar2`, `callvar3`, `callvar4` and `callvar5`
- [ ] `callvar`, `callvar2`, `callvar3`, `callvar4` and `callvar5` always exist inside Evaluate
- [ ] an omitted callvar argument has the XPScript value `Null`
- [ ] if no callvar arguments are supplied, `callvar` and `callvar2` through `callvar5` are all `Null`
- [ ] with one supplied value, `callvar` contains that value and `callvar2` through `callvar5` are `Null`
- [ ] all callvar slots are read-only root values inside Evaluate
- [ ] reject `Dim callvar`, `Dim callvar2`, `Dim callvar3`, `Dim callvar4` and `Dim callvar5` inside Evaluate
- [ ] reject assignments that attempt to replace any callvar root value
- [ ] `IsNull(callvarN)` returns True for an omitted numbered parameter once final XPScript Null semantics are active; the first slot is tested as `IsNull(callvar)`

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
- [ ] extend `XPScriptEvaluateRuntime` invocation state to expose five stable callvar slots named `callvar`, `callvar2`, `callvar3`, `callvar4`, `callvar5`
- [ ] initialize omitted slots to XPScript `Null`
- [ ] do not create a separate `callvar1` alias; slot 1 is `callvar`
- [ ] avoid static/global storage for callvar values
- [ ] reuse existing collection snapshot/runtime contracts instead of reflection-based bridges
- [ ] evaluate each caller argument exactly once and preserve left-to-right argument evaluation order

## Permanent regression coverage

- [ ] no-callvar regression: `callvar` and `callvar2` through `callvar5` are Null
- [ ] one-callvar regression: existing `callvar` exposes the first value and slots 2 through 5 are Null
- [ ] two-callvar regression using `callvar` and `callvar2`
- [ ] five-callvar regression reading `callvar`, `callvar2`, `callvar3`, `callvar4`, `callvar5`
- [ ] negative regression proving `callvar1` is not a public Evaluate identifier
- [ ] mixed scalar + Array + List regression
- [ ] omitted trailing arguments are Null and safe to test with `IsNull`
- [ ] read-only negative regression for each callvar root
- [ ] `Dim callvar` / `Dim callvarN` negative regression
- [ ] aggregate budget regression across multiple callvars
- [ ] shared-reference identity regression where the same child collection is supplied through two different callvar slots
- [ ] concurrent multi-callvar isolation regression
- [ ] more-than-five-arguments negative compiler regression

## Documentation

- [ ] update `docs/evaluate.md` with the zero-to-five callvar syntax
- [ ] document that the first slot is `callvar`, followed by `callvar2` through `callvar5`; `callvar1` does not exist
- [ ] document that all five slots always exist and omitted values are `Null`
- [ ] document that input budgets are shared across all callvar parameters in one invocation

## Example contract

```xpscript
result = Evaluate("Return callvar + callvar2", firstValue, secondValue)

result = Evaluate("Return IsNull(callvar5)", firstValue)

result = Evaluate("Return callvar", firstValue)
' The first slot is named callvar. The second slot is callvar2, then callvar3 through callvar5.
```

`callvar`, `callvar2`, `callvar3`, `callvar4` and `callvar5` are the restricted input channels and `Return` is the explicit output channel. `callvar1` is intentionally not part of the language. Evaluate never implicitly shares the caller's variable namespace.
