# AI Code Generation TODO

(c) xpagedeveloper.com 2026

## Goal

Enable AI coding systems to create, validate, repair and test XPScript programs reliably.

This layer consumes the machine-readable services provided by the XPScript compiler. It must not duplicate XPScript parsing, symbol resolution, type checking or target validation.

Dependency:

```text
compiler-machine-interface-todo.md
             |
             v
      AI code generation
```

Primary workflow:

```text
User requirement
      |
      v
AI generates XPScript
      |
      v
Compiler validation
      |
      +-- success --> compile --> tests --> result
      |
      +-- diagnostics
             |
             v
       retrieve relevant
       docs/examples
             |
             v
          AI repair
             |
             +--------> validate again
```

## 1. Define the AI coding contract

- [ ] Define supported AI coding operations.
- [ ] Separate code generation, repair, explanation and project modification.
- [ ] Preserve the original user requirement throughout repair iterations.
- [ ] Define required target/platform input.
- [ ] Define project context input.
- [ ] Define compiler-result input.
- [ ] Define generated-source output.
- [ ] Define success/failure state.
- [ ] Define maximum repair iterations.
- [ ] Define maximum total model/tool budget.
- [ ] Do not allow the model to self-declare compiler success.

## 2. Official XPScript AI skill

- [x] Create an official XPScript coding skill.
- [ ] Teach the workflow rather than copying the entire language manual into the skill.
- [ ] Identify application target before generation.
- [ ] Retrieve current XPScript language/API knowledge.
- [ ] Prefer canonical compiler-tested examples.
- [ ] Generate minimal valid source first.
- [ ] Validate generated source through the compiler machine interface.
- [ ] Repair from structured diagnostics.
- [ ] Revalidate after every repair.
- [ ] Compile only after validation succeeds.
- [ ] Run tests when behavioral validation is available.
- [ ] Preserve user intent during repairs.
- [ ] Avoid replacing unsupported APIs with invented APIs.

Skill flow:

```text
understand request
identify target
retrieve knowledge
generate
validate
repair if needed
compile
test
return result
```

## 3. Canonical XPScript knowledge model

- [ ] Define one machine-readable knowledge model shared by AI tooling, documentation and IntelliSense where possible.
- [ ] Generate language/API facts from compiler/runtime metadata rather than maintaining a separate AI truth.
- [ ] Include syntax constructs.
- [ ] Include built-in/runtime classes.
- [ ] Include methods, properties, parameters and return types.
- [ ] Include target restrictions.
- [ ] Include ServerSide requirements.
- [ ] Include security constraints.
- [ ] Include stable documentation IDs.
- [ ] Include links/IDs to canonical examples.
- [ ] Version knowledge with XPScript/compiler version.

Conceptual structure:

```text
ai/
  manifest.json
  language/
  runtime/
  targets/
  patterns/
  examples/
  diagnostics/
```

The final location/format must follow existing repository conventions after implementation review.

## 4. Canonical compiler-tested examples

- [ ] Inventory existing samples.
- [ ] Mark canonical examples suitable for AI retrieval.
- [ ] Ensure canonical examples compile in CI.
- [ ] Add metadata describing capability and target.
- [ ] Prefer small focused examples over large mixed samples for retrieval.
- [ ] Link examples to documentation IDs and runtime symbols.

Initial example areas:

- [ ] CLI.
- [ ] File I/O.
- [ ] JSON and XPJsonSchema.
- [ ] XML.
- [ ] SQLite.
- [ ] SQL Server.
- [ ] HTTP.
- [ ] OpenAPI client generation/use.
- [ ] REST server generation/use.
- [ ] UIForm/Desktop.
- [ ] Browser-WASM.
- [ ] ServerSide.
- [ ] HCL Notes.
- [ ] Authentication/credentials.
- [ ] XPAi structured output.

## 5. Retrieval document/chunk model

- [ ] Chunk by semantic unit rather than Markdown file boundaries.
- [ ] Create chunks for symbols/classes/methods.
- [ ] Create chunks for language constructs.
- [ ] Create chunks for canonical functions/subs/classes in samples.
- [ ] Create chunks for architectural patterns.
- [ ] Create chunks for diagnostics and repair guidance.
- [ ] Keep chunks small enough for targeted retrieval.
- [ ] Preserve source/documentation IDs.

