# XPscript AST Compiler Migration TODO

## Goal

Investigate and incrementally introduce a real XPscript compiler front-end based on tokens, a syntax tree, semantic binding and structured code generation.

The migration must preserve existing XPscript behavior and runtime compatibility. Do not replace the current compiler in one large change.

Target architecture:

```text
XPscript source
  -> source preprocessing
  -> lexer
  -> tokens
  -> parser
  -> XPscript syntax tree (AST)
  -> semantic analysis / binder
  -> bound tree
  -> diagnostics
  -> C# emitter
  -> Roslyn
  -> .NET output
```

The existing runtime libraries, packaging, source mapping, web/desktop/mobile targets and Roslyn backend should be reused where practical.

## Current architecture investigation

- [ ] Document the complete current compiler pipeline from source input to generated assembly.
- [ ] Map all source preprocessors that run before `AdvancedXPScriptTranspiler`.
- [ ] Inventory parsing responsibilities currently implemented by `AdvancedXPScriptTranspiler`.
- [ ] Inventory regex-based syntax recognition and string rewriting in the compiler.
- [ ] Identify places where parsing, semantic analysis and C# emission are currently mixed.
- [ ] Identify syntax validation currently delegated to generated C# / Roslyn.
- [ ] Identify type and symbol validation currently delegated to generated C# / Roslyn.
- [ ] Map existing source-map behavior and requirements.
- [ ] Map compiler diagnostics and machine/MCP diagnostic contracts that must remain compatible.
- [ ] Inventory compiler tests by language feature and compiler phase.
- [ ] Document syntax that is intentionally compatible with LotusScript/VB-like semantics.
- [ ] Produce a migration-risk list before replacing any production parsing path.

## Migration rules

- [ ] Keep the current compiler operational while the AST implementation is introduced.
- [ ] Add focused tests before migrating each syntax feature.
- [ ] When a test fails, follow the repository test rule: create or move a small focused reproducer so it runs before the larger test.
- [ ] Never mark a syntax feature migrated until old and new paths have been compared against representative fixtures.
- [ ] Preserve public compiler diagnostics where compatibility is required.
- [ ] Keep normal compiler and machine/MCP compiler behavior synchronized.
- [ ] CLI compilation and MCP/AI machine compilation must consume the same lexer, parser, AST, semantic binder and bound tree; do not create a second language implementation for machine compilation.
- [ ] Every migrated syntax/semantic feature must be verified through both normal compilation and MCP/AI machine compilation when that feature is exposed through the machine interface.
- [ ] Any AST diagnostic change must be verified in both human-readable compiler output and the structured machine/MCP diagnostic contract.
- [ ] Every AST diagnostic must preserve the original XPscript source file, absolute source span, 1-based line and column.
- [ ] Unknown variables, functions, properties and methods must be reported by the XPscript semantic binder before Roslyn when the symbol/type information is available.
- [ ] Diagnostic parity is a release gate across normal CLI compilation and MCP/AI machine compilation.
- [ ] Diagnostic parity must be verified both with debug diagnostics disabled and enabled; debug mode may add generated-code/debug details but must not change the XPscript diagnostic code, meaning, source file, line, column or source span.
- [ ] Do not remove legacy parsing code until its replacement has dedicated tests and integration coverage.
- [ ] Prefer small commits organized by compiler phase or language feature.

## Phase 1: Syntax model

- [x] Define a common `SyntaxNode` base abstraction.
- [ ] Define source spans on every syntax node.
- [ ] Define `CompilationUnitSyntax`.
- [ ] Define declaration node hierarchy.
- [x] Define statement node hierarchy.
- [x] Define expression node hierarchy.
- [ ] Define type syntax nodes.
- [ ] Define parameter and argument syntax nodes.
- [ ] Decide how comments/trivia are represented.
- [ ] Decide whether malformed/incomplete syntax is retained using missing tokens/error nodes.
- [ ] Add syntax-tree debug/dump output for tests and diagnostics.

Initial expression nodes should cover at least:

