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

Release gate: when the AST compiler becomes the supported production compilation path and the migration definition of done is satisfied, release the compiler as `0.9.4 Beta`. Keep published `0.9.3 Beta` binaries unchanged for compatibility verification until that gate is reached.

## Current architecture investigation

- [x] Document the complete current compiler pipeline from source input to generated assembly in `docs/ast-pipeline.md`.
- [x] Map all source preprocessors that run before `AdvancedXPScriptTranspiler` in `docs/ast-preprocessor-inventory.md`.
- [x] Inventory parsing responsibilities currently implemented by `AdvancedXPScriptTranspiler` in `docs/ast-parser-responsibility-map.md`.
- [x] Inventory regex-based syntax recognition and string rewriting in the compiler in `docs/ast-regex-rewrite-inventory.md`.
- [x] Identify places where parsing, semantic analysis and C# emission are currently mixed in `docs/ast-phase-boundaries.md`.
- [x] Identify syntax validation currently delegated to generated C# / Roslyn in `docs/ast-roslyn-validation-boundary.md`.
- [x] Identify type and symbol validation currently delegated to generated C# / Roslyn in `docs/ast-symbol-validation-boundary.md`.
- [x] Map existing source-map behavior and requirements in `docs/ast-source-mapping.md`.
- [x] Map compiler diagnostics and machine/MCP diagnostic contracts in `docs/ast-diagnostic-contract.md`.
- [x] Inventory compiler tests by language feature and compiler phase in `docs/ast-test-coverage-map.md`.
- [x] Document syntax that is intentionally compatible with LotusScript/VB-like semantics in `docs/ast-language-compatibility-rules.md`.
- [x] Produce a migration-risk list before replacing any production parsing path in `docs/ast-migration-risk-list.md`.

## Migration rules

- [x] Keep the current compiler operational while the AST implementation is introduced.
- [x] Add focused tests before migrating each syntax feature.
- [x] When a test fails, follow the repository test rule: create or move a small focused reproducer so it runs before the larger test.
- [x] Never mark a syntax feature migrated until old and new paths have been compared against representative fixtures.
- [x] Preserve the current public compiler diagnostic contract where compatibility is required. Focused AST CLI regressions retain stable parser, entry-point and Optional validation messages; binder and MCP probes retain stable codes, source locations and structured properties. Feature-specific diagnostic migration remains tracked by the later source-mapping items.
- [x] Keep normal compiler and machine/MCP compiler behavior synchronized for the current diagnostic surface. The protocol probe compares normal/debug MCP identities with normal/debug CLI identities and rejects drift; broader migrated-feature exposure remains tracked separately.
- [x] CLI compilation and MCP/AI machine compilation consume the same experimental AST compiler entry point; `xpscript ast-compile` and `xpscript_ast_validate` both call `AstExperimentalCompiler`, so machine validation does not create a second language implementation.
- [x] Every currently migrated AST syntax/semantic feature exposed through the machine interface is verified through both normal AST compilation and AST-MCP validation. The Static lifetime parity fixture is the first permanent paired regression; additional migrated features must extend the same pattern before being exposed.
- [x] AST diagnostic changes are verified in both human-readable CLI output and the structured machine/MCP diagnostic contract. AST-MCP now preserves diagnostic code, source filename, positions, severity and category; the protocol probe covers both successful AST validation and a source-mapped AST error.
- [x] Every AST diagnostic preserves the original XPscript source filename, mapped line/column and end span in structured MCP output and human-readable AST CLI output. The parser-error regression verifies the same source location contract in both surfaces.
- [x] Unknown variables, functions, properties and methods are reported by the XPscript semantic binder before Roslyn when the symbol/type information is available. Focused binder coverage verifies XPS2008 unknown symbols and XPS2009 unknown members; the MCP protocol probe also passes.
- [x] Diagnostic parity is a release gate across normal AST CLI compilation and AST-MCP validation. The permanent MCP protocol probe runs the same parser-error source through both surfaces and compares stable code, filename and source position.
- [x] Diagnostic parity is verified with debug diagnostics disabled and enabled. AST CLI and AST-MCP preserve the same diagnostic code, source file, line, column and span; debug mode is allowed only to add implementation details.
- [x] Do not remove legacy parsing code until its replacement has dedicated tests and integration coverage.
- [x] Prefer small commits organized by compiler phase or language feature.

## Phase 1: Syntax model

- [x] Define a common `SyntaxNode` base abstraction.
- [x] Define source spans on every syntax node. `SyntaxNode.Span` and `TextSpan` are source-derived across the syntax tree; focused AST tests cover tokens, expressions, statements, declarations and diagnostics.
- [x] Define `CompilationUnitSyntax`; `DeclarationParser.ParseCompilationUnit` returns it with source-derived spans.
- [x] Define declaration node hierarchy. All compilation-unit and class declarations derive from `DeclarationSyntax`.
- [x] Define statement node hierarchy.
- [x] Define expression node hierarchy.
- [x] Define type syntax nodes; `TypeSyntax` preserves the declared type token and span.
- [x] Define parameter syntax nodes; `ParameterSyntax` preserves Optional/ByRef/ByVal/default metadata. Call arguments are represented by expression lists with source spans.
- [x] Decide how comments/trivia are represented. Apostrophe comments are skipped as trivia; line-ending tokens are retained for statement boundaries and source mapping, and apostrophes inside strings remain string content.
- [x] Decide whether malformed/incomplete syntax is retained using missing tokens/error nodes.
- [x] Add syntax-tree debug/dump output for tests and diagnostics.

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

- [x] SubDeclarationSyntax; parsed and bound through the AST compile probe.
- [x] FunctionDeclarationSyntax; parsed and bound through the AST compile probe.
- [x] ClassDeclarationSyntax; parsed by `DeclarationParser` and covered by declaration/inheritance tests.
- [x] FieldDeclarationSyntax; parsed as class members.
- [x] PropertyDeclarationSyntax; parsed with accessor, parameters and body.
- [x] Constructor/Destructor declarations; parsed as class members with bodies.

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
- [x] Define precedence and associativity for every XPscript operator.
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
- [x] Implement zero-argument runtime function syntax.
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

- [x] Parse Subs and Functions.
- [x] Parse parameters including ByRef/ByVal semantics.
- [x] Parse return types.
- [x] Parse Classes and inheritance syntax; `DeclarationParser` handles `Extend`, fields, properties and members, with inheritance contract regressions in Language FullTest.
- [x] Parse fields.
- [x] Parse properties.
- [x] Parse constructors and destructors.
- [x] Parse visibility modifiers.
- [x] Parse application-level declarations. `DeclarationParser` retains top-level `Option`, `Const` and `Declare` lines as typed application declaration nodes; the `.wps` regression compiles and prints `42`.
- [x] Parse target-specific entry points. AST compilation prefers `Sub Main`, otherwise selects the first `Sub`/`Function`; target route attributes are removed before parsing and the fallback entry point is covered by `target-entry-point.wps`.
- [x] Validate block terminators structurally rather than with transpiler state. Statement parsers consume matching terminator tokens and diagnose incomplete blocks; the AST probe covers an unterminated `If` regression.

### Custom class compatibility contract

Use LotusScript custom-class behavior as the semantic reference where it fits XPscript. Do not introduce SSJS, Domino product-object, Notes API, LSX, JVM, XPages, or other host-specific semantics into the XPscript class model.

XPscript intentionally differs from LotusScript in these syntax choices:
- [x] Keep XPscript inheritance syntax as `Class Child Extend Parent`; do not restore LotusScript `Class Child As Parent`.
- [x] Keep `Parent.member` as the XPscript syntax for direct base-class access; do not require LotusScript `BaseClass..member`.
- [x] Keep the existing explicit `Sub Delete` / `Delete object` lifecycle model; do not add `Sub Terminate` or tie deletion to .NET GC/finalizer timing.

Class declaration and scope:
- [x] Restrict class declarations to module/application declaration scope; reject nested classes and classes declared inside procedures.
- [x] Verify class default visibility is Private unless XPscript `Option Public` semantics explicitly change it.
- [x] Verify member fields default to Private.
- [x] Verify member Subs, Functions, and Properties default to Public.
- [x] Reject `Static` class fields and member procedures unless XPscript deliberately defines a documented extension.
- [x] Require one class field per declaration; do not silently accept comma-separated field declarations.
- [x] Permit self-referential field types but do not allow field declarations to instantiate themselves with `New`.
- [x] Define and test reserved-keyword rules for field names and keyword-named member procedures.

Members and properties:
- [x] Support fields, Subs, Functions, Properties, `Sub New`, and `Sub Delete` as class members.
- [x] Keep constants out of class members unless XPscript deliberately defines a documented extension.
- [x] Support read-only, write-only, and read/write properties.
- [x] Support parameterized/indexed properties and require compatible Get/Let/Set index signatures for accessors sharing a property name.
- [x] Preserve XPscript scalar `Property Let` versus object/reference `Property Set` assignment semantics.
- [x] Enforce that a property name cannot conflict with a field/member variable name where the existing language contract forbids it.
- [x] Verify property getter assignment to the property name produces the getter result, and setter access exposes the documented incoming value semantics.

Inheritance and member access:
- [x] Bind `Me.member` to the current class instance and validate it only where an instance context exists.
- [x] Bind `Parent.member` to the direct base-class member and support base Subs, Functions, and Properties according to XPscript visibility rules.
- [x] Define whether `Parent` may access Private base procedures/properties; test the chosen XPscript rule explicitly rather than inheriting backend C# behavior accidentally.
- [x] Support overriding inherited methods/properties only with compatible signatures and produce XPscript diagnostics for incompatible overrides.
- [x] Verify inherited constructor behavior and define explicit base-constructor invocation syntax/semantics for XPscript.
- [x] Verify inherited destructor/delete ordering and ensure base cleanup occurs exactly once.

Object lifecycle and references:
- [x] Verify `New` invokes `Sub New` for each newly created XPscript object.
- [x] Verify object references start as `Nothing` where required by XPscript semantics.
- [x] Verify `Delete object` invokes `Sub Delete`, clears the deleted reference, and preserves the repository's defined alias/reference semantics.
- [x] Verify `Set object = Nothing` remains reference clearing only and does not implicitly call `Sub Delete`.
- [x] Add focused tests for constructor arguments, explicit deletion, aliases, inheritance, `Me`, `Parent`, and property access before marking class migration complete.

Class-oriented language patterns:
- [x] Verify fluent/chained method calls on class instances, including continued chains across XPscript line continuation.
- [x] Treat singleton/lazy-instance patterns as ordinary XPscript module/application-level state plus class construction; do not add NotesSession-, JVM-, SSJS-, or Domino-specific singleton behavior.

