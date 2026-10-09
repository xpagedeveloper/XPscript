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
- [ ] Preserve public compiler diagnostics where compatibility is required.
- [ ] Keep normal compiler and machine/MCP compiler behavior synchronized.
- [ ] CLI compilation and MCP/AI machine compilation must consume the same lexer, parser, AST, semantic binder and bound tree; do not create a second language implementation for machine compilation.
- [ ] Every migrated syntax/semantic feature must be verified through both normal compilation and MCP/AI machine compilation when that feature is exposed through the machine interface.
- [ ] Any AST diagnostic change must be verified in both human-readable compiler output and the structured machine/MCP diagnostic contract.
- [ ] Every AST diagnostic must preserve the original XPscript source file, absolute source span, 1-based line and column.
- [ ] Unknown variables, functions, properties and methods must be reported by the XPscript semantic binder before Roslyn when the symbol/type information is available.
- [ ] Diagnostic parity is a release gate across normal CLI compilation and MCP/AI machine compilation.
- [ ] Diagnostic parity must be verified both with debug diagnostics disabled and enabled; debug mode may add generated-code/debug details but must not change the XPscript diagnostic code, meaning, source file, line, column or source span.
- [x] Do not remove legacy parsing code until its replacement has dedicated tests and integration coverage.
- [x] Prefer small commits organized by compiler phase or language feature.

## Phase 1: Syntax model

- [x] Define a common `SyntaxNode` base abstraction.
- [ ] Define source spans on every syntax node.
- [x] Define `CompilationUnitSyntax`; `DeclarationParser.ParseCompilationUnit` returns it with source-derived spans.
- [ ] Define declaration node hierarchy.
- [x] Define statement node hierarchy.
- [x] Define expression node hierarchy.
- [x] Define type syntax nodes; `TypeSyntax` preserves the declared type token and span.
- [x] Define parameter syntax nodes; `ParameterSyntax` preserves Optional/ByRef/ByVal/default metadata. Call arguments are represented by expression lists with source spans.
- [ ] Decide how comments/trivia are represented.
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
- [ ] Parse application-level declarations.
- [ ] Parse target-specific entry points.
- [ ] Validate block terminators structurally rather than with transpiler state.

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
- [ ] Connect compilation-unit emission to the shared experimental CLI/machine compilation path with real procedure bodies and runtime integration.

- [x] Emit actual top-level Sub/Function bodies in the experimental CLI, with procedure-local scopes, forward calls and declared ByRef modes; remove fabricated reduced-arity procedure overloads.
- [ ] Expose the same experimental program compilation through the machine/MCP interface and verify paired diagnostics.
- [ ] Model Optional defaults in syntax/binding/emission instead of generating no-op overloads.
- [x] Preserve Optional flags, parameter modes and default-expression syntax with absolute spans; handle nested default-expression commas in declaration parsing.
- [ ] Validate Optional ordering, default types/expression rules and supplied/omitted arguments in the binder; lower omitted ByRef defaults through temporary locals.
- [x] Execute omitted trailing Optional arguments through bound forwarding bodies and fresh writable storage; preserve supplied ByRef aliases and Function results, with repeat-call and generated-name collision regressions.
- [x] Retain explicit empty argument slots as distinct syntax nodes with absolute spans; bind positional omissions only to declared Optional parameters and emit only the forwarding shapes actually used. Verify leading, middle, trailing and all-empty slots, required parameters following Optional parameters, supplied ByRef aliases and XPS2004 call spans against executable legacy/AST coverage and published main.
- [x] Verify positional default-expression evaluation and side effects against published main. Named default evaluators run at omitted argument positions before later supplied arguments; forwarding parameters give omitted ByRef values fresh writable storage. The executable optional-default-order fixture covers earlier/later side effects, explicit suppression and nested defaults. Further declaration/default-name validation remains open; executable defaults are supported, so do not impose C# constant-only rules.
- [ ] Complete Optional declaration validation, default-name binding, overload conflicts and diagnostic source mapping; verify legacy and machine-interface parity before closing Optional migration.
  AST now rejects required parameters after Optional and defaults on non-Optional parameters, binds a default expression against earlier parameters in the same procedure, and gives explicit overloads precedence over generated Optional forwarding; source mapping remains open.
