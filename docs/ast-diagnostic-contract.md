# AST diagnostic contract

AST diagnostics must preserve the XPscript contract: original source file, absolute span, one-based line and column, stable diagnostic code and user-facing meaning. Human CLI output and structured MCP output must report the same contract. Debug mode may add generated-code details, but it must not change the XPscript diagnostic identity or location.

A Roslyn error is actionable only when no AST rule exists yet; once syntax and binding support is migrated, the AST layer must report the language diagnostic before generated C# compilation.