## Phase 6: Symbols and semantic binder

- [x] Define symbol base model.
- [x] Define variable/local symbols.
- [x] Define parameter symbols.
- [x] Define procedure symbols.
- [x] Define class/type symbols.
- [x] Define property/field symbols.
- [x] Integrate compiler-owned runtime/public symbol catalog.
- [x] Implement lexical scopes.
- [x] Implement name lookup.
- [x] Implement duplicate declaration diagnostics.
- [x] Implement unknown symbol diagnostics.
- [x] Implement member lookup.
- [x] Implement overload/call binding where required.
- [x] Implement ByRef/ByVal validation.
- [x] Implement assignment compatibility.
- [x] Implement return-type validation.

## Phase 7: Bound tree and types

Reverification on 2026-10-08: the five completed items at the end of this phase (Variant/dynamic, Object, Null/Empty, array/list typing and binder type diagnostics) passed `tests/ast-binding`; the lexer/compatibility and executable emission probes also passed. The binder probe is now included in the permanent Language FullTest runner, with completion markers required for all five areas. The complete Windows Language FullTest passed with this coverage enabled. This verifies the current AST semantic slice, not the still-pending production CLI/MCP integration.

- [x] Define `BoundNode` hierarchy separate from syntax.
- [x] Bind literals to XPscript types.
- [x] Bind names to symbols.
- [x] Bind unary operators.
- [x] Bind binary operators.
- [x] Bind calls.
- [x] Bind member access.
- [x] Bind assignments.
- [x] Bind control flow.
- [x] Define conversion rules.
- [x] Define Variant/dynamic semantics explicitly.
- [x] Define Object semantics explicitly.
- [x] Define Null/Empty behavior needed by XPscript.
- [x] Define array/list typing behavior.
- [x] Move applicable type diagnostics from Roslyn-derived failures into XPscript semantic diagnostics.

## Phase 8: C# emitter

Latest verification: the RunCommand array-conversion snapshot needed the legacy comparison updated as well. Exact AST conversion assertions are retained, and executable emission coverage verifies both boolean forms with true and false results. The AST CLI fixture runs first in Language FullTest, followed by the lexer/compatibility probe.

Next increment implemented: a console compilation-unit emitter for already-bound method definitions, with a two-Sub parse/bind/emit/compile/run regression. This is an internal emission boundary; full declaration, runtime and CLI/MCP integration remain open below.

Reverification on 2026-10-08 of the five most recently completed implementation items before the Optional syntax increment: real procedure bodies/ByRef and structural function results passed the executable `tests/ast-compile-probe/run-ast-cli.ps1` regression; restricted dynamic call fallback, XPS2004 overload diagnostics and XPS2009 typed-receiver member diagnostics passed `tests/ast-binding`. The latter checks retain Variant calls and assert diagnostic source spans. Optional declaration syntax also passed `tests/ast-lexer`. These results cover the implemented experimental AST slice; shared machine integration and complete Optional semantics remain open.

- [x] Verify the latest RunCommand conversion fix with exact emission and executable true/false coverage.
- [x] Introduce a bound-method console compilation-unit emitter and execute a cross-procedure call regression.
- [x] Add these probes to the existing Language FullTest runner and pass the complete Windows Language FullTest.
- [x] Connect compilation-unit emission to the shared experimental CLI/machine compilation path with real procedure bodies and runtime integration. `AstExperimentalCompiler` binds the parsed unit, emits real method bodies and produces executable `Generated.dll`; the AST CLI/MCP parity probes and `.wps` application-declaration fixture cover the path.

- [x] Emit actual top-level Sub/Function bodies in the experimental CLI, with procedure-local scopes, forward calls and declared ByRef modes; remove fabricated reduced-arity procedure overloads.
- [x] Expose the same experimental program compilation through the machine/MCP interface and verify paired diagnostics. `xpscript_ast_validate` calls `AstExperimentalCompiler`, and `CompilerMcpProtocolProbe` compares CLI/MCP success and parser diagnostics.
- [x] Model Optional defaults in syntax/binding/emission instead of generating no-op overloads. Defaults are retained in `ParameterSyntax`, bound in declaration scope and lowered to executable forwarding bodies with writable omitted ByRef storage.
- [x] Preserve Optional flags, parameter modes and default-expression syntax with absolute spans; handle nested default-expression commas in declaration parsing.
- [x] Validate Optional ordering, default types/expression rules and supplied/omitted arguments in the binder; lower omitted ByRef defaults through temporary locals. The AST CLI regressions cover invalid ordering/default types plus executable supplied and omitted argument paths.
- [x] Execute omitted trailing Optional arguments through bound forwarding bodies and fresh writable storage; preserve supplied ByRef aliases and Function results, with repeat-call and generated-name collision regressions.
- [x] Retain explicit empty argument slots as distinct syntax nodes with absolute spans; bind positional omissions only to declared Optional parameters and emit only the forwarding shapes actually used. Verify leading, middle, trailing and all-empty slots, required parameters following Optional parameters, supplied ByRef aliases and XPS2004 call spans against executable legacy/AST coverage and published main.
- [x] Verify positional default-expression evaluation and side effects against published main. Named default evaluators run at omitted argument positions before later supplied arguments; forwarding parameters give omitted ByRef values fresh writable storage. The executable optional-default-order fixture covers earlier/later side effects, explicit suppression and nested defaults. Further declaration/default-name validation remains open; executable defaults are supported, so do not impose C# constant-only rules.
- [x] Complete Optional declaration validation, default-name binding, overload conflicts and diagnostic source mapping; verify legacy and machine-interface parity before closing Optional migration. AST rejects invalid ordering/default declarations, binds earlier-parameter defaults, resolves overload precedence and preserves mapped diagnostics; the executable optional fixtures and MCP parity probe cover the migrated contract.
- [x] Fix the legacy Optional/ByRef runtime comparison failure (`'long' does not contain a definition for 'Value'`): literal defaults receive fresh compatibility-reference storage, and the generic ByRef postprocessor preserves those wrappers instead of nesting them. The saved fixture verifies supplied aliases, repeated defaults, string and Boolean values in legacy and AST execution.
- [x] Include the published main executable under `publish/xpscript/win-x64` in compatibility verification. The preserved main baseline reproduces the documented Optional/ByRef runtime failure; the corrected branch and AST path pass. Baseline SHA-256 and observed results are recorded in `docs/ast-published-main-verification.md`; subsequent migrated behavior is compared against this baseline and baseline defects are distinguished from migration regressions.
- [x] Replace regex-based function-result rewriting with structural return-local lowering, preserving execution after result assignments; cover Exit Function, fall-through, recursion and generated-name collisions in the experimental CLI.
- [x] Implement GoTo and labels with case-insensitive targets restricted to the same Sub or Function; reject cross-procedure targets and allow the same label name in separate procedures.
- [x] Lower If/ElseIf/Else, While, Do and Select to flat conditional branches in procedures with GoTo, keeping labels in procedure scope. Permanent Language FullTest CLI fixtures cover nested transfers and Function/Sub scope.
- [x] Remove experimental GoSub support by user decision (2026-10-08). Explicitly diagnose it as unimplemented and recommend Sub/Function calls instead; retain a rejection regression. GoSub parity is no longer a migration requirement.
- [x] Complete nested For/ForAll GoTo, local initialization across jumps and source-mapped target diagnostics before closing GoTo migration. GoTo now requires source and target to be in the exact same For/ForAll scope; jumps into, out of, or between nested levels are rejected with source-mapped diagnostics. Same-scope loop jumps remain executable fixtures.
- [x] Preserve scalar Static procedure-local storage without initializers instead of rewriting Static to Dim. Published main and corrected AST print 1 then 2 for repeated calls. Permanent Language FullTest coverage verifies procedure/overload isolation, defaults and ByRef mutation.
- [x] Complete Static arrays, lists, objects and explicit initialization semantics. Static arrays with `ReDim`/`ReDim Preserve`, Static Lists, Static Objects, parameter-dependent first-reached initializers and explicit initializers are implemented. Static initialization is guarded by a per-storage lock/state: concurrent callers serialize and recursive initialization receives a deterministic diagnostic.
  Static Object storage is now supported and starts as Nothing. `static-object.xps` verifies one creation across repeated calls; `New Object()` now emits ExpandoObject instead of the nonexistent XpObject. Full initializer semantics remain open.
  Explicit Object initializers are verified by `object-initializer-lifetime.xps`: Dim reruns construction, while Static retains a cleared reference without rerunning construction. Static initializers execute once at the first reached declaration, with parameter/earlier-local dependencies supported. `static-initializer-scope.xps` verifies skipped declarations, one-time side effects and first-call values.
  AST now supports initializer-free Static arrays, Lists, Variant storage and user-class instances through persistent generated fields. Scalar Static initializers are also supported as an AST capability extension and covered by `static-initializer-error.xps`; Linux static compiles that fixture but emits an empty line, so AST's printed `3` is an intentional semantic improvement. Published 0.9.3 rejects Static List syntax with XPS1012, so that part is also an AST capability extension rather than a legacy parity claim.
- [x] Restore typed writable List aliases in AST ForAll using the shared List runtime/iterable contract. Published main and AST match the permanent ListTag, numeric/string assignment, nested-loop and snapshot regression; AST no longer enumerates KeyValuePair objects as values.
- [x] Lower IsElement on declared keyed List access to ContainsTag without reading the element first. Present/missing tags match published main; the permanent AST regression also verifies one evaluation of a tag function with a side effect.
- [x] Bind Erase on declared Lists to the existing runtime's Erase/Clear methods, without reading a removed/missing element. Permanent regression matches published main for single-tag removal, whole-list clearing and removals during snapshot iteration.
- [x] Preserve one-dimensional zero-based array declaration lengths, string defaults and Erase for fixed CLR arrays. AST and published main now match the permanent array-storage regression. Nonzero lower bounds, multidimensional and dynamic array lifecycle semantics remain separate migration work.
- [x] Preserve dynamic array type and allocation lifecycle through `ReDim`, `ReDim Preserve` and `Erase`. AST retains the CLR element type, deallocates dynamic arrays on `Erase`, and `UBound` after deallocation now raises the same allocation error as published main. Multidimensional and nonzero-bound arrays remain separate work.
- [x] Implement multidimensional array lifecycle. Fixed and dynamic CLR-backed arrays preserve rank, explicit lower/upper bounds, element type, reads, writes, `Erase`, `LBound` and `UBound` (including rank two and three); dynamic `ReDim`/`ReDim Preserve` now copies overlapping values through the shared runtime.
  Fixed, dynamic and multidimensional lowering now share `IXPScriptArrayStorage`; CLR-backed arrays use the managed adapter and dynamic AST arrays use the same runtime contract.
  - [x] Define one AST array storage abstraction for fixed, dynamic and multidimensional arrays.
  - [x] Preserve array rank, lower/upper bounds and element type in syntax and bound symbols; fixed rank-two/rank-three runtime fixtures verify the lowering.
  - [x] Emit fixed multidimensional and one-dimensional reads, writes and `Erase` through the shared array runtime.
  - [x] Lower one-dimensional and multidimensional `ReDim`, `ReDim Preserve` and `Erase` through the shared runtime abstraction.
