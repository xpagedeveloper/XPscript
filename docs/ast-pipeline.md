# AST compiler pipeline

The experimental AST path currently processes a source file in this order:

1. `AstExperimentalCompiler.CompileAsync` reads the source and applies the compatibility preprocessor.
2. `DeclarationParser` builds declarations and procedure syntax nodes.
3. `StatementParser` and the expression parser build syntax nodes for the selected procedure.
4. `StatementBinder` resolves names, overloads, conversions and control-flow bound nodes.
5. `BoundMethodEmitter` and `BoundStatementEmitter` emit C# from the bound tree.
6. Roslyn compiles the generated C# together with the shared runtime source and writes `Generated.dll`.

The compatibility preprocessor is deliberately treated as a migration boundary, not as semantic lowering. Current rewrites for GoTo/GoSub, Resume/On Error, implicit member statements and XPImage are tracked in `todo/ast-compiler-migration-todo.md` because they can discard observable behavior. A feature is complete only after its syntax is represented in the bound tree, emitted structurally and compared with the published compiler.

The experimental machine path now exposes `xpscript_ast_validate`. It invokes the same `AstExperimentalCompiler` entry point as the `ast-compile` CLI command and returns a non-executing validation result, keeping lexer/parser/binder/emitter behavior in one implementation.

`tests/CompilerMcpProtocolProbe` pairs this path with the `Static` lifetime fixture. The normal AST CLI executes the fixture and expects `1`, `2`; AST-MCP validates the identical source through the same compiler entry point. New AST features must add an equivalent paired regression before machine exposure.

The published compiler remains the compatibility reference. Its executable under `publish/xpscript/win-x64` is not rebuilt by the AST workflow.

## Comments and trivia

The AST lexer treats apostrophe comments as trivia: comment text is skipped and is not represented as a syntax node or token. The terminating newline remains a `NewLineToken`, so statement boundaries and source positions are preserved. Apostrophes inside quoted string literals remain part of the string token. This keeps the syntax tree focused on compilable structure while retaining enough source information for diagnostics and compatibility preprocessing.
