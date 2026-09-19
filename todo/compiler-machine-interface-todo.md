# Compiler Machine Interface TODO

(c) xpagedeveloper.com 2026

## Goal

Make the XPScript compiler a deterministic, versioned and machine-readable language service that can be consumed by CLI tooling, CI, IDE/LSP integrations, MCP servers and AI coding agents.

The compiler remains the single source of truth for syntax, symbols, types, semantic rules, target restrictions and compilation validity.

This feature must not depend on an LLM, XPAi, OpenAI, embeddings, RAG or any AI provider.

## Architecture

Target architecture:

```text
                 XPScript Compiler Core
                         |
              CompilerService API
                         |
        +----------------+----------------+
        |                |                |
       CLI            IDE/LSP            MCP
        |                                 |
        +----------------+----------------+
                         |
                    AI tooling
```

The CLI must be a consumer of the shared compiler service rather than the only programmatic interface.

## 1. Audit existing compiler diagnostics

- [x] Locate the implementation of `--result-format json` and document the current contract.
- [x] Inventory parser, symbol, type, semantic, target and code-generation diagnostics.
- [x] Identify diagnostics that are currently created only as formatted strings.
- [x] Identify where structured information is lost before reaching CLI output.
- [x] Identify reusable source-location, symbol and type metadata.
- [x] Define one shared diagnostic model for text, JSON and XML output.
- [x] Preserve normal compilation as the authoritative validation path.

## 2. Versioned compiler result contract

- [x] Define a stable compiler-result contract.
- [x] Add `schema` and `schemaVersion`.
- [x] Include compiler version.
- [x] Include operation, such as `compile` or `validate`.
- [x] Include target and entry source where applicable.
- [x] Always return a diagnostics collection.
- [x] Ensure early failures still produce valid structured output.
- [x] Ensure JSON mode never mixes human logging into stdout.
- [x] Route non-result logging to stderr where appropriate.
- [x] Make breaking contract changes explicit through schema versioning.

Target shape:

```json
{
  "schema": "xpscript.compiler-result",
  "schemaVersion": 1,
  "compilerVersion": "1.0.0",
  "operation": "validate",
  "result": "error",
  "target": "linux-x64",
  "source": {
    "entryPoint": "program.xps"
  },
  "diagnostics": []
}
```

## 3. Stable diagnostic codes

- [x] Introduce stable diagnostic codes independent of human-readable messages.
- [x] Define code ranges after reviewing the current compiler architecture.
- [x] Never reuse a code for a different semantic meaning.
- [x] Add regression tests protecting diagnostic-code stability.

Candidate ranges to investigate:

```text
XPS1xxx parser/syntax
XPS2xxx symbols/types
XPS3xxx semantic validation
XPS4xxx target/platform/execution boundaries
XPS5xxx security/static analysis
XPS6xxx code generation
XPS7xxx external/runtime dependencies
XPS8xxx project/configuration
XPS9xxx internal compiler diagnostics
```

## 4. Common diagnostic model

- [x] Support `code`, `severity`, `category` and `message`.
- [x] Support file, start line/column and end line/column where available.
- [x] Support structured diagnostic properties instead of encoding metadata into `message`.
- [x] Standardize severities at least as `error`, `warning` and `info`.
- [x] Preserve the same semantic diagnostic across text, JSON and XML representations.

Minimum representation:

```json
{
  "code": "XPS2104",
  "severity": "error",
  "category": "member-resolution",
  "file": "program.xps",
  "line": 12,
  "column": 17,
  "endLine": 12,
  "endColumn": 21,
  "message": "Unknown member 'Load'."
}
```

## 5. Parser diagnostics

- [ ] Convert parser failures to structured diagnostics.
- [ ] Include the token/construct found where available.
- [ ] Include expected token/construct information where available.
- [ ] Include precise source ranges.
- [ ] Do not reconstruct parser metadata by parsing error messages.

## 6. Symbol diagnostics

- [x] Structure unknown symbol and unknown member failures.
- [x] Include requested symbol name.
- [x] Include receiver type for failed member resolution.
- [x] Include symbol kind and containing scope where useful.
- [ ] Reuse compiler symbol tables.
- [ ] Preserve XPScript symbol/casing semantics.

## 7. Candidate symbols

- [ ] Investigate returning valid nearby candidates for failed symbol/member lookup.
- [ ] Limit candidate count.
- [ ] Include canonical name, symbol kind and signature.
- [ ] Make ordering deterministic.
- [ ] Guarantee candidates exist in the current compiler/runtime.
- [ ] Do not maintain a separate AI-only symbol catalog.

## 8. Type and argument diagnostics

- [x] Include expected and actual type for type mismatches.
- [x] Include affected symbol where applicable.
- [x] Structure missing, extra and invalid arguments.
- [x] Include parameter name/index and expected type.
- [x] Include procedure/function signature where useful.
- [x] Include ByRef/ByVal compatibility information where relevant.
- [x] Include overload candidates if supported by the language model.

## 9. Target/platform diagnostics