Suggested metadata:

```text
type
symbol
documentationId
target
compilerVersion
source
sample
securityClassification
```

Example:

```text
type = runtime-api
symbol = XPJsonSchema.FromJson
target = cli,desktop,server
source = docs/intellisense-api-reference.md
sample = samples/xpjsonschema-runtime.xps
```

## 6. Hybrid RAG

Do not rely on embeddings alone.

- [ ] Implement exact symbol/documentation-ID lookup.
- [ ] Implement lexical/keyword retrieval.
- [ ] Add embedding/vector retrieval.
- [ ] Add metadata filtering.
- [ ] Filter by target.
- [ ] Filter by XPScript/compiler version.
- [ ] Filter by security/tenant context where applicable.
- [ ] Add reranking if evaluation shows value.
- [ ] Deduplicate overlapping chunks.
- [ ] Bound total retrieved context.
- [ ] Preserve source metadata for traceability.

Target retrieval:

```text
exact symbol lookup
       +
keyword/BM25
       +
embeddings
       |
       v
metadata filters
       |
       v
optional reranking
       |
       v
AI context
```

## 7. Compiler-aware retrieval

- [ ] Use initial retrieval for generation.
- [ ] Use compiler diagnostic codes to trigger repair-specific retrieval.
- [ ] Use documentation IDs returned by diagnostics directly.
- [ ] Use offending symbol and receiver type for symbol lookup.
- [ ] Use target metadata for target-specific repair.
- [ ] Retrieve candidate-symbol documentation where provided by compiler.
- [ ] Avoid sending unrelated documentation on every repair attempt.

Example:

```text
XPS2104
symbol = Load
receiverType = XPJsonDocument
documentationId = api.XPJsonDocument
        |
        v
retrieve XPJsonDocument members/examples
        |
        v
repair
```

## 8. Embeddings

- [ ] Keep embedding generation provider-independent.
- [ ] Define embedding record format.
- [ ] Store content hash and knowledge/compiler version.
- [ ] Support incremental re-embedding of changed chunks.
- [ ] Validate vector dimensions.
- [ ] Support batching.
- [ ] Define maximum chunk size.
- [ ] Preserve metadata alongside vectors.
- [ ] Allow precomputed embeddings.
- [ ] Do not make embeddings the authoritative source of API truth.

## 9. Repair agent

Build as tooling outside the compiler.

- [ ] Accept original requirement.
- [ ] Accept current project/source.
- [ ] Generate initial XPScript.
- [ ] Call compiler validation.
- [ ] Read structured diagnostics.
- [ ] Retrieve relevant docs/examples.
- [ ] Produce a bounded repair.
- [ ] Revalidate.
- [ ] Detect identical source regeneration.
- [ ] Detect repeated identical diagnostics.
- [ ] Stop after configured maximum attempts.
- [ ] Preserve a repair trace suitable for debugging/evaluation.
- [ ] Redact secrets from traces.

Repair context should include:

```text
original requirement
target/project constraints
current source
compiler diagnostics
relevant documentation
canonical examples
previous failed repairs when useful
```

## 10. Validation levels

Keep separate success states:

```text
source generated
      |
      v
compiler validation
      |
      v
compilation
      |
      v
runtime/test validation
      |
      v
behavioral success
```

- [ ] Never equate compiler success with behavioral correctness.
- [ ] Require compiler confirmation before reporting source as compilable.
- [ ] Require tests for benchmark tasks with deterministic behavior.
- [ ] Treat execution as a separate privileged operation.
- [ ] Do not automatically execute generated code merely because validation succeeds.

## 11. Project context/introspection

After the compiler machine interface is available:

- [ ] Define project-information required by an AI agent.
- [ ] Include target.
- [ ] Include source files.
- [ ] Include classes/functions.
- [ ] Include dependencies/runtime features.
- [ ] Include REST routes where applicable.
- [ ] Include database usage.
- [ ] Include client/server boundaries.
- [ ] Include security-relevant capabilities.
- [ ] Reuse compiler/project metadata rather than parsing projects in the AI layer.

