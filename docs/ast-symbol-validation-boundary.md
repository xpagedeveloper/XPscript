# AST symbol and type validation boundary

The binder already validates declared procedure calls, overload arity and ByRef mode, assignment compatibility, `Set` compatibility, return types, conversions, unknown symbols and declared member access. Variant receivers intentionally retain dynamic member behavior.

Generated C# can still expose missing validation when source constructs are rewritten before binding or when a compatibility symbol is declared only to keep Roslyn compiling. Such cases must be migrated to real symbols and bound nodes before the AST path can claim parity; Roslyn success alone is insufficient evidence.