- [x] Structure target-specific failures.
- [x] Include active target.
- [x] Include offending API/symbol.
- [x] Include allowed targets where known.
- [ ] Cover CLI, Desktop, Web, REST and Browser-WASM.
- [ ] Cover native/platform-specific APIs.
- [ ] Reuse existing runtime/compiler feature metadata rather than duplicating target rules.

## 10. ServerSide execution-boundary diagnostics

- [x] Structure errors for APIs that require server-side execution.
- [x] Include current target/context.
- [x] Include `requiredContext`, for example `ServerSide`.
- [x] Include offending symbol.
- [x] Ensure Browser-WASM validation can distinguish client and `[ServerSide]` code.
- [x] Reuse compiler/runtime metadata for these restrictions.

## 11. Stable documentation IDs

- [x] Define documentation IDs independent of Markdown paths.
- [x] Map language constructs, runtime classes/members, targets, security rules and diagnostics.
- [x] Allow diagnostics to reference one or more documentation IDs.
- [x] Keep actual documentation retrieval outside the compiler.

Examples:

```text
language.If
language.ForAll
api.XPJsonSchema
api.XPJsonSchema.FromJson
api.XPAi
target.BrowserWasm
target.ServerSide
security.Shell
```

## 12. JSON Schemas

- [x] Add JSON Schema for compiler results.
- [x] Add JSON Schema for diagnostics.
- [x] Choose a stable repository location such as `schemas/`.
- [x] Validate representative compiler output against the schemas in CI.
- [x] Document additive and breaking schema evolution.

Suggested files:

```text
schemas/compiler-result.schema.json
schemas/compiler-diagnostic.schema.json
```

## 13. Validation-only operation

- [x] Add or expose validation that performs normal parsing, symbol resolution, type checking, semantic validation and target validation without producing a final executable.
- [x] Reuse the normal compiler pipeline.
- [x] Do not create a reduced AI parser/type checker.
- [x] Ensure diagnostics match normal compilation for equivalent validation failures.
- [x] Avoid expensive packaging/publishing work.

Proposed CLI:

```text
xpscriptc validate program.xps --result-format json
```

## 14. stdin validation

- [x] Investigate `--stdin` source input.
- [x] Support a virtual filename for diagnostics.
- [x] Preserve line/column information.
- [x] Keep JSON result on stdout and logging on stderr.
- [x] Apply source-size and resource limits.

Possible interface:

```text
xpscriptc validate --stdin --filename program.xps --result-format json
```

## 15. Reusable compiler service

- [x] Expose validation behind a reusable compiler API.
- [x] Return typed compiler-result and diagnostic objects.
- [x] Make CLI consume this API.
- [x] Make tests able to invoke it directly.
- [x] Design it so IDE/LSP and future MCP tooling can reuse it.
- [x] Do not require in-process consumers to shell out to `xpscriptc`.

Conceptual API only:

```text
Validate(source, options)
Compile(source, options)
GetDiagnostics(...)
GetSymbols(...)
```

Exact public API names must follow the existing compiler architecture.

## 16. Symbol introspection

- [x] Investigate a machine-readable symbol-description service.
- [x] Support exact symbol/type lookup.
- [x] Return kind, signature, parameters and return type.
- [x] Return target restrictions.
- [x] Return documentation ID.
- [x] Return deprecation metadata where supported.
- [x] Expose only public XPScript language/runtime symbols.

Possible CLI consumers:

```text
xpscriptc describe XPJsonSchema --result-format json
xpscriptc symbols --search XPJson --result-format json
```

## 17. Diagnostic introspection

- [x] Provide deterministic lookup of diagnostic definitions.
- [x] Return category, severity defaults, explanation and documentation IDs.
- [x] Do not use an LLM inside the compiler to explain diagnostics.

Possible interface:

```text
xpscriptc explain XPS2104 --result-format json
```

## 18. Security and redaction

- [x] Structure existing security/static diagnostics where appropriate.
- [x] Include stable security rule IDs.
- [x] Never leak API keys, Authorization headers or credential values.
- [x] Review source snippets returned in diagnostics for secret leakage.
- [x] Validation must never execute submitted XPScript.
- [x] Separate validation, compilation and execution permissions in future remote tooling.
- [x] Bound CPU, memory, source size and diagnostic output where practical.
- [x] Prevent path traversal through virtual filenames/project paths.

## 19. Determinism

- [x] Same source, compiler version and options must produce stable diagnostic codes.
- [x] Define deterministic diagnostic ordering.
- [x] Define deterministic candidate-symbol ordering.
- [x] Normalize path representation where needed.
- [x] Avoid environment-dependent structured values.
- [x] Include enough target/configuration metadata to reproduce validation.

## 20. Performance

- [x] Benchmark validation startup and processing time.
- [x] Avoid executable generation in validation-only mode.
- [ ] Define maximum diagnostics returned.
- [ ] Report diagnostic truncation explicitly.
- [x] Investigate a reusable compiler process/service only if measured startup cost requires it.