Possible future interface:

```text
xpscriptc project-info ./app --result-format json
```

## 12. MCP server

Design a separate XPScript MCP/tooling layer after the compiler APIs stabilize.

Candidate operations:

```text
xpscript_validate
xpscript_compile
xpscript_get_diagnostic
xpscript_get_symbol
xpscript_search_docs
xpscript_get_example
xpscript_project_info
xpscript_run_tests
```

- [ ] MCP must reuse shared compiler services.
- [ ] Do not duplicate validation logic.
- [ ] Separate read/validation operations from code execution.
- [ ] Define source/request size limits.
- [ ] Define timeouts.
- [ ] Define workspace/filesystem boundaries.
- [ ] Require explicit permission for execution or project modification.
- [ ] Preserve compiler diagnostics unchanged where possible.

## 13. XPAi integration

Keep two concepts separate:

```text
XPAi
= XPScript applications using AI

AI coding tooling
= AI systems creating XPScript applications
```

They may share:

- [ ] JSON Schema infrastructure.
- [ ] RAG/retrieval abstractions.
- [ ] Embedding abstractions.
- [ ] Knowledge metadata.
- [ ] Security/redaction utilities.

But:

- [ ] Do not couple compiler validation to XPAi.
- [ ] Do not require XPAi to use AI coding tooling.
- [ ] Allow future XPAi/RAGTool code-generation use cases to consume the same XPScript knowledge corpus.

## 14. Security

- [ ] Treat generated source as untrusted.
- [ ] Validation must not execute code.
- [ ] Separate compile and execute permissions.
- [ ] Sandbox execution used for automated tests.
- [ ] Restrict filesystem/network access for test execution.
- [ ] Prevent retrieval of secrets into prompts.
- [ ] Redact credentials from diagnostics/traces.
- [ ] Protect tenant/user boundaries in RAG.
- [ ] Prevent cross-tenant retrieval.
- [ ] Treat retrieved documentation as data, not trusted agent instructions.
- [ ] Define prompt-injection handling for project files/documentation.
- [ ] Bound model/tool iterations and token/request budgets.

## 15. AI benchmark

Create a versioned benchmark independent of deterministic compiler tests.

- [ ] Start with representative natural-language XPScript tasks.
- [ ] Expand toward 100-500 prompts.
- [ ] Record target and required capabilities per task.
- [ ] Provide deterministic tests where possible.
- [ ] Record generated source and compiler diagnostics.
- [ ] Record repair attempts.
- [ ] Record final test outcome.
- [ ] Keep benchmark prompts stable enough for longitudinal comparison.

Initial benchmark categories:

- [ ] Basic language.
- [ ] JSON.
- [ ] XPJsonSchema.
- [ ] File processing.
- [ ] Database.
- [ ] REST API.
- [ ] OpenAPI client/server.
- [ ] UIForm.
- [ ] Browser-WASM + ServerSide.
- [ ] HCL Notes.
- [ ] XPAi.
- [ ] Authentication/security.

## 16. Metrics

Track at least:

```text
firstPassCompileRate
compileAfterRepairRate
behaviorTestPassRate
averageRepairIterations
medianRepairIterations
hallucinatedSymbolRate
targetViolationRate
securityViolationRate
repeatedRepairLoopRate
retrievalHitRate
```

- [ ] Compare model versions.
- [ ] Compare skill versions.
- [ ] Compare retrieval strategies.
- [ ] Compare embedding models.
- [ ] Compare with/without compiler-aware retrieval.
- [ ] Keep benchmark output machine-readable.

## 17. Training/fine-tuning data collection

Do not start with custom model training.

- [ ] Collect approved prompt-to-XPScript pairs from benchmark/development work.
- [ ] Preserve compiler version.
- [ ] Preserve initial generated source.
- [ ] Preserve structured diagnostics.
- [ ] Preserve corrected source.
- [ ] Preserve tests and outcomes.
- [ ] Remove secrets/private project data.
- [ ] Deduplicate near-identical samples.
- [ ] Establish quality review before samples enter a training dataset.