- [x] Fix the legacy Optional/ByRef runtime comparison failure (`'long' does not contain a definition for 'Value'`): literal defaults receive fresh compatibility-reference storage, and the generic ByRef postprocessor preserves those wrappers instead of nesting them. The saved fixture verifies supplied aliases, repeated defaults, string and Boolean values in legacy and AST execution.
- [x] Include the published main executable under `publish/xpscript/win-x64` in compatibility verification. The preserved main baseline reproduces the documented Optional/ByRef runtime failure; the corrected branch and AST path pass. Baseline SHA-256 and observed results are recorded in `docs/ast-published-main-verification.md`; subsequent migrated behavior is compared against this baseline and baseline defects are distinguished from migration regressions.
- [x] Replace regex-based function-result rewriting with structural return-local lowering, preserving execution after result assignments; cover Exit Function, fall-through, recursion and generated-name collisions in the experimental CLI.
- [x] Implement GoTo and labels with case-insensitive targets restricted to the same Sub or Function; reject cross-procedure targets and allow the same label name in separate procedures.
- [x] Lower If/ElseIf/Else, While, Do and Select to flat conditional branches in procedures with GoTo, keeping labels in procedure scope. Permanent Language FullTest CLI fixtures cover nested transfers and Function/Sub scope.
- [x] Remove experimental GoSub support by user decision (2026-10-08). Explicitly diagnose it as unimplemented and recommend Sub/Function calls instead; retain a rejection regression. GoSub parity is no longer a migration requirement.
- [ ] Complete nested For/ForAll GoTo, local initialization across jumps and source-mapped target diagnostics before closing GoTo migration.
  AST now reports a stable semantic diagnostic for a jump into a nested For/ForAll instead of exposing a Roslyn CS0159; actual loop-entry lowering and declaration analysis remain open.
- [x] Preserve scalar Static procedure-local storage without initializers instead of rewriting Static to Dim. Published main and corrected AST print 1 then 2 for repeated calls. Permanent Language FullTest coverage verifies procedure/overload isolation, defaults and ByRef mutation.
- [ ] Complete Static arrays, lists, objects and explicit initialization semantics. These forms are explicitly rejected in the experimental binder until implemented. Published main confirms Static arrays are dynamic storage that must be allocated with `ReDim` before access; preserve that lifecycle rather than emitting an eagerly allocated CLR array.
  AST now supports initializer-free Static arrays, Lists and Variant/object storage through persistent generated fields; class instances remain open. Published 0.9.3 rejects Static List syntax with XPS1012, so that part is an AST capability extension rather than a legacy parity claim.
- [x] Restore typed writable List aliases in AST ForAll using the shared List runtime/iterable contract. Published main and AST match the permanent ListTag, numeric/string assignment, nested-loop and snapshot regression; AST no longer enumerates KeyValuePair objects as values.
- [x] Lower IsElement on declared keyed List access to ContainsTag without reading the element first. Present/missing tags match published main; the permanent AST regression also verifies one evaluation of a tag function with a side effect.
- [x] Bind Erase on declared Lists to the existing runtime's Erase/Clear methods, without reading a removed/missing element. Permanent regression matches published main for single-tag removal, whole-list clearing and removals during snapshot iteration.
- [x] Preserve one-dimensional zero-based array declaration lengths, string defaults and Erase for fixed CLR arrays. AST and published main now match the permanent array-storage regression. Nonzero lower bounds, multidimensional and dynamic array lifecycle semantics remain separate migration work.
- [x] Preserve dynamic array type and allocation lifecycle through `ReDim`, `ReDim Preserve` and `Erase`. AST retains the CLR element type, deallocates dynamic arrays on `Erase`, and `UBound` after deallocation now raises the same allocation error as published main. Multidimensional and nonzero-bound arrays remain separate work.
- [ ] Implement multidimensional and nonzero-bound array shapes. AST now rejects both forms explicitly instead of treating them as one-dimensional zero-based arrays; permanent regressions cover each rejection against published main acceptance.
- [ ] Implement `Option Base` and lower-bound semantics. AST now rejects `Option Base` explicitly instead of silently assuming zero-based arrays.
- [ ] Complete List ByRef aliases/copy-back, initialized declarations and parameters/returns. The ByRef alias audit produces a C# ref-property error in AST; published main compiles but leaves the element unchanged (1 instead of the expected mutation to 11). Initialized List declarations are explicitly rejected instead of discarded. Complete IsElement argument validation, dynamically supplied Lists and array Erase as part of API migration.
  AST now reports the unsupported List-element ByRef case during binding rather than leaking a generated C# error; temporary storage and copy-back remain open.
