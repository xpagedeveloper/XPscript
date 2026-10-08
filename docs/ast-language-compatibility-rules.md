# AST language compatibility rules

The AST path must preserve the established LotusScript/VB-like behavior: `Empty`, `Nothing` and `Null` retain their distinct runtime meanings; `ByRef` aliases the caller storage; omitted `Optional` arguments evaluate their declared defaults; `Exit Function` and fall-through return the function result; and GoTo/GoSub/Return plus `On Error` retain their legacy control-flow semantics.

These rules are language behavior, not emitter conveniences. Any lowering that changes them is a compatibility regression and must be compared with the published compiler before being accepted.