- [x] Complete `Option Base` and lower-bound semantics. Fixed and dynamic one-dimensional and multidimensional arrays now honor `Option Base 1` and explicit lower bounds.
  Remaining architecture work: extend lower-bound parity tests to additional legacy array forms as they migrate.
  - [x] Apply `Option Base` to dynamic one-dimensional and multidimensional `ReDim` bounds without changing fixed-array behavior; permanent fixtures cover both.
  - [x] Emit `LBound` and `UBound` for supported one-dimensional and multidimensional storage, including explicit dimensions and rank-two/rank-three arrays.
- [x] Complete List ByRef aliases/copy-back, initialized declarations and parameters/returns. Sub and Function calls support matching typed List elements and ForAll aliases, including ordinary ByRef parameter forwarding. Named Function wrappers capture receiver/tag/value in argument order and copy back in finally. List parameters preserve element types and ByVal/ByRef reference behavior; Variant-returned Lists can initialize typed declarations. IsElement validates keyed access and checks dynamically supplied Lists without reading missing elements. Negative CLI/MCP fixtures preserve XPS2004/XPS2001 and source locations for incompatible parameter/initializer element types. Fixed/dynamic array Erase is covered separately.
  Permanent CLI/MCP fixtures verify Function results, alias mutation, parameter replacement, reference initializers/returns, exceptional copy-back, tag evaluation once and invalid argument/element types. Typed List parameter overloads preserve separate Static storage identities.
- [x] Implement Resume and On Error execution in the AST bound tree. Protected instruction graphs support automatic continuation, disabling, handlers, retry, next-statement and label transfers. Error normalization, Err and Error$ reuse the existing runtime. CLI fixtures verify retries, loops, List aliases and handler-error propagation; MCP validates the same sources and unknown handler-label diagnostics.
  - [x] Add bound error-handler state for `On Error Resume Next`, `On Error GoTo 0` and handler labels. Runtime lowering now uses protected instruction graphs.
  - [x] Lower protected statements with source-mapped `try/catch` and resume targets.
    - [x] Assign stable IDs to bound statements for source mapping and future resume transfers.
  - [x] Implement `Resume`, `Resume Next` and `Resume label` transfer semantics.
  - [x] Model resume targets as typed bound state with optional label; runtime transfer uses the captured current/next instruction or label target.
  - [x] Add CLI/MCP fixtures and compare against the legacy compiler. `samples/nested-resume-targets.xps` compiles normally and prints identical results in AST and legacy. The new combined retry/label fixture additionally exposes missing legacy `__ls_stmt_after_*` labels; AST execution is verified independently.
- [x] Preserve implicit With/member statement semantics in the AST path. `With` establishes a lexical receiver scope, leading-dot member access binds to that receiver (including nested scopes), and CLI/MCP fixtures verify valid execution plus diagnostics for access outside a scope.
- [x] Replace AST XPImage/Object substitutions with the existing image runtime implementation. XPImage now binds constructors, static loaders, read-only properties and overloaded instance methods to the shared runtime, without legacy fallback. Native API metadata and compiler/MCP documentation are derived from that runtime's public source in `AstImageSupport`; no second image implementation or package-copy path is introduced.
  - [x] Add catalog-driven NuGet dependency builds for AST-generated runtime code, including Magick.NET references, SDK-selected native assets and exact direct/transitive third-party notices. The same path also handles SQLite and future catalog entries; package-free programs remain independent of addons. `GeneratedDependencyProbe` executes image/SQLite native code and checks notices on fresh/cached builds.
  - [x] Verify native AST image creation, file/byte/base64 loading, encoding, editing, statistics/histogram, ByVal clone, ByRef replacement, Static retention, returned references and exception/early-exit cleanup. `xpimage-runtime.xps` and `xpimage-variant-return.xps` are executable Language FullTest fixtures; MCP verifies construction/member/property errors with source spans. `xpimage-parity.xps` compares real normal and AST compilation/execution.
  - [x] Preserve hexadecimal color strings by removing the obsolete global hash-character deletion; validate `As New` declarations as their constructed type in the shared source validator. Stage relative source documents in the isolated publish workspace so normal image compilation retains source mapping instead of failing with missing-source diagnostics.
- [x] Migrate native CSV documents, headers, rows and columns into AST using the existing CSV engine.
  - [x] Import typed members, constructors, global CSV functions and indexes; preserve shared ByVal references, ByRef replacement, typed results, Static storage and typed ForAll iteration.
  - [x] Preserve file encoding/BOM handling, JSON round trips and FromBytes reference replacement/evaluation order, including Call statements. Normal and AST compilation/execution agree on the existing native CSV sample and the dedicated file/reference parity fixture.
  - [x] Include CSV runtime only on actual use; omit document/file runtime for CsvEscape-only programs and all CSV runtime for unused names in strings/comments. Pure CSV output has no unrelated addon dependencies. Spreadsheet interop now uses the shared native helper in AST (see spreadsheet migration below).
  - [x] Add permanent CLI execution, MCP validation and source-mapped negative fixtures to the existing Language/Runtime test paths.
- [x] Migrate XPSpreadsheet, XPWorksheet, XPCell and XPRange into AST using the existing package-free XLSX engine.
  - [x] Bind typed members, overloads, constructors and read-only/private-setter properties; preserve initialized declarations, typed parameter/return values, ByVal sharing, ByRef replacement and Static references.
  - [x] Lower CSV interoperability through existing helpers, including inferred Variant results and static FromWorksheet; include only the engines actually used, without extra NuGet addons or changes to notices.
  - [x] Share native LSArray indexing/bounds and ForAll enumeration; verify Range.Values rather than substituting empty enumeration. Preserve script-relative XLSX paths and external-workbook/version guards.
  - [x] Compare seven existing demos through real normal/AST compilation and execution; verify byte/style/formula round trips, malformed/oversized ZIP inputs, external-file protection and mapped CLI/MCP negatives in permanent Language/Runtime coverage.
  Verified on Linux on 2026-10-13: compiler Release build (0 errors), complete AST CLI compilation/execution probe, spreadsheet runtime/parity/safety probe, MCP protocol probe, binder, lexer, emission, compilation-unit and existing spreadsheet capability probes. Static FromWorksheet also has permanent normal/AST executable parity coverage.
- [x] Migrate native XML document, node/element, attribute, collection and DTD-validation objects into AST using the existing secure runtime.
  - [x] Share source-derived native metadata with CSV; preserve inheritance, optional arities, static/instance checks, read-only members and typed collection iteration/indexing.
  - [x] Preserve shared ByVal references, ByRef replacement, typed results, element-to-node upcasts, Static storage and in-place LoadXml; XML node element operations are runtime-checked rather than unrestricted dynamic fallback.
  - [x] Keep DTD/entity resolver restrictions and 8 MiB/1 MiB limits; include XML runtime only on use and only the escape method for escape-only programs, without external addons/notices.
  - [x] Verify focused Parent/With regressions, native DOM/DTD samples, normal/AST output parity and source-mapped CLI/MCP diagnostics in permanent Language/Runtime tests. Qualified Parent members are not rewritten as class inheritance references; actual Parent/Me class references retain their contract.
