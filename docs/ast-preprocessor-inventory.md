# AST preprocessor inventory

`AstExperimentalCompiler.CompileAsync` currently applies these source-level steps before parsing:

- removes request-binding attributes and normalizes `Static` declarations;
- rewrites selected file, binary-I/O and utility commands to AST helper calls;
- normalizes line continuations, constants and `Error$`;
- rewrites `ReDim` into `ArrayResize`;
- rewrites selected XPImage calls and encoding helpers;
- removes labels and converts GoTo, GoSub, Resume and On Error to `AstNoOp`;
- removes `With` delimiters and rewrites implicit member lines to placeholders.

The first groups are compatibility shims with explicit helper calls. The last two groups are semantic losses: they erase control-flow targets, error-handler state or member reads/writes. They must be removed only after equivalent syntax nodes and bound/emitted runtime behavior exist. The parity fixtures and published executable remain the acceptance reference for those migrations.
