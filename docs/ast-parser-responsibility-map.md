# Parser responsibility map

The AST path owns lexical tokenization, declaration parsing, statement parsing, expression parsing and syntax diagnostics for the migrated subset. `DeclarationParser` selects procedure and class declarations; `StatementParser` owns block structure and statement spans; expression parsing owns precedence and call/member/index syntax.

The legacy transpiler still recognizes syntax that is not represented by AST nodes, including labels and GoTo/GoSub control transfers, error-handler transfers, implicit `With` members, several runtime object operations and target-specific declarations. Those constructs currently cross the AST boundary as textual rewrites or placeholders. They remain migration tasks until parser nodes, binder rules and emitted runtime behavior are covered by published-main comparisons.
