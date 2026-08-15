# XPScript error code documentation TODO

(c) xpagedeveloper.com 2026

Goal: create one authoritative error-code reference under `docs/` so developers can quickly understand every XPScript compiler/runtime error code.

## Documentation

- [x] create `docs/error-codes.md`
- [x] list every public XPScript error code used by the compiler and runtime
- [x] include the numeric error code
- [x] include a short, clear title/name for each error
- [x] include a concise explanation of what the error means and when it is normally raised
- [x] distinguish compiler diagnostics from runtime errors where relevant
- [x] document important compatibility/runtime codes such as type mismatch, overflow, divide by zero, file/permission errors and generic Evaluate/runtime errors
- [x] avoid documenting internal-only implementation exceptions as public XPScript error codes unless they can be exposed to user code
- [x] verify the list against the actual compiler/runtime implementation so no codes are invented or omitted; source of truth is `XPScriptErrorRuntime` plus explicit public runtime mappings such as permission/access error 70
- [x] add a link to `docs/error-codes.md` from the `Error / Error$` section of `docs/console-process-formatting.md`

## Verification

- [x] documentation/reference regression fails if `docs/error-codes.md` is missing through `tools/validate_docs.py`
- [x] cross-reference existing regression samples that prove important Evaluate error-code mappings
- [x] mark this TODO complete and move it to `todo/done/` only after the document and verification gate are in place