CI benchmark evidence on the GitHub Linux runner shows a median of 1029 ms when validation starts a fresh CLI process versus 5 ms for repeated validation through a warmed, reusable `CompilerDriver` in the same process. This makes reusable in-process compiler hosting the preferred path for latency-sensitive IDE/LSP and AI tooling. The CLI remains appropriate for one-shot validation. The existing reusable compiler API satisfies in-process consumers; a persistent external transport/process should be added only when an out-of-process consumer requires it.

## 20a. MCP and warm development hosts

- [x] Add a local MCP stdio transport over the reusable compiler API.
- [x] Expose compiler validation, symbol search/description and diagnostic explanation as MCP tools.
- [x] Keep MCP validation non-executing and separate from compile/run permissions.
- [x] Add MCP protocol/contract probes to CI.
- [x] Ship an XPScript development skill that tells coding agents when to use warm MCP validation/introspection and when to run a full CLI compile.
- [x] Add `xpscript mcp install codex|claude` to install, validate or update the MCP registration and XPScript skill in supported coding clients.
- [x] Add installer integration tests with isolated fake HOME/config roots on Windows, Linux and macOS. CI verifies idempotent install/update behavior, skill installation and repair of mismatched MCP registrations on all three platforms.
- [ ] Re-check Codex and Claude Code client configuration formats before each XPScript release and keep installers versioned.
- [ ] Investigate reusing a warm `CompilerDriver` inside the existing debugger host without changing the debugger protocol.
- [ ] Let debugger-driven edit/validate cycles use warm validation before a full debug build when semantics permit.
- [ ] Investigate a warm compiler host for test/run-without-debugger so repeated test runs do not pay process/compiler startup for unchanged compiler state.
- [ ] Define cache invalidation for source, Include graph, runtime identifier, preprocessors, dependencies and compiler version before enabling warm compile reuse.
- [ ] Benchmark debugger and run-without-debugger cold versus warm paths before changing their current execution semantics.

The debugger transport is intentionally unchanged by the MCP work. Warm compilation for debugger and test/run-without-debugger should reuse the compiler service internally and preserve their existing protocols and observable execution behavior.

## 21. Tests

Add deterministic fixtures covering:

- [ ] Syntax errors.
- [ ] Unknown symbols.
- [ ] Unknown members.
- [ ] Type mismatch.
- [ ] Wrong argument count/type.
- [ ] ByRef/ByVal errors.
- [ ] Target restrictions.
- [ ] Browser-WASM restrictions.
- [ ] ServerSide-required APIs.
- [ ] Security warnings.
- [ ] Multiple diagnostics.
- [ ] Multiple source files.
- [ ] Malformed and empty source.
- [ ] UTF-8/non-ASCII source according to language support.
- [ ] LF and CRLF location consistency.

Golden tests should verify machine fields, not unnecessarily depend on exact human message wording.

## 22. Cross-platform CI

- [ ] Run machine-interface tests on Windows.
- [ ] Run on Linux.
- [ ] Run on macOS.
- [ ] Verify equivalent diagnostic codes and source positions.
- [x] Validate JSON output against the published schemas.

## 23. Documentation

- [x] Document compiler-result schema.
- [x] Document diagnostic schema and code ranges.
- [x] Document severity semantics.
- [x] Document validation-only mode.
- [x] Document stdin mode if implemented.
- [ ] Document source-location semantics.
- [x] Document versioning/compatibility.
- [ ] Document security/redaction behavior.
- [ ] Add examples for CI and external tooling.

## 24. Implementation order

### Phase 1: foundation

- [ ] Audit diagnostics and current JSON output.
- [x] Define shared diagnostic/result models.
- [x] Define diagnostic code policy.
- [x] Add schema versioning.
- [x] Add regression tests.

### Phase 2: structured compiler knowledge

- [ ] Parser metadata.
- [ ] Symbol/member metadata.
- [ ] Type/argument metadata.
- [ ] Target/ServerSide metadata.
- [ ] Documentation IDs.
- [ ] Candidate symbols.

### Phase 3: machine interface

- [x] Validation-only operation.
- [ ] Reusable compiler service API.
- [ ] stdin support if approved by architecture review.
- [ ] Symbol/diagnostic introspection.
- [ ] Resource limits.

### Phase 4: hardening

- [ ] Golden fixtures.
- [ ] Cross-platform CI.
- [x] JSON Schema validation.
- [ ] Performance benchmarks.
- [ ] Security/redaction review.

## Definition of done

An external program can submit invalid XPScript and deterministically receive enough structured compiler-owned information to identify and correct common syntax, symbol, type, semantic and target failures without parsing human-readable error strings.

A corrected source can be resubmitted and receive:

```json
{
  "schema": "xpscript.compiler-result",
  "schemaVersion": 1,
  "operation": "validate",
  "result": "ok",
  "diagnostics": []
}
```

No LLM, embedding provider, RAG system or AI SDK is required to satisfy this TODO.

## Non-goals

- [ ] AI model training or fine-tuning.
- [ ] Embeddings/vector databases.
- [ ] RAG.
- [ ] Provider-specific LLM integration.
- [ ] Prompt management.
- [ ] Autonomous code repair.
- [x] MCP transport implementation.
- [ ] Automatic execution or deployment.
- [ ] A separate AI parser/type checker.