- [x] Apply production reserved-identifier validation in the AST path. Legacy and AST reject user identifiers beginning with compiler-owned `__xps`; the focused fixture verifies the diagnostic, source span and MCP rejection while valid lowering names remain covered by the CLI probe.
- [x] Restrict dynamic call fallback to genuinely dynamic receivers; invalid calls to declared procedures must produce binder overload diagnostics.
- [x] Reject unmatched declared overloads with XPS2004 even when dynamic member binding is enabled; regress arity, argument type, ByRef mode and source span while retaining Variant member calls.
- [x] Restrict unresolved member fallback to Variant receivers rather than every object-backed user type; regress both properties and calls with XPS2009 and member source spans.
- [ ] Replace experimental runtime placeholders (including HTTP/TLS, image, UI, error and environment helpers) with existing runtime implementations before claiming sample compatibility.
  - [x] Replace AST `DateAdd`/`DateDiff` placeholders with the existing `XPScriptRuntime` implementations; `date-builtins-next.xps` verifies day and month intervals.
  - [x] Bind AST `DatePart`, `Weekday`, `MonthName` and `WeekdayName` to existing `XPScriptRuntime` implementations; `date-parts-next.xps` verifies their migrated behavior.
  - [x] Bind AST `DateValue`, `TimeValue` and three-argument `TimeNumber` to existing `XPScriptRuntime` implementations; `date-values-next.xps` verifies their migrated behavior.
  - [x] Bind AST `Hour`, `Minute` and `Second` to existing `XPScriptRuntime` implementations; `time-parts-next.xps` verifies their migrated behavior.
  - [x] Replace the AST `ShellId` zero placeholder with the shared empty-program error contract; `shell-id-next.xps` verifies it. Actual process-launch integration remains target-runtime work because the AST generated assembly does not yet include `CrossPlatformRuntimeSource`.
  - [x] Replace Application's NewLine-only ExpandoObject with typed native namespaces, shared argument/state runtime and conditional Debug/SystemLog/Registry/Crypto/Secrets/Log/Audit services. Preserve argument snapshots/bounds, metadata, readonly guards and ExitCode; use existing packaging for literal executable metadata. No extra NuGet dependency or permissive credential/host stub.
  - [x] Add permanent normal/AST runtime parity, isolated registry/crypto/credential/host guards, debug/assembly-metadata/ExitCode tests and binder/MCP mapped diagnostics. Shared Linux user-registry storage respects absolute XDG_CONFIG_HOME with its existing default fallback. Native Variant snapshot writes reuse shared array/dynamic index storage.
    Verified on Linux on 2026-10-13: Release build (0 errors), complete AST CLI compilation/execution probe, normal/AST Application runtime probe, complete MCP protocol and binder probes (including encrypted-secret facade target checks), lexer/emission/compilation-unit probes, and existing Application isolation/concurrency/debug probes. With Application is an explicitly tested AST extension; normal compilation still rejects that namespace-value form. Credentials are not read or written by the migration tests.
  - [x] Migrate XPHttpClient/XPHttpRequest/XPHttpResponse and response part/file APIs to the existing native runtime. Source-derived metadata binds overloads, typed returns and read-only fields; JSON/authentication facades delegate to existing helpers. Remove HTTP sample-name substitution and the always-valid TLS placeholder; error capture uses the shared runtime exception contract.
  - [x] Verify real normal/AST HTTP/TLS execution, Strict/self-signed certificate rejection (hostname/expiry), private-network blocking, header controls, redirect behavior, request/response limits, fractional timeout and disposal. Conditional inclusion omits unused HTTP/TLS without unrelated addons. Async/UIForm use whole-file legacy fallback; those language/UI migrations remain separate.
  - [x] Add permanent Language/Runtime HTTP/TLS execution plus binder/MCP mapped diagnostics, including synchronized invalid native constructors.
  - [x] Include the full shared `XPScriptRuntimeSource` and `TypeCoercionRuntimeSource` in AST output. Remove the nested scalar runtime substitute and JSON/array/HTTP rewrites targeting it. Scalar conversions, Null/Empty printing, type inspection, string helpers, environment lookup and numeric loop ranges use the shared runtime; CLI/MCP and normal compilation cover `shared-runtime.xps`. Target-specific UI and extended helpers remain separate migration work.
  - [x] Replace AST JSON document/object/array/element placeholders with `NativeJsonRuntimeSource` and shared object-reference JSON converters. CLI execution and MCP cover parse/stringify, nested mutation, Unicode/null, invalid input/indexes, typed parameter/return and ByRef replacement, array iteration and source-mapped unknown members. JSON runtime code is no longer selected by sample variable names.
    `json-runtime.xps` matches normal compilation output. Typed JSON Function return via Set is additionally supported by AST; legacy currently rejects that form, so `json-reference.xps` is an AST capability test rather than a full legacy-parity claim.
  - [ ] Migrate the remaining AST AI scaffolds (`XpAi`/`XpAiTool`) through the native XPAI route. The current `AddFunction`, `AddParameter`, tool lookup/removal and request execution are placeholders; native behavior is assembled by `AiPromptSchemaRuntimePostProcessor` and includes callback allowlisting, JSON schema, sessions and sanitized failures. Do not replace this with a reduced metadata-only stub.
    - [x] Remove the obsolete generated `XpAi`/`XpAiTool` metadata placeholders; AST XPAITool/XPAi declarations now resolve to the native `XPScriptAi`/`XPScriptAiTool` runtime emitted by the XPAI route. Function lookup/removal and request-execution coverage remain open under this parent item.
    - [x] Add AST request-execution error coverage against a closed local endpoint; the native XPAi path returns a sanitized error without leaking the configured API key. Provider success/tool-loop and streaming coverage remain open.
    - [x] Map typed `XPAiResponse` locals to `XPScriptAiResponse` instead of generating the invalid user-class name `XpXPAiResponse`; the provider-success fixture now compiles with a typed response.
    - [x] Expose `XPAIToolFunction` as a native AST type with metadata and parameter lifecycle methods; the lifecycle fixture now compiles and runs through registration, lookup and removal.
    - [x] Expose the documented `XPAIToolCall` callback type and its tool/function/call/argument/session properties; the lifecycle callback now binds with a typed `XPAIToolCall` parameter.
    - [x] Bind the remaining documented XPAITool function lookup, request-context and JSON metadata methods; the lifecycle fixture executes those native APIs.
    - [x] Correct the `XPAi.NewRequest` catalog contract to the native parameterless reset method; the lifecycle fixture invokes it successfully.
    - [x] Bind documented `XPAiResponse.HasJsonResult` and `ResultJson` properties supplied by the native prompt/schema postprocessor; the typed response fixture now compiles with the JSON-result contract.
    - [x] Bind AST overloads for native `XPAi.Complete(messages, model)` and `XPAi.Stream(messages, callback, model)`; `xpai-overloads-next.xps` compiles both signatures against the native runtime.
    - [x] Bind `XPAi.StreamCallback(callback, context)` so streaming callbacks can receive native callback context; the overload fixture compiles the two-parameter callback contract.
  - [ ] Investigate the remaining AST `XpNotesHttp` compatibility scaffold separately from the migrated native HTTP path; its no-op headers/proxy methods and empty `Get`/`Post` responses must either delegate to the native runtime or remain an explicit legacy fallback with diagnostics.
    - [ ] Initial migration probe found the existing `JsonHttpCompatibilityRuntimeSource` can replace the placeholder runtime, but the AST path currently ends in an internal compiler failure; keep the fallback until generated-type binding and the Notes JSON compatibility surface are migrated together.
    - [x] Bind the standalone Notes JSON navigator, object, array and element surfaces through the shared native-object metadata importer; notes-json-native-next.xps verifies parsing, element lookup, name and value semantics.
    - [x] Bind NotesHTTPRequest construction, properties and header methods to the compatibility runtime; notes-http-native-next.xps verifies the local surface. Full request/response and json-http parity remain open.
  - [x] Bind AST `MsgBox` to the shared console/runtime return contract; `msgbox-next.xps` verifies the documented `1=OK` result while desktop UI postprocessing remains target-specific.
  - [x] Investigate AST `ProcedureCounter`: it is not a documented built-in, but `samples/core-language.xps` defines it as a user `Static Function`; the fallback was removed after structural module-level Static Function binding was implemented and verified by `static-module-function-next.xps`.
    - [x] Add module-level `Static Function` parsing and remove the generated `ProcedureCounter` fallback; `static-module-function-next.xps` verifies the real user function and persistent local state.
  - [x] Replace the AST `AstOpen`/`AstClose`/`Loc` scaffold with the shared `LSFileRuntime`; `file-runtime-next.xps` verifies missing input-path errors and the initial output position.
  - [x] Add AST `Get` lowering through `LSFileRuntime.GetValue`; `file-get-next.xps` verifies typed binary `Put`/`Get` string round-trip and file length.
  - [x] Bind AST `Print #` and `Line Input #` to `LSFileRuntime`; `file-text-next.xps` verifies text round-trip.
  - [x] Add the shared `LSFileRuntime.InputChars` byte-count API and bind AST `Input$`; `file-input-chars-next.xps` verifies binary byte reads and Latin-1 decoding.
  - [x] Bind AST `Seek` to `LSFileRuntime.SeekSet` so binary `Input$` can reposition an open handle.
  - [x] Bind AST byte-range `Lock`/`Unlock` to `LSFileRuntime`; `file-lock-next.xps` verifies the interval and closed-handle error contract.
  - [x] Bind AST `InputBox` overloads to the shared console input runtime; `inputbox-next.xps` verifies prompt input while desktop-specific dialog behavior remains target-specific.
  - [x] Bind AST `ChDrive` to the shared platform-aware file runtime; `chdrive-next.xps` verifies the missing-drive error contract.
- [ ] Remove sample-specific identifiers and type coercions from binders/emission and runtime scaffolding.
  - [x] Remove the unconditional `matrix` local-symbol scaffold; undeclared `matrix` now follows normal XPS2008 binding diagnostics.
  - [x] Remove global `ForAll` alias scaffolding; undeclared array/string/dynamic aliases are now created in the loop body scope and existing aliases retain their normal symbol semantics.
  - [x] Remove the broad assignment-line global-symbol fallback; undeclared assignment targets now follow normal binder diagnostics instead of becoming module globals.
  - [x] Remove hard-coded `Person` class construction/emission; user classes now use the generic AST class lowering path.
  - [x] Gate legacy `NotesHTTPRequest`/AI compatibility helper emission on actual source usage instead of emitting those helpers for every AST compilation.
  - [x] Remove duplicate `Process`/`Session`/`Request` Expando globals; `AstApplicationSupport` already emits their native `.State` targets. Application runtime execution remains covered by its existing fixture.
  - [x] Remove the unused global `Debugger` Expando fallback; AST debug statements use their dedicated lowering path.
  - [x] Remove global `XPJson*` Expando fallbacks; JSON objects/documents/arrays are emitted through the native AST JSON runtime and semantic type mappings.
  - [x] Remove the unused global `Response` Expando fallback; supported AST HTTP responses are typed native values and web callback compatibility remains a separate legacy route.
  - [x] Remove global `Database`/`Document` Expando fallbacks; AST database/document surfaces use typed native objects or remain explicit legacy fallbacks.
  - [x] Remove `UIForm`/`NotesSession` AST Expando fallbacks; both are already routed explicitly to legacy compatibility.
- [x] Investigate regex-based module-field generation for source `Const` declarations; module-level constants now come from `ApplicationConstDeclarationSyntax`, while procedure-level constants retain an explicit compatibility fallback until local constant symbols are lowered. `module-const-syntax-next.xps` verifies both forms.
    - [x] Accept procedure-level `Const` declarations in the AST syntax/binding path and add executable coverage using `samples/const-statements.xps`; regex-based value/type lowering remains open for structural migration.
  - [x] Investigate regex-based `BuildUnknown`/`BuildReady`/`BuildRunning`/`BuildDone` and `BuildState` globals; they are documented enum support, and AST binding plus qualified enum access preserve implicit sequencing. Regression coverage is complete.
    - [x] Confirmed `BuildState` is documented enum support and fixed AST binding plus qualified enum access; generated enum members now preserve implicit sequencing after explicit values (`BuildRunning = BuildReady + 1`).
  - [x] Investigate regex-based array module-field generation from `Dim name(...) As Type`; AST now registers module arrays with typed array semantic symbols and initializes managed carriers with source lower bounds. Full typed shared module-array storage is covered by the module-array regression.
    - [x] Verify public/private module arrays, `ReDim Preserve`, indexing and source lower bounds through `samples/module-arrays.xps` in the AST CLI regression (`MODULEARRAY=50`, `MODULEBOUNDS=1:4`, `MODULEFIXED=two`).
    - [x] Confirmed and regression-tested default-private module-level `Dim` arrays without `Public`/`Private`; `module-array-default-private-next.xps` verifies typed storage and indexing while preserving default-private semantics.
  - [x] Investigate remaining unconditional `SEARCH_DEPTH`, `Command`, `Platform`, and `CurDir` generated globals; they are documented built-ins with runtime-backed values. Focused runtime-global and Notes-search regressions are complete; the broader Notes namespace remains separate.
    - [x] Classified `Platform`, `Command`, and `CurDir` as documented built-ins; `runtime-globals-next.xps` verifies non-empty runtime-backed values. AST now exposes the authoritative Notes values `SEARCH_BREADTH = 1723` and `SEARCH_DEPTH = 1724`, verified by `notes-search-constants-next.xps`; full Notes namespace binding remains separate.
