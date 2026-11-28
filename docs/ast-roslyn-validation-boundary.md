# AST validation boundary

The AST binder owns the diagnostics it can determine from XPscript symbols and declared types: unknown names, member lookup, overload selection, conversions, return types and control-flow expressions. Roslyn still validates generated C# details such as emitted identifier legality, `ref` call shape, helper signatures, definite assignment and any syntax that bypassed the AST model.

Roslyn diagnostics are implementation feedback, not the compatibility contract. When a language construct is supported, its user-facing validation must move into the syntax/binding phases with XPscript source spans and diagnostic codes before the corresponding generated-C# fallback is removed.
