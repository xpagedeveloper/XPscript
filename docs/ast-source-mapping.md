# AST source mapping

Lexer tokens and syntax nodes retain absolute source spans. Binding diagnostics should report those original spans, while `BoundMethodEmitter.EmitWithSourceMap` can add generated `#line` directives and mapping records for emitted C#. The mapping boundary is the syntax node that produced a bound node; generated helper code must not replace the user span.

Control-flow lowering must preserve spans for labels, GoTo/GoSub targets and Resume statements. A diagnostic for an unresolved target therefore points at the target token in the original `.xps` file, even when the emitted C# uses a generated label name.
