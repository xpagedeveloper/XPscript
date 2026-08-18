# Evaluate runtime closeout

(c) xpagedeveloper.com 2026

This closeout records the verified state of the standalone XPScript `Evaluate` implementation so the older high-level runtime checklist can be reconciled without losing the detailed evidence kept in `todo/evaluate-callvar-todo.md`.

## Verified implementation

The following items are implemented and permanently regression-tested:

- `Evaluate(sourceText)` uses the isolated XPScript evaluator.
- `Evaluate(sourceText, callvar)` provides the only explicit caller-data bridge.
- The obsolete `System.Data.DataTable.Compute` evaluator has been physically removed.
- Legacy formula-engine terminology is no longer used by the Evaluate implementation.
- Caller locals, module globals and Static locals are not implicitly visible inside Evaluate.
- Scalar, Array and List callvar values are snapshotted before evaluation.
- Nested Arrays and Lists are recursively snapshotted with cycle and shared-reference handling.
- Arbitrary mutable objects are rejected instead of being shared into the evaluator.
- `Return expression` is the explicit result path.
- Reaching the end without Return yields Variant EMPTY.
- `Return Null` yields Variant NULL.
- `Return Nothing` is rejected with bounded XPScript error 5 semantics.
- EMPTY and NULL remain distinct through scalar, Array and List callvar paths.
- Conversion, inspection, string, math/number and date/time function categories are covered by the Evaluate regression corpus.
- `IsObject(Null)` and `IsScalar(Null)` now match normal runtime semantics.
- Coercion and error mapping cover type mismatch, divide-by-zero, overflow, permission failures and parser/API failures.
- Diagnostics are sanitized before crossing the Evaluate boundary.
- Collection snapshots are bounded by depth, element count and payload size.
- Concurrent evaluations use independent evaluator and snapshot state.
- Input and returned mutable collections are detached from caller/evaluator storage.
- Evaluate runtime classes are checked for unintended mutable static state.

## Permanent verification

The primary permanent gate is `Evaluate Runtime Compatibility`.

Focused supporting gates include:

- `Evaluate Null Empty Semantics`
- `Evaluate Negative Assertion Guard`
- `Null Empty Semantics`
- `Managed Null Interop`

The regression corpus and generated-runtime probes cover the detailed evidence listed in `todo/evaluate-callvar-todo.md`.

## Remaining conditional items

The following are intentionally not treated as current implementation gaps:

- nested Evaluate snapshot semantics, because nested Evaluate syntax is not currently part of the language surface
- deterministic disposal of native/disposable callvar objects, because such objects are currently rejected rather than bridged

If either capability is introduced later, its corresponding contract must be implemented and regression-tested before the feature is considered complete.