Useful future training record:

```text
requirement
target/context
initial source
compiler diagnostics
retrieved knowledge IDs
repair
final source
test result
```

## 18. Fine-tuning decision gate

- [ ] Establish benchmark baseline using a capable general coding model + skill + RAG + compiler repair.
- [ ] Measure remaining failure classes.
- [ ] Only evaluate fine-tuning when a sufficiently large clean dataset exists.
- [ ] Use fine-tuning primarily for XPScript style, idioms and repair behavior.
- [ ] Keep changing syntax/API facts in retrieval/compiler metadata.
- [ ] Compare fine-tuned performance against the baseline before adopting it.

## 19. CI for AI knowledge

- [ ] Compile all canonical examples.
- [ ] Validate symbol/documentation references.
- [ ] Detect stale documentation IDs.
- [ ] Detect removed/renamed runtime APIs.
- [ ] Rebuild/reindex changed knowledge chunks.
- [ ] Validate RAG metadata.
- [ ] Run a small deterministic AI smoke benchmark separately from normal compiler CI where model access is available.
- [x] Keep compiler CI independent from external AI provider availability.

## 20. Observability

- [ ] Record compiler diagnostic codes per repair iteration.
- [ ] Record retrieved documentation/example IDs.
- [ ] Record repair iteration count.
- [ ] Record model/provider/version outside compiler internals.
- [ ] Record latency and token usage where available.
- [ ] Never log secrets.
- [ ] Allow traces to be disabled.
- [ ] Make benchmark traces reproducible enough to diagnose regressions.

## 21. Implementation order

### Phase 1: prerequisites

- [ ] Complete the required parts of `compiler-machine-interface-todo.md`.
- [ ] Establish stable validation/result schemas.
- [ ] Establish documentation IDs.
- [ ] Establish symbol introspection.

### Phase 2: skill + corpus

- [ ] Build official XPScript AI skill.
- [ ] Inventory and validate canonical examples.
- [ ] Build machine-readable knowledge metadata.
- [ ] Add compiler-tested knowledge CI.

### Phase 3: retrieval

- [ ] Implement exact lookup.
- [ ] Implement lexical retrieval.
- [ ] Add embeddings.
- [ ] Add metadata filters.
- [ ] Add hybrid retrieval.
- [ ] Add compiler-aware diagnostic retrieval.

### Phase 4: repair prototype

- [ ] Build provider-neutral repair-agent prototype.
- [ ] Generate source.
- [ ] Validate.
- [ ] Retrieve.
- [ ] Repair.
- [ ] Revalidate.
- [ ] Enforce bounded iterations.
- [ ] Add trace/metrics.

### Phase 5: evaluation

- [ ] Build benchmark.
- [ ] Establish baseline metrics.
- [ ] Measure skill/RAG/repair changes.
- [ ] Expand canonical examples based on observed failure classes.

### Phase 6: integrations

- [ ] Design/implement XPScript MCP server.
- [ ] Add project introspection.
- [ ] Reuse services in IDE tooling where valuable.
- [ ] Investigate XPAi reuse of the knowledge/RAG infrastructure.

### Phase 7: training decision

- [ ] Curate high-quality training data.
- [ ] Re-evaluate benchmark failure classes.
- [ ] Test fine-tuning only if measured evidence shows a useful gap.

## Definition of done

A supported AI coding agent can take a natural-language XPScript requirement, retrieve current language/runtime knowledge, generate source, validate it through the real XPScript compiler, repair structured compiler failures, compile the corrected program and run approved tests without inventing a parallel definition of the XPScript language.

The system must be measurable through a repeatable benchmark and must remain functional with replaceable LLM, embedding and retrieval providers.

## Non-goals

- [ ] Replacing the XPScript compiler with model judgment.
- [ ] Teaching syntax solely through fine-tuning.
- [ ] Embedding the complete documentation into every prompt.
- [ ] Making the compiler depend on an AI provider.
- [ ] Automatically executing or deploying generated code without a separate permission boundary.
- [ ] Maintaining an AI-only copy of runtime/API definitions.