- [ ] Preserve legacy Resume and On Error semantics in the AST bound tree. AST now rejects these forms explicitly instead of lowering them to `AstNoOp`; verify each syntax against a published-main fixture before implementing error-handler state.
- [ ] Preserve implicit With/member statement semantics in the AST path. AST now rejects `With` and implicit member access explicitly instead of rewriting them to `AstNoOp`; compare valid legacy samples before implementing receiver scopes.
- [ ] Replace AST XPImage/Object substitutions with the existing image runtime implementation. AST now rejects XPImage explicitly instead of converting it to `Object`; `FromBytes`, `FromBase64`, `Load` and `New XPImage(...)` still require runtime integration.
- [x] Apply production reserved-identifier validation in the AST path. Legacy and AST reject user identifiers beginning with compiler-owned `__xps`; the focused fixture verifies the diagnostic, source span and MCP rejection while valid lowering names remain covered by the CLI probe.
- [x] Restrict dynamic call fallback to genuinely dynamic receivers; invalid calls to declared procedures must produce binder overload diagnostics.
- [x] Reject unmatched declared overloads with XPS2004 even when dynamic member binding is enabled; regress arity, argument type, ByRef mode and source span while retaining Variant member calls.
- [x] Restrict unresolved member fallback to Variant receivers rather than every object-backed user type; regress both properties and calls with XPS2009 and member source spans.
- [ ] Replace experimental runtime placeholders (including HTTP/TLS, image, UI, error and environment helpers) with existing runtime implementations before claiming sample compatibility.
- [ ] Remove sample-specific identifiers and type coercions from binders/emission and runtime scaffolding.
- [ ] Migrate class declarations, inheritance and object lifecycle into the AST bound tree. AST now rejects class source explicitly instead of compiling null/dynamic placeholders that fail at runtime.
- [x] Add missing AST Variant-to-Byte conversion through the existing runtime `CByte` helper; the executable regression now matches published scalar conversion behavior.
- [x] Preserve uninitialized Variant Nothing semantics for `Is Nothing`; AST now emits null storage instead of an ExpandoObject and matches published output.
- [ ] Preserve original source spans through all preprocessing and map parser/binder errors through the shared diagnostic contract.
  Optional declaration validation, explicit unsupported-feature guards and binder/default-expression errors now emit source-mapped CompileDiagnostic values; remaining AST diagnostics still need the same treatment.
  Parser diagnostics now use the same mapped path; control-flow and generated-source diagnostics remain open.
  Label, duplicate-label and forbidden loop-entry diagnostics now also carry source spans.

Initial implementation: expression conversions and a method-body emitter for assignment, calls, return, If, For, ForAll, While, Do and Select Case are covered by `tests/ast-emission`. Bound syntax spans and an opt-in `#line`/mapping result are also available. `tests/ast-compile-probe` drives a real XPscript `Sub Main` through declaration parsing, binding and C# compilation. See `docs/ast-csharp-emitter.md` for supported nodes and remaining integration work. The phase remains open until the full bound tree, mapping and targets are connected to production compilation.

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
- [ ] Update the compiler/package version to `0.9.4 Beta` only after all AST migration gates above pass and published `0.9.3 Beta` compatibility verification is recorded.

## Additional compatibility regressions discovered during reverification

- [x] Fix legacy Optional preprocessing for nested supplied calls using AST lexer token boundaries rather than a flat-parenthesis regex. The executable fixture covers nested same-procedure calls, inner commas, repeated calls, and unchanged strings/comments; legacy and AST produce 123, 1333, 246 and the original literal. Published main still reproduces missing-argument/generated-expression errors.
- [x] Reject a bare procedure name where a scalar argument is required before AST execution. Remove the regex global-symbol scaffolding that treated function-result assignments as globals, preserve XPS2003 in AST CLI and MCP, and retain the optional-bare-procedure-argument.xps parity regression against published main.

Reverification scope on 2026-10-08: the last five completed implementation increments by commit order are typed-receiver fallback (6c1a9553), Optional declaration syntax (17089047), trailing Optional forwarding (e22b27c9), legacy Optional ByRef storage (cf5181b7), and explicit Optional slots (df7ffa5d). These are exercised by the binder, lexer and executable CLI probes in Language FullTest. The new side-effect regression extends this audit beyond the earlier literal-only defaults.
Audit result: all five scoped increments passed their focused probes and the complete Windows Language FullTest. MCP protocol validation also passed, including executable default expressions; this remains production-path validation rather than experimental AST exposure.

- [x] Replace the AST Print file-output regex with lexer token boundaries so commas inside string literals are not misclassified. The print-parentheses-literal fixture now matches published main output.
Nested-call verification completed: focused legacy/AST execution, production MCP validation and the complete Windows Language FullTest passed. Published main reproduces the original compile failure; its SHA-256 remains unchanged.
