# XPScript error code documentation TODO

(c) xpagedeveloper.com 2026

Goal: create one authoritative error-code reference under `docs/` so developers can quickly understand every XPScript compiler/runtime error code.

## Documentation

- [ ] create `docs/error-codes.md`
- [ ] list every public XPScript error code used by the compiler and runtime
- [ ] include the numeric error code
- [ ] include a short, clear title/name for each error
- [ ] include a concise explanation of what the error means and when it is normally raised
- [ ] distinguish compiler diagnostics from runtime errors where relevant
- [ ] document important compatibility/runtime codes such as type mismatch, overflow, divide by zero, file/permission errors and generic Evaluate/runtime errors
- [ ] avoid documenting internal-only implementation exceptions as public XPScript error codes unless they can be exposed to user code
- [ ] verify the list against the actual compiler/runtime implementation so no codes are invented or omitted
- [ ] add a link to `docs/error-codes.md` from the appropriate documentation index/reference page

## Verification

- [ ] add a documentation/reference regression check that fails if the error-code document is missing
- [ ] where practical, cross-reference existing regression samples that prove important error-code mappings
- [ ] mark this TODO complete and move it to `todo/done/` only after the document and verification gate are in place