- [ ] Migrate class declarations, inheritance and object lifecycle into the AST bound tree.
  - [x] Bind and emit scalar typed instance Sub/Function bodies through shared procedure binding, control-flow validation and BoundMethodEmitter. Verify per-instance fields, private sibling calls, Me, scalar ByRef, recursion, parameter shadowing and default String fields; class bodies no longer disappear into empty scaffolding for this supported subset.
  - [x] Keep private fields/methods out of external symbol lookup and prevent class declarations from becoming fake module globals. Verify private access, cross-class field leakage and source-mapped body failures through CLI/MCP. AST canonicalizes case-insensitive member names; the old compiler's mixed-case failure is an explicit AST-only capability fixture.
    - [ ] Extend class method signatures/storage to object, array/List, Optional and Static cases, with scalar Optional/Object/user-reference Optional/Optional ByRef/List and ByRef List replacement plus Optional ByRef List, Optional array and Optional ByRef array and Static root methods, derived scalar method bodies, direct Parent.member dispatch, compatible scalar overrides, concrete user-class references, plain Object/Variant/XPJsonObject/XPJsonArray/XPJsonDocument/XPJsonElement/XPXmlNode/XPXmlDocument/XPXmlElement/XPCsvDocument/XPSpreadsheet/XPHttpClient/XPHttpRequest/typed List and ordinary array parameters, and Static List/array storage covered; other native signatures remain open.
      - [x] Verified the implemented class-method subset: scalar Optional, Object/user-reference Optional and Optional ByRef, Static methods, List and array signatures, plus their executable CLI regressions in `run-ast-cli.ps1`; remaining work is the broader native/derived/Parent/member-signature matrix described above.
  - [x] Extend native class-method parameter binding to `XPHttpResponse`; the executable `class-method-http-response-next.xps` regression verifies typed method emission and `Nothing` argument conversion.
  - [x] Extend the same binding to `XPHttpPart` and `XPHttpFile`; `class-method-http-parts-next.xps` verifies both typed parameters and `Nothing` conversion.
  - [x] Extend native class-method binding to `XPHttpPartCollection` and `XPHttpFileCollection`; `class-method-http-collections-next.xps` verifies both collection parameters and `Nothing` conversion.
  - [x] Cover `XPHttpResponse` as a class-method result; `class-method-http-response-return-next.xps` verifies typed return emission and `Nothing` assignment.
  - [x] Cover `XPHttpPart` and `XPHttpFile` as class-method results; `class-method-http-parts-return-next.xps` verifies typed returns and `Nothing` assignment.
  - [x] Cover `XPHttpPartCollection` and `XPHttpFileCollection` as class-method results; `class-method-http-collections-return-next.xps` verifies typed returns and `Nothing` assignment.