- [x] LiteralExpressionSyntax
- [x] NameExpressionSyntax
- [x] UnaryExpressionSyntax
- [x] BinaryExpressionSyntax
- [x] ParenthesizedExpressionSyntax
- [x] CallExpressionSyntax
- [x] MemberAccessExpressionSyntax
- [x] IndexExpressionSyntax
- [x] NewExpressionSyntax
- [x] ArrayExpressionSyntax

Initial statement nodes should cover at least:

- [x] AssignmentStatementSyntax
- [x] Expression/CallStatementSyntax
- [x] DimStatementSyntax
- [x] SetStatementSyntax
- [x] IfStatementSyntax
- [x] ForStatementSyntax
- [x] ForAllStatementSyntax
- [x] WhileStatementSyntax
- [x] DoStatementSyntax
- [x] SelectStatementSyntax
- [x] Return/Exit statements

Initial declaration nodes should cover at least:

- [ ] SubDeclarationSyntax
- [ ] FunctionDeclarationSyntax
- [ ] ClassDeclarationSyntax
- [ ] FieldDeclarationSyntax
- [ ] PropertyDeclarationSyntax
- [ ] Constructor/Destructor declarations

## Phase 2: Lexer

- [x] Define `SyntaxKind` / token kinds.
- [x] Implement identifiers and keywords.
- [x] Implement numeric literals.
- [x] Implement string literals and XPscript escaping rules.
- [x] Implement punctuation.
- [x] Implement operators.
- [x] Implement newline handling.
- [x] Implement comments.
- [x] Preserve exact source spans.
- [x] Produce structured lexical diagnostics.
- [x] Add focused lexer tests for every token family.
- [x] Add malformed-token tests.
- [x] Add regression fixtures for quotes and comments.

## Phase 3: Expression parser first

Expressions are the first migration target because the current transpiler performs substantial textual expression rewriting and expression precedence directly affects correctness.

- [x] Implement precedence-based expression parsing.
- [ ] Define precedence and associativity for every XPscript operator.
- [x] Implement unary `Not`, unary plus and unary minus.
- [x] Implement arithmetic operators.
- [x] Implement comparison operators.
- [x] Implement boolean operators.
- [x] Implement parentheses.
- [x] Implement calls.
- [x] Implement member access.
- [x] Implement indexing.
- [x] Implement array expressions.
- [x] Implement `New`.
- [ ] Implement zero-argument runtime function syntax.
- [x] Test nested calls and member access.
- [x] Test mixed unary/binary precedence.
- [x] Add focused regression for `If Not RunCommand(...) Then`.
- [x] Add equivalent regression for `If RunCommand(...) = False Then`.
- [x] Verify both produce the intended boolean semantics.
- [x] Compare expression output against the legacy transpiler on existing fixtures.

## Phase 4: Statement parser

- [x] Parse variable declarations.
- [x] Parse assignments and `Set`.
- [x] Parse single-line and block `If`.
- [x] Parse `ElseIf` and `Else`.
- [x] Parse loops.
- [x] Parse `Select Case`.
- [x] Parse calls used as statements.
- [x] Parse error-handling statements.
- [x] Parse event-related statements.
- [x] Parse file/runtime-specific statements that currently have compiler rewrites.
- [x] Add recovery at statement boundaries so one syntax error does not destroy the remaining tree.
- [x] Add focused tests per statement family.

## Phase 5: Declarations and program structure

- [ ] Parse Subs and Functions.
- [ ] Parse parameters including ByRef/ByVal semantics.
- [ ] Parse return types.
- [ ] Parse Classes and inheritance syntax.
- [ ] Parse fields.
- [ ] Parse properties.
- [ ] Parse constructors and destructors.
- [ ] Parse visibility modifiers.
- [ ] Parse application-level declarations.
- [ ] Parse target-specific entry points.
- [ ] Validate block terminators structurally rather than with transpiler state.

## Phase 6: Symbols and semantic binder

