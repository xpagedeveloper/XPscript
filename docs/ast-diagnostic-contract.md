# AST diagnostic contract

AST diagnostics must preserve the XPscript contract: original source file, absolute span, one-based line and column, stable diagnostic code and user-facing meaning. Human CLI output and structured MCP output must report the same contract. Debug mode may add generated-code details, but it must not change the XPscript diagnostic identity or location.

A Roslyn error is actionable only when no AST rule exists yet; once syntax and binding support is migrated, the AST layer must report the language diagnostic before generated C# compilation.
The AST binder reports unknown symbols and members before generated C# reaches Roslyn when the symbol/type information is available. Unknown names use `XPS2008`; unknown members on declared object-backed types use `XPS2009`. Variant receivers remain dynamic and are not rejected by this rule. The focused `tests/ast-binding` probe verifies these boundaries, while `tests/CompilerMcpProtocolProbe` verifies the shared machine-interface diagnostic path.

The protocol probe also compares normal and debug CLI/MCP diagnostic identities, including code, file, source position, span and description. This keeps the current public diagnostic surface synchronized while feature-specific machine exposure is migrated.