- [ ] Bind property Get/Let/Set bodies and indexed accessors. Derived scalar/reference property getters/setters, indexed getter/setter coverage, compatible scalar/indexed property overrides, base-typed virtual/override dispatch, and direct Parent scalar/reference property access now have focused coverage; remaining lifecycle/ownership integration is tracked separately.
    - [x] Execute scalar non-indexed Get/Let bodies through shared procedure binding/emission, including implicit incoming property-name values and declared setter parameters; enforce accessor visibility and read/write permissions in CLI/MCP.
      Verified 2026-10-13: release build, real normal/AST execution parity with implicit incoming-value syntax, explicit setter/With/casing AST capability execution, complete AST CLI and MCP suites, and binding/lexer/emission/compilation probes passed. Normal compilation currently rejects explicit non-indexed setter parameters, so those remain a separate AST capability test rather than a parity claim.
    - [ ] Add indexed accessor binding and object/reference Get/Set with complete return/assignment semantics.
      - [x] Bind and emit scalar indexed Get/Let, multiple typed indexes, internal/With access, permissions and evaluation order; verify normal/AST getter parity and existing indexed scalar sample.
        Verified 2026-10-14: latest release build, full AST CLI and MCP suites, binding/lexer/emission/compilation probes, real ByVal getter parity and AST execution of the existing scalar indexed sample. Index snapshots, read/write/private permissions, arity/type/signature diagnostics and class storage in On Error emission are exercised. The existing unmodified-index sample compiles normally but has a legacy runtime failure; its successful AST execution is a capability claim, not full legacy parity.
      - [ ] Complete explicit ByRef index alias/copy-back semantics and Optional index signatures.
        - [x] Preserve named scalar local/parameter/class-field aliases through Get/Let, mixed modes, shared aliases, forwarding and exceptional exits; verify mapped mode/type/location errors in CLI/MCP.
          Verified 2026-10-14: release build, executable alias/RHS/exception fixture, full AST CLI and MCP suites, and focused binding/lexer/emission/compilation probes passed. The full fixture's normal compilation still fails in legacy Resume/Goto label scope, so it is an AST capability test; existing ByVal getter parity remains verified. Disk-full test failures were resolved by removing two regenerable old image builds and a failed package cache, then rerunning the complete suites with existing signed packages and unchanged source.
        - [ ] Lower complex ByRef index locations.
          - [x] Preserve typed List-element aliases through value-returning ByRef calls with generated copy-back wrappers; verified by `byref-list-element-call-next.xps` in AST CLI/MCP.
          - [x] Verify qualified scalar user-class fields and implicit With fields through direct aliases, shared targets and exceptional exits; keep accessor-backed properties non-addressable.
            Verified 2026-10-14: release build, executable qualified-field Get/Let/shared-alias/With/exception coverage, complete AST CLI and MCP suites, and binding/lexer/emission/compilation probes passed. The smaller field capability fixture also executes successfully in AST; normal compilation succeeds but legacy execution fails on numeric-field Value access, so no full legacy parity is claimed. Field provenance is retained through optional-call rewrites, with no duplicate emitter or copy-back engine.
          - [x] Verify typed uninitialized scalar Private/Public module-variable targets, shared aliases, defaults, shadowing and exceptional exits with mapped CLI/MCP diagnostics.
            Verified 2026-10-14: release build, executable Get/Let/shared-alias/forwarding/class-access/shadowing/RHS/exception regressions, complete AST CLI and MCP suites, and binding/lexer/emission/compilation probes passed. One shared module-storage description supplies symbols and emitted fields with no extra runtime/addons. A failing MCP header-offset regression was fixed by masking rather than slicing module prefixes and now runs before the broad CLI suite. The smaller module fixture compiles normally but still fails in legacy numeric Value access at runtime, so it remains an AST capability claim rather than full legacy parity.
        - [x] Lower initialized Date and user-class reference module storage, including Nothing, New, constructor arguments, ByRef replacement and field access.
          Verified 2026-10-17: release compiler and executable AST fixtures `initialized-module-reference-next.xps`, `initialized-module-reference-new-next.xps`, `initialized-module-reference-args-next.xps` and `initialized-module-date-next.xps` passed with outputs `9`, `12`, `13` and `01/03/2026 00:00:00`.
          - [x] Verify typed array/List entries and ForAll aliases as indexed ByRef targets, with shared identity, bounds, evaluation order and exceptional exits.
            Verified 2026-10-14: release build, actual CLI compilation/execution in ordinary/flat/error-flow emission, full AST CLI and MCP suites, focused binding/lexer/emission/compilation probes and production-helper runtime guards passed. Shared List cells retain aliases through assignment/growth and detach removed/recreated entries; array references check exact type/rank/bounds before access, including non-zero lower bounds for rank 1–8 and reference-valued/scalar slots. Fixed-array size/offset and dynamic/Static array storage regressions discovered by the new tests were corrected in shared emitters; Static now runs early. Normal/AST shared List storage parity passed; the new indexed element fixture still has a legacy numeric Value runtime failure and remains an AST capability claim, not full legacy parity. No extra package or copy-back engine was added.
          - [ ] Lower initialized/other module-variable types, including Date and references.
        - [x] Implement trailing Optional index signatures/defaults, retaining a required Let value parameter.
          Verified 2026-10-15: Release build, complete AST CLI and MCP protocol suites, focused binding/lexer/emission/compilation probes and executable Optional-index fixtures passed. Instance forwarding reuses OptionalCallLowering and the shared bound emitter; omitted ByRef defaults have fresh storage, supplied indexes retain direct/shared aliases, and defaults bind in instance scope with earlier indexes. Coverage includes zero supplied indexes, implicit scalar defaults, side effects, setter RHS order, With/internal access, array/ForAll targets, exceptional exits and mapped declaration/type/name/arity diagnostics. Language FullTest runs these first. Normal compilation still rejects Optional declarations in lowered property wrappers with XPS1012, so these remain AST capability regressions rather than full legacy parity. Object Set and Optional ordinary class methods remain open.
        - [x] Lower explicit empty index argument slots for Optional indexed accessors; required slots retain mapped argument diagnostics. Verified by `class-property-empty-slot-next.xps` in AST CLI/MCP.
      - [x] Bind non-indexed Object Get/Set with Nothing, replacement and alias identity.
        Initial Object-only CLI/MCP coverage is `object-property-set.xps`; typed and indexed coverage follows below.
      - [x] Preserve declared Object/user-class reference types through fields, accessor parameters, results and wrappers; bind indexed Get/Set with scalar/Optional/ByRef indexes.
        Verified 2026-10-15: Release build, complete AST CLI and MCP protocol suites, all four focused compiler probes, the strengthened reference/error-flow execution fixture and seven mapped negative fixtures passed.
        `indexed-object-runtime.xps` exercises typed chained access, default Nothing results, reference identity and mutation, incoming-value snapshots, private/internal/With access, instance state, duplicate/array/ForAll index aliases, RHS ordering and accessor/caller error flow. The existing indexed-object sample executes in AST but normal compilation still reports an LSRef<Script.Box> mismatch, so no full legacy parity is claimed. PropertyAssignmentKind survives bound member/Optional rewrites; wrong assignment modes, scalar Set values, incompatible concrete classes, invalid Set declarations and non-writable function targets have paired CLI/MCP XPS2001 regressions in Language FullTest.
      - [x] Bind direct/transitive user-reference upcasts through a validated compilation-local hierarchy and shared conversion classification.
        Verified 2026-10-15: Release build, complete AST CLI and MCP suites, all four focused compiler probes, inherited-reference execution and six mapped negative fixtures passed. ByRef/array/List invariance and independent scope metadata are covered by the early binder regression.
        `inherited-reference-runtime.xps` covers identity, mutation, fields/initializers, getter results, Optional/ByRef indexes, setter-value snapshots, Nothing, With and exceptional exits. Binder probes retain ByRef/array/List invariance and scope isolation; early CLI/MCP negatives cover downcasts, siblings, unrelated reused class names, unknown bases and direct/transitive cycles. Native conversions retain the existing classifier, and ordinary derived member bodies/advanced constructor signatures/lifecycle dispatch remain separate work. Normal compilation still rejects the fixture's Optional property wrapper, so full legacy parity is not claimed.
      - [x] Bind typed JSON/XML reference accessors through shared semantic signatures; verify shared mutation, element-to-node upcasts, Optional/ByRef indexes, Nothing, setter snapshots and errors in CLI/MCP and Language FullTest.
      - [x] Bind typed CSV/spreadsheet reference accessors using shared mappings; verify every document/collection/row/column/workbook/worksheet/cell/range signature, mutation, Nothing, Optional/ByRef indexes, snapshots, error exits and mapped CLI/MCP guards in Language FullTest.
      - [x] Bind typed HTTP client/request/response/part/file/collection accessors through the shared native mappings; verify local HTTP/TLS, multipart identity, scalar Optional/ByRef indexes, Nothing, setter snapshots, exceptional exits and explicit Dispose, with signature-only runtime selection and paired CLI/MCP diagnostics in Language/Runtime FullTest.
      - [x] Bind XPImage Get factories through the shared procedure/image scope: returned results survive, discarded/replaced images and failed results are recycled, Nothing and scalar Optional/ByRef indexes work. Verify signature-only addon selection/notices and paired mapped diagnostics in Language/Runtime FullTest.
      - [x] Enable XPImage Set with ByVal cloning and borrowed ByRef value snapshots; typed image fields clone on store, recycle replaced/cleared values and preserve old storage if cloning fails. Verify Optional forwarding, receiver/RHS order, temporary and exceptional cleanup, Nothing, instance isolation and paired CLI/MCP diagnostics in Language/Runtime FullTest (`image-property-set.xps`).
      - [x] Clear/recycle typed XPImage fields on explicit Delete via the existing virtual/base chain, including private and inherited storage. Verify alias visibility, source/instance isolation, Nothing, repeated deletion and active error-handler cleanup in the early image harness, MCP and Language/Runtime FullTest (`image-field-delete.xps`).
      - [x] Bind root-class parameterless Sub Delete bodies through the existing destructor syntax and shared procedure emitter. Verify inherited dispatch, private/Me/module access, Exit Sub, protected bodies, re-entry/repeated aliases, errors and caller/image cleanup with early CLI/MCP regressions and simple real normal/AST parity.
      - [x] Bind derived destructor bodies in validated lineage scopes and generate finally-protected override/base cleanup. Verify three levels, private shadowing/access guards, inherited public members and ByRef storage, Me/With, re-entry, early exits and multi-level image/error cleanup in early CLI/MCP and Language/Runtime FullTest; retain simple real normal/AST chain parity.
      - [ ] Complete constructor lifecycle dispatch. Derived child-body failure cleanup, no-user-Delete behavior, generated class-image GC finalization, native module ByRef storage, and ordinary derived method/accessor virtual dispatch now have focused coverage; remaining work is full lifecycle/ownership integration and broader native accessor parity.
  - [ ] Bind constructors/arguments and Parent.New forwarding; finish full lifecycle dispatch.
    - [x] Bind and execute one root Sub New with required explicit scalar ByVal arguments, canonical typed New/Dim storage, shared conversions/body emission and failed-construction image cleanup without Delete. CLI/MCP pair mapped diagnostics; normal/AST parity and Language/Runtime FullTest markers cover the implemented subset.
    - [x] Execute derived scalar ByVal constructors and first-statement Parent.New through shared binding/real base initializers, with mapped position/arity/type/access/name diagnostics and non-virtual failed-construction field cleanup across completed ancestry. Scalar derived method/accessor bodies are covered separately; advanced/native forms and normal/AST parity remain separately tracked from richer image/error capability coverage.
    - [x] Support trailing Optional scalar ByVal constructor defaults through shared Optional lowering and private argument carriers, including dependent defaults, New/Parent.New/implicit base paths, mapped CLI/MCP diagnostics and default-routine image cleanup. FullTest requires Optional constructor markers; normal parity remains full-arity only.
    - [x] Support required scalar ByRef/unmodified constructor parameters with shared address validation/emission and direct New/Parent.New/body-helper aliases. Cover locals/parameters/fields/module storage, array/List/ForAll references, protected/error exits, image cleanup and mapped CLI/MCP diagnostics in Language/Runtime FullTest; retain ByVal legacy parity separately from AST ByRef capability.
    - [x] Mix required scalar ByRef with trailing Optional ByVal constructors through shared Optional lowering and a stack-only managed-reference carrier. Cover dependent/mutating defaults, aliases, supplied snapshots, Parent.New, default/body image failures and paired CLI/MCP diagnostics in permanent Language/Runtime FullTest.
    - [x] Support explicit/implicit scalar Optional ByRef constructor parameters using fresh typed heap array slots in shared Optional lowering and the existing managed-reference carrier. Preserve supplied aliases, dependent defaults, Parent.New/implicit bases and failure/image cleanup; guard Byte boxing and pair CLI/MCP diagnostics in permanent Language/Runtime FullTest.
    - [x] Support required ByVal Object/user-class constructor parameters through existing conversions, semantic signatures and both scalar Optional carrier paths. Verify snapshot replacement/shared mutation, New/Parent.New, earlier-member defaults, narrow concrete-reference normal parity, borrowed-object versus owned-image cleanup and paired CLI/MCP diagnostics in permanent Language/Runtime FullTest.
    - [x] Support required explicit/implicit ByRef Object/user-class constructor slots through shared identity/address validation and direct managed references. Cover duplicate/live aliases, replacement/clearing, fields/With/receiver order, Nothing-valued storage, Parent.New/nested forwarding, scalar Optional carriers, failure persistence and borrowed versus owned images in CLI/MCP and permanent Language/Runtime FullTest.
    - [x] Support trailing Optional ByVal Object/user-class constructor defaults through shared lowering and semantic carriers, including implicit Nothing, fresh/dependent defaults, New/Parent.New, mixed ByRef storage and borrowed image ownership. Pair CLI/MCP errors and require permanent Language/Runtime FullTest markers.
    - [x] Support Optional ByRef Object/user-class references using fresh StrongBox<T> cells with shared lowering, including dependent defaults, nested forwarding and image borrowing/error cleanup.
    - [x] Preserve concrete array/List types and support reference elements, writable List ForAll aliases and initializer-free typed reference module storage through shared binding/emission, including protected control flow and lower bounds.
    - [x] Verify constructor overload selection and signature-specific emission across CLI/MCP and permanent FullTest.
      Verified 2026-10-17: Release build, complete AST CLI, final constructor harness, MCP protocol suite and all four compiler probes passed. Focused execution covers identity/numeric/reference specificity, Nothing, Optional arity overlaps, fresh omitted ByRef cells, private/internal calls, Parent.New, Optional caller rewriting, canonical casing and generated-tag name collisions. Normal/AST scalar overload parity matches; overloaded image construction preserves old targets and cleans failed fields/locals without user Delete. Duplicate signatures/names, ambiguity, no-match and private access have paired CLI/MCP codes/lines. Language FullTest requires AST_OVERLOAD_CONSTRUCTORS_OK; Runtime FullTest retains the strengthened image-constructor fixture.
    - [x] Verify Variant and shared JSON/XML/CSV/spreadsheet/HTTP constructor signatures, Optional typed defaults/cells and borrowed image cleanup in CLI/MCP and permanent FullTest.
      Verified 2026-10-17: Release build, complete AST CLI, final MCP protocol suite, all four compiler probes and the updated emission regression passed. Focused execution covers live duplicate Variant/native aliases, writes surviving failures, fresh Empty/Nothing/New defaults, dependent native references, Parent.New XML upcasts, HTTP explicit disposal and distinct XPHttpPart/XPHttpFile overloads despite their common CLR representation. Borrowed Variant images survive constructor failure/Delete while owned field snapshots are recycled. Narrow normal/AST ByVal Variant parity matches. Language FullTest requires native/Variant constructor markers; the new Runtime FullTest constructor block was executed successfully, and image coverage requires AST_IMAGE_VARIANT_CONSTRUCTORS_OK. XPImage-declared parameters, collection signatures and compatibility module storage retain their existing guards.
    - [x] Verify typed List constructor parameters through shared semantic parameter resolution and Optional cells, preserving invariant element types, aliases, Parent.New and borrowed image elements in CLI/MCP and permanent FullTest.
      Verified 2026-10-17: Release build, complete AST CLI, final constructor harness, final MCP protocol suite and all four compiler probes passed. Focused execution covers List replacement and duplicate aliases, ByVal sharing/snapshots, fresh Nothing/factory/dependent defaults, default/body failures preserving old targets and prior mutations, Parent.New, user/native elements and erased Object/Variant List overloads. Optional emission retains StrongBox<LSList<XpBox>>. Borrowed List image elements survive construction failure/Delete while owned fields are recycled; the expiration assertion also rejects missing observations. Narrow normal/AST ByVal mutation parity matches; legacy ByVal Set-to-Nothing remains unsupported. New Language/Runtime FullTest markers cover List constructors and image borrowing, and the updated Runtime constructor block was executed successfully. Ordinary class-method List signatures, scalar XPImage/array parameters and compatibility module addresses remain guarded.
    - [x] Retain ranked array parameter syntax and Optional/default source metadata, with explicit CLI/MCP migration guards and invalid-rank/array-List diagnostics. Execution remains deferred pending shared physical bounds and whole-array ByRef storage.
      Verified 2026-10-17: Release build, complete AST CLI and MCP protocol suites and all four compiler probes passed. Parser coverage exercises ranks 1–8, untyped spans, Optional factory defaults and invalid bounded/rank/List forms. Language FullTest runs explicit mapped constructor/procedure/method guards first. No array storage/emission behavior was changed; executable array constructor signatures remain open.
    - [x] Unify AST array local/Static carriers and physical bounds, including signed bounds, Variant identity/replacement validation, shared element references, String defaults/Erase and ordinary/flat/protected allocation. Constructor array parameter execution remains guarded pending parameter/default/ByRef lowering.
      Verified 2026-10-17: Release build, complete AST CLI and final MCP protocol suites, all four compiler probes, narrow normal/AST bounds/String Erase parity, native image Byte-array decoding and the actual updated Runtime FullTest array/constructor block passed. New regressions cover signed lower bounds, Variant sharing and replacement, wrong rank/element conversions preserving old storage, duplicate ByRef element aliases, Static/GoTo/protected allocation and String Erase. Shared module-scan masking prevents procedure arrays from generating duplicate globals; native Byte vectors normalize only at the API boundary without changing source bounds. Production helpers retain rank 1–8 reference/type/layout checks. The complete CLI suite exceeded the old 15-minute budget locally, so Language FullTest retains all coverage with a bounded 20-minute CLI allowance. Whole-array constructor parameter/default/ByRef lowering remains the next increment.
      - [ ] Initialized compatibility module addresses beyond scalar literals, DateNumber, explicit-Nothing references and numeric-literal user-class factories; finish full lifecycle dispatch. Scalar XPImage constructor, procedure and root class-method signatures, native module ByRef storage, and HTTP indexed accessor runtime typing are covered; broader native/accessor parity and full lifecycle dispatch remain open.
        - [x] Extend user-class module initializer arguments to numeric/string literals and verify `New Box("hello")` through the AST CLI regression.
        - [x] Preserve earlier module-field identifier arguments in user-class initializers and verify `New Box(seed)` through the AST CLI regression.
        - [x] Translate `Nothing`/boolean constructor arguments in initialized user-class module references and verify `New Box(Nothing)`/`New Box(True)` through the AST CLI regression.
    - [x] Lower whole-array constructor/procedure/class-method parameters through the shared System.Array carrier, including ByRef mutation, physical-bound preservation, semantic method registration, Optional omission as unallocated storage, CLI/MCP validation and invalid-rank/array-List guards. Scalar XPImage signatures remain a separate follow-up work item.
    Reference-cell/storage verification 2026-10-17: Release build, complete AST CLI (including constructor/image/normal-parity coverage), MCP protocol suite and all four compiler probes passed. Fresh StrongBox<T> defaults preserve exact user-class types, supplied aliases and nested/Parent.New forwarding. Executable storage coverage includes lower bounds, multidimensional/dynamic arrays, persistent Static array storage, Lists, List ForAll aliases, module references and replacement on errors; CLI/MCP pair incompatible element types. Focused failures exposed Object's shadowed type metadata and zero-sized fixed Static arrays; both are corrected through shared storage/allocation paths. Former storage/Optional deferrals are positive regressions, with the obsolete negative manifest entries removed. Native/Variant/List/array signatures and overloads remain open; broader GC and ordinary derived method migration are not closed by this increment.
    Optional-ByVal reference constructor verification 2026-10-16: final Release build, focused fresh/default/Nothing/upcast/dependent/Parent.New/mixed-carrier cases, full final constructor harness, image borrowing/cleanup regression, complete final AST CLI, final MCP protocol suite and all four compiler probes passed. The strengthened failure test captures the error outside Resume Next and verifies preserved old targets plus persistent ByRef writes. CLI/MCP pair incompatible defaults and earlier-only name scope, retaining Optional ByRef reference/native/List deferrals. Language/Runtime FullTest require Optional-reference constructor markers; full OS matrices remain CI coverage. An initial Application metadata SDK build returned nonzero despite reporting zero build errors; isolated metadata/Application reruns and the complete final CLI passed without an Application compiler change. Application now runs early under the test-failure feedback rule. XPSCRIPT_TEST_OUTPUT_ROOT redirects main CLI/constructor/image outputs when workspace space is limited; existing output defaults are unchanged. Legacy omitted-constructor parity is not claimed.
    Required-ByRef reference constructor verification 2026-10-16: Release build, focused explicit/implicit alias/replacement/field/With/Nothing/default/Parent.New cases, Object and concrete-class image failure/borrowing cleanup, complete AST CLI, MCP protocol suite and all four compiler probes passed. CLI/MCP pair exact-type/address errors and retained reference element/alias/module/Optional deferrals. Language/Runtime FullTest require ByRef-reference constructor markers; full OS matrices remain CI coverage. Normal compilation of the simple concrete-reference ByRef fixture prints 0 instead of AST's 7, so capability is not claimed as legacy parity. Ordinary concrete-class module signature migration remains separate; image helpers use existing Object/Variant binding.
    Required-ByVal reference constructor verification 2026-10-16: Release build, focused identity/snapshot/upcast/default/order/Parent.New cases, image borrowing versus owned-field cleanup, concrete-class normal/AST parity, complete AST CLI, MCP protocol suite and all four compiler probes passed. CLI/MCP pair incompatible scalar/class/downcast arguments and retained ByRef/Optional/native/Variant/unknown/List and ordinary-method deferrals. Language/Runtime FullTest require reference-constructor markers; full OS matrices remain CI coverage. Broader Object/default/error behavior is AST capability, not complete legacy parity.
    Optional-ByRef constructor verification 2026-10-16: Release build, focused explicit/implicit/default/alias/Parent.New cases, Byte-zero freshness regression, image default/body failure cleanup, complete final AST CLI, MCP protocol suite and all four compiler probes passed. Former deferral fixtures are now success regressions with paired literal/type/default/Parent.New diagnostics. Language/Runtime FullTest require Optional ByRef constructor markers; complete OS matrices remain CI coverage. Legacy ByRef and omitted-constructor parity are not claimed.
    Mixed-constructor verification 2026-10-16: Release build, focused alias/default/Parent.New and image failure fixtures, complete AST CLI, MCP protocol suite and all four compiler probes passed. The final strengthened scalar error-code assertion also passed in the focused constructor harness and MCP reruns. Permanent Language/Runtime FullTest require mixed-constructor markers; complete OS matrices remain CI coverage. At that increment Optional ByRef remained mapped XPS3001; legacy ByRef and omitted-constructor parity are not claimed.
    ByRef-constructor verification 2026-10-16: Release build, focused reference/forwarding/alias/error and image cleanup fixtures, full AST CLI, MCP protocol suite and all four compiler probes passed. Normal compilation builds the classic-declaration simple fixture but fails at runtime with a long.Value error, so ByRef is AST capability coverage; existing valid ByVal parity remains green. Language/Runtime FullTest require the ByRef constructor markers; complete OS matrices remain CI coverage.
    Optional-constructor verification 2026-10-16: final Release build, focused omission/order/naming and image default-failure tests, full AST CLI, MCP protocol suite and all four compiler probes passed. Full-arity Optional declaration execution matches normal compilation; omitted constructor arguments remain an AST capability because normal compilation still requires full arity. Language/Runtime FullTest now require Optional constructor markers; complete OS matrices remain CI coverage.
    Derived-constructor verification 2026-10-16: Release build, focused constructor/normal parity and three-level private/inherited image failure tests, full AST CLI, MCP protocol suite and all four compiler probes passed. Language/Runtime FullTest require the derived-constructor markers; complete cross-platform matrices remain CI coverage.
    Verified 2026-10-16: Release build, focused constructor execution and normal/AST parity, full AST CLI (including image constructor cleanup), MCP protocol suite and all four binding/lexer/emission/compilation probes passed. Language/Runtime FullTest now require constructor markers; cross-platform CI remains the authority for those full matrices.
  Verified 2026-10-13: release compiler build, normal/AST class-method execution parity, full AST CLI and MCP protocol suites, and focused binding/lexer/emission/compilation probes passed. The environment required regenerating test caches and reusing existing signed Magick packages after an external NuGet repository-signature endpoint returned HTTP 503; signature validation remained enabled.
  AST now emits class fields, scalar instance methods and Get/Let bodies, inherited fields, root scalar ByVal construction and virtual/overridden `Delete` cleanup, and `Delete object` clears the caller reference; `class-lifecycle.xps` verifies the lifecycle scaffolding. Advanced method signatures, derived-class bodies, indexed/object accessors, advanced constructor signatures still need bound-tree lowering before this parent item can close.
