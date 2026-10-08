# AST migration risk list

The highest compatibility risks are the constructs currently erased before binding: labels and GoTo/GoSub/Return flow, Resume and On Error state, implicit `With` member reads/writes, and XPImage construction/loading. The next risks are production integration (shared CLI/MCP path), source-mapped diagnostics, complete declaration and class lowering, and runtime/API coverage.

Each risk is closed only by an executable AST regression plus published-main comparison. A compile-only result is insufficient because the current placeholders can produce valid C# while changing observable behavior.