- [ ] Define symbol base model.
- [ ] Define variable/local symbols.
- [ ] Define parameter symbols.
- [ ] Define procedure symbols.
- [ ] Define class/type symbols.
- [ ] Define property/field symbols.
- [ ] Integrate compiler-owned runtime/public symbol catalog.
- [ ] Implement lexical scopes.
- [ ] Implement name lookup.
- [ ] Implement duplicate declaration diagnostics.
- [ ] Implement unknown symbol diagnostics.
- [ ] Implement member lookup.
- [ ] Implement overload/call binding where required.
- [ ] Implement ByRef/ByVal validation.
- [ ] Implement assignment compatibility.
- [ ] Implement return-type validation.

## Phase 7: Bound tree and types

- [ ] Define `BoundNode` hierarchy separate from syntax.
- [ ] Bind literals to XPscript types.
- [ ] Bind names to symbols.
- [ ] Bind unary operators.
- [ ] Bind binary operators.
- [ ] Bind calls.
- [ ] Bind member access.
- [ ] Bind assignments.
- [ ] Bind control flow.
- [ ] Define conversion rules.
- [ ] Define Variant/dynamic semantics explicitly.
- [ ] Define Object semantics explicitly.
- [ ] Define Null/Empty behavior needed by XPscript.
- [ ] Define array/list typing behavior.
- [ ] Move applicable type diagnostics from Roslyn-derived failures into XPscript semantic diagnostics.

## Phase 8: C# emitter

- [ ] Implement C# generation from bound nodes.
- [ ] Keep emitter free of XPscript parsing logic.
- [ ] Reuse existing runtime APIs rather than reimplementing runtime behavior.
- [ ] Preserve generated source mapping.
- [ ] Preserve target-specific generated code requirements.
- [ ] Preserve generated runtime integration.
- [ ] Keep Roslyn as the .NET backend and final generated-C# validator.
- [ ] Add snapshot/golden tests for generated C# where useful.

## Phase 9: Diagnostics and tooling

- [ ] Attach source span and source file to all parser diagnostics.
- [ ] Attach source span and source file to semantic diagnostics.
- [ ] Map new diagnostics into existing `CompileDiagnostic`.
- [ ] Preserve stable XPS diagnostic codes where applicable.
- [ ] Add new codes only with documented categories and tests.
- [ ] Update compiler machine interface together with normal compiler diagnostics.
- [ ] Update MCP compiler output together with normal compiler diagnostics.
- [ ] Add paired diagnostic tests that feed the same invalid XPscript source through CLI compilation and MCP/AI machine compilation and verify equivalent diagnostic code, source span and meaning.
- [ ] Expose syntax-tree information internally in a form suitable for future IDE tooling.
- [ ] Investigate parser APIs for completion, hover, go-to-definition and rename.
- [ ] Investigate incremental parsing only after the non-incremental AST is stable.

## Phase 10: Compatibility harness

- [ ] Build a corpus of existing XPscript samples and test programs.
- [ ] Run each compatible source through legacy and AST compiler paths.
- [ ] Compare compile success/failure.
- [ ] Compare diagnostic locations and categories.
- [ ] Run representative corpus cases through the MCP/AI machine compiler as well as the normal compiler.
- [ ] Compare CLI and machine/MCP compile success/failure for the same source.
- [ ] Compare CLI and machine/MCP structured diagnostics for the same source.
- [ ] Compare observable runtime output.
- [ ] Compare generated target behavior for console.
- [ ] Compare web target behavior.
- [ ] Compare desktop/Avalonia behavior.
- [ ] Compare Android/mobile behavior as support matures.
- [ ] Add explicit compatibility fixtures for LotusScript-like edge cases.
- [ ] Record intentional behavior changes instead of silently changing semantics.

## Post-migration performance investigation

Only start this after the full AST front-end, binder, bound tree, diagnostics, emitter integration and compatibility verification are working reliably.