- [x] Add missing AST Variant-to-Byte conversion through the existing runtime `CByte` helper; the executable regression now matches published scalar conversion behavior.
- [x] Preserve uninitialized Variant Nothing semantics for `Is Nothing`; AST now emits null storage instead of an ExpandoObject and matches published output.
- [ ] Preserve original source spans through all preprocessing and map parser/binder errors through the shared diagnostic contract.
  - [x] Investigate `Option Base` removal and route-attribute removal for source-span shifts; both preserve line structure and have focused regressions.
  - [x] Audit every length-changing AST preprocessor rewrite in the current AST pipeline for the same span and declaration-preservation risk; declaration removals preserve line structure and transformed diagnostics use the shared span mapper.
  - [x] Add column-mapping regressions for rewrites that insert or remove characters on a line (`Sleep`, `MkDir`, file statements, `ReDim`, and related lowering).
    - [x] `Sleep` and `MkDir` now have focused diagnostic-column regressions; `MkDir` lowering preserves the original argument column.
    - [x] File-lock rewrites have focused coverage; `Put`, `Open`, `Close`, `Lock` and `Unlock` now use the shared transformed-span mapping and preserve their argument columns.
    - [x] `ReDim` has a focused bound-expression diagnostic regression through the shared transformed-span mapping.
    - [x] `Print #` and `Kill` have focused argument/path diagnostic-column regressions through the shared transformed-span mapping.
    - [x] `Input$` and `Line Input` have focused argument/target diagnostic-column regressions through the shared transformed-span mapping.
    - [x] `Error$` expression replacement has a focused following-expression diagnostic-column regression.
    - [x] Reviewed `p.`/`Print p.` rewrites; they are deliberate no-op compatibility lowerings for unsupported legacy property statements, so they suppress the statement by design and do not introduce a diagnostic span to remap.
  Optional declaration validation, explicit unsupported-feature guards and binder/default-expression errors now emit source-mapped CompileDiagnostic values. Bound emitter failures are wrapped with the originating bound statement span and mapped to the emission diagnostic category; generated control-flow mappings retain the original file/line. Roslyn backend diagnostics now consume the same generated-source mapping contract.
  - [x] Map Roslyn code-generation diagnostics through generated `#line` directives, including original file, start position and end position; `GeneratedDependencyProbe` covers the mapped backend failure path.
  Parser diagnostics now use the same mapped path; control-flow and generated-source diagnostics remain open.
  Label, duplicate-label and forbidden loop-entry diagnostics now also carry source spans.
  - [x] Parser diagnostics from transformed source now map through the shared transformed-span mapper; `preprocessor-parser-error.xps` covers a malformed `Open` statement.