- [ ] Benchmark compile time before and after the AST production path is enabled.
- [ ] Measure where compile time is spent across preprocessing, lexing, parsing, binding, C# generation and Roslyn.
- [ ] Investigate caching the parsed AST per source file.
- [ ] Investigate caching the bound tree / semantic result per source file.
- [ ] Define cache invalidation for changed files, referenced symbols, imports/includes and compiler options.
- [ ] Regenerate generated C# only for files or units whose syntax or bound semantics changed.
- [ ] Investigate compiling only changed generated units instead of rebuilding all generated C# when the backend architecture allows it.
- [ ] Preserve correct cross-file symbol binding when only a subset of files changes.
- [ ] Add correctness tests that compare clean builds with incremental/cache-enabled builds.
- [ ] Add performance benchmarks for edit-compile cycles in small and large XPscript projects.
- [ ] Evaluate whether persistent AST + bound-tree caching materially improves edit-compile latency before making it the default.

Target outcome: unchanged files should be able to reuse their AST and bound semantic state so that edit-compile cycles only redo the work invalidated by the user's changes, while producing results identical to a clean build.

## Phase 11: Incremental rollout

- [ ] Add an internal/experimental switch for AST compilation.
- [ ] Migrate expressions first.
- [ ] Migrate simple statements.
- [ ] Migrate control-flow statements.
- [ ] Migrate procedures.
- [ ] Migrate classes/properties.
- [ ] Migrate remaining special compiler constructs.
- [ ] Run focused tests after every migrated feature.
- [ ] Run compiler subsystem suites before broader full tests.
- [ ] Make AST path the default only after compatibility gates pass.
- [ ] Keep temporary legacy fallback only where explicitly documented.
- [ ] Remove legacy parser/transpiler paths after all supported syntax is migrated.
- [ ] Remove obsolete regex/string rewriting after replacement coverage exists.

## Investigation questions

- [ ] Should the lexer/parser be handwritten or use a parser-generator library?
- [ ] If considering a dependency, evaluate maintenance, license, performance, diagnostics, recovery and incremental parsing support.
- [ ] Should syntax nodes be immutable?
- [ ] Should tokens be first-class syntax nodes similar to Roslyn?
- [ ] How much trivia must be retained for future formatting/refactoring?
- [ ] Which current preprocessors belong before parsing, and which should become syntax/semantic features?
- [ ] Which existing transformations should become lowering passes over the bound tree?
- [ ] Which XPscript semantics differ from C# and therefore must never be delegated to Roslyn?
- [ ] Can generated C# source maps become simpler when every XPscript node has a source span?
- [ ] What compiler APIs should be public/internal to support a future language server?
- [ ] What performance baseline must the AST compiler meet or beat?

## Initial proof of concept acceptance criteria

The first AST proof of concept is complete when:

- [ ] A lexer can tokenize a representative XPscript expression with exact spans.
- [ ] An expression parser produces a deterministic syntax tree.
- [ ] Operator precedence is covered by focused tests.
- [ ] `Not RunCommand(...)` is represented as a unary expression over the call result.
- [ ] `RunCommand(...) = False` is represented as a comparison expression.
- [ ] Both forms can be bound to boolean semantics without relying on C# parsing to determine XPscript meaning.
- [ ] The proof of concept can emit valid C# for those expressions.
- [ ] Existing production compilation remains unchanged unless the experimental path is explicitly selected.
- [ ] The proof of concept demonstrates that both CLI compilation and MCP/AI machine compilation can call the same AST front-end without duplicating parsing or semantic logic.

## Definition of done for full migration

- [ ] All supported XPscript syntax is represented by the XPscript syntax tree.
- [ ] Parsing no longer depends on line-by-line regex/string transformation.
- [ ] Semantic analysis operates on XPscript syntax/bound nodes.
- [ ] C# generation operates from the bound representation.
- [ ] Roslyn is used as the C#/.NET backend, not as the primary XPscript parser/type checker.
- [ ] Existing supported runtime behavior passes compatibility tests.
- [ ] Normal compiler and machine/MCP interfaces use the same AST/binder implementation and report consistent diagnostics.
- [ ] Legacy parser/transpiler implementation is removed or reduced to explicitly justified compatibility code.
- [ ] Compiler architecture and extension guidance are documented.