Initial implementation: expression conversions and a method-body emitter for assignment, calls, return, If, For, ForAll, While, Do and Select Case are covered by `tests/ast-emission`. Bound syntax spans and an opt-in `#line`/mapping result are also available. `tests/ast-compile-probe` drives a real XPscript `Sub Main` through declaration parsing, binding and C# compilation. See `docs/ast-csharp-emitter.md` for supported nodes and remaining integration work. The phase remains open until the full bound tree, mapping and targets are connected to production compilation.

- [ ] Implement C# generation from bound nodes.
- [x] Keep emitter free of XPscript parsing logic. Parsing and binding are completed before `BoundStatementEmitter`/`BoundExpressionEmitter`; the emitters consume bound nodes only.
- [ ] Reuse existing runtime APIs rather than reimplementing runtime behavior.
- [x] Preserve generated source mapping for AST control-flow and emitter diagnostics. Bound emission failures now retain the originating statement span and are mapped through the shared CLI/MCP diagnostic contract; generated `#line` mappings remain deterministic for emitted control flow.
- [ ] Preserve target-specific generated code requirements.
- [x] Preserve generated runtime integration. AST output includes the shared compatibility/runtime fragments required by the bound emitter and preserves the existing third-party notice generation path.
- [x] Keep Roslyn as the .NET backend and final generated-C# validator. AST-generated C# is compiled through the shared in-process Roslyn backend.
- [x] Add snapshot/golden tests for generated C# where useful. The AST emission probe now compares stable method, ByRef, instance-method and Select Case output against exact golden strings before compiling and executing the generated C#.

## Phase 9: Diagnostics and tooling

- [x] Attach source span and source file to all AST parser diagnostics. `SyntaxDiagnosticException` maps parser spans through the original source, and the CLI/MCP probe verifies filename, line, column and span identity.
- [x] Attach source span and source file to AST semantic diagnostics. Binder errors are mapped through the original source; the MCP probe verifies XPS2008, semantic category, filename, line, column and end span for an unresolved variable.
- [x] Map new AST diagnostics into existing `CompileDiagnostic`. Parser, binder and explicit unsupported-feature diagnostics all use the shared mapped payload consumed by CLI and MCP.
- [x] Preserve stable XPS diagnostic codes where applicable. The permanent probe asserts stable XPS1012 and XPS2008 identities across the AST diagnostic surfaces.
- [x] Add new codes only with documented categories and tests. AST diagnostics use entries from `CompilerDiagnosticCatalog`; the focused CLI/MCP probe exercises the syntax and semantic categories with stable-code assertions.
- [x] Update compiler machine interface together with normal compiler diagnostics. `CompilerMachineInterfaceProbe` verifies compile/validate results, stable codes and categories, source mapping, JSON compatibility and include traces for the shared diagnostic contract.
- [x] Update MCP compiler output together with normal compiler diagnostics. AST MCP now marks structured compilation failures as protocol tool errors while preserving the shared diagnostic payload.
- [x] Add paired diagnostic tests that feed the same invalid XPscript source through CLI compilation and MCP/AI machine compilation and verify equivalent diagnostic code, source span and meaning. The permanent compiler probe covers normal/debug CLI and MCP paths.
- [x] Expose syntax-tree information internally in a form suitable for future IDE tooling. The internal `SyntaxTreeDumper` provides a deterministic traversal of syntax nodes, kinds and spans; lexer/parser tests cover the representation without expanding the public compiler API.
- [ ] Investigate parser APIs for completion, hover, go-to-definition and rename.
- [ ] Investigate incremental parsing only after the non-incremental AST is stable.

## Phase 10: Compatibility harness

- [x] Build a corpus of existing XPscript samples and test programs. `tests/ast-compat` contains focused fixtures plus the repository sweep over representative `samples/` and `demo/` programs; the sweep records compatible successes and existing failures separately.
- [ ] Run each compatible source through legacy and AST compiler paths.
- [ ] Compare compile success/failure.
- [ ] Compare diagnostic locations and categories.
- [ ] Run representative corpus cases through the MCP/AI machine compiler as well as the normal compiler.
- [x] Compare CLI and machine/MCP compile success/failure for the same source. `CompilerMcpProtocolProbe` exercises shared valid fixtures through legacy and AST MCP validation and checks the AST CLI parity fixture.
- [x] Compare CLI and machine/MCP structured diagnostics for the same source. The permanent protocol probe compares diagnostic code, source, line, column, span and message identity for normal/debug CLI and MCP paths.
- [ ] Compare observable runtime output.
- [ ] Compare generated target behavior for console.
- [ ] Compare web target behavior.
- [ ] Compare desktop/Avalonia behavior.
- [ ] Compare Android/mobile behavior as support matures.
- [ ] Migrate the Android target branch (`origin/android-runtime-uiform-mobile`) into the AST compiler path. Preserve the Android ARM64/x64 hosts, generated APK project setup, logcat/error routing, CLI device/build/deploy workflow, Android-specific runtime objects and UIForm/Avalonia behavior; add focused compiler/runtime probes and move stable coverage into the permanent FullTest workflows.
- [ ] Add target selection to `ast-compile` and compile AST-generated units through the Windows, Linux, macOS, browser-WASM/web and Android project backends; the current target matrix probe verifies normal project metadata only and must not be treated as full AST cross-target compilation.
- [x] Add explicit compatibility fixtures for LotusScript-like edge cases. The AST compatibility suite now has permanent executable fixtures for `Rem`/apostrophe comments, `_` line continuation and `Application.NewLine`.
- [x] Record intentional behavior changes instead of silently changing semantics. `docs/ast-intentional-behavior.md` documents explicit legacy fallback, mapped unsupported-feature diagnostics and deferred runtime semantics.

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

- [x] Add an internal/experimental switch for AST compilation. The `ast-compile` CLI command and `xpscript_ast_validate` MCP tool select the AST path explicitly.
- [x] Migrate expressions first. The AST migration began with lexer, precedence parsing, binding and expression emission before statement/control-flow expansion.
- [x] Migrate simple statements. Declarations, assignments, `Set`, calls, `Print`, `Return` and `Error` are represented in the AST path with focused fixtures.
- [ ] Migrate control-flow statements.
- [x] Migrate procedures. `Sub` and `Function` declarations, parameters, returns, Optional/ByRef handling and procedure-scoped labels are covered by AST fixtures.
- [ ] Migrate classes/properties.
- [ ] Migrate remaining special compiler constructs.
- [x] Run focused tests after every migrated feature. AST CLI fixtures and paired MCP probes are extended with each migrated feature.
- [x] Run compiler subsystem suites before broader full tests. Release builds, AST CLI probes and MCP protocol probes are run as the focused gate.
- [ ] Make AST path the default only after compatibility gates pass.
- [x] Keep temporary legacy fallback only where explicitly documented. `AstObjectSupportRegistry` owns the remaining object fallback routes; XPImage now uses native AST compilation.
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

- [x] A lexer can tokenize a representative XPscript expression with exact spans. Lexer fixtures cover token families, malformed tokens, comments, strings and line continuations.
- [x] An expression parser produces a deterministic syntax tree. Syntax dump and parser regressions cover representative expressions.
- [x] Operator precedence is covered by focused tests.
- [x] `Not RunCommand(...)` is represented as a unary expression over the call result.
- [x] `RunCommand(...) = False` is represented as a comparison expression.
- [x] Both forms can be bound to boolean semantics without relying on C# parsing to determine XPscript meaning.
- [x] The proof of concept can emit valid C# for those expressions.
- [x] Existing production compilation remains unchanged unless the experimental path is explicitly selected. Normal `compile` continues through the legacy driver; `ast-compile` is opt-in.
- [x] The proof of concept demonstrates that both CLI compilation and MCP/AI machine compilation can call the same AST front-end without duplicating parsing or semantic logic. Both surfaces call `AstExperimentalCompiler`.

## Definition of done for full migration

- [ ] All supported XPscript syntax is represented by the XPscript syntax tree.
- [x] Parsing no longer depends on line-by-line regex/string transformation in the opt-in AST path; lexer and declaration/statement parsers consume tokens.
- [x] Semantic analysis operates on XPscript syntax/bound nodes in the opt-in AST path.
- [x] C# generation operates from the bound representation in the opt-in AST path; legacy transpiler migration remains open.
- [ ] Roslyn is used as the C#/.NET backend, not as the primary XPscript parser/type checker.
- [ ] Existing supported runtime behavior passes compatibility tests.
- [ ] Normal compiler and machine/MCP interfaces use the same AST/binder implementation and report consistent diagnostics.
- [ ] Legacy parser/transpiler implementation is removed or reduced to explicitly justified compatibility code.
- [x] Compiler architecture and extension guidance are documented in `docs/ast-compiler-extension-guide.md`.
- [ ] Update the compiler/package version to `0.9.4 Beta` only after all AST migration gates above pass and published `0.9.3 Beta` compatibility verification is recorded.

## Additional compatibility regressions discovered during reverification

- [x] Fix legacy Optional preprocessing for nested supplied calls using AST lexer token boundaries rather than a flat-parenthesis regex. The executable fixture covers nested same-procedure calls, inner commas, repeated calls, and unchanged strings/comments; legacy and AST produce 123, 1333, 246 and the original literal. Published main still reproduces missing-argument/generated-expression errors.
- [x] Reject a bare procedure name where a scalar argument is required before AST execution. Remove the regex global-symbol scaffolding that treated function-result assignments as globals, preserve XPS2003 in AST CLI and MCP, and retain the optional-bare-procedure-argument.xps parity regression against published main.

Reverification scope on 2026-10-08: the last five completed implementation increments by commit order are typed-receiver fallback (6c1a9553), Optional declaration syntax (17089047), trailing Optional forwarding (e22b27c9), legacy Optional ByRef storage (cf5181b7), and explicit Optional slots (df7ffa5d). These are exercised by the binder, lexer and executable CLI probes in Language FullTest. The new side-effect regression extends this audit beyond the earlier literal-only defaults.
Audit result: all five scoped increments passed their focused probes and the complete Windows Language FullTest. MCP protocol validation also passed, including executable default expressions; this remains production-path validation rather than experimental AST exposure.

- [x] Replace the AST Print file-output regex with lexer token boundaries so commas inside string literals are not misclassified. The print-parentheses-literal fixture now matches published main output.
Nested-call verification completed: focused legacy/AST execution, production MCP validation and the complete Windows Language FullTest passed. Published main reproduces the original compile failure; its SHA-256 remains unchanged.
