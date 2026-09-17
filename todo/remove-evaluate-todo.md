# Remove Evaluate support TODO

(c) xpagedeveloper.com 2026

Branch: `remove-evaluate`

## Goal

Remove the XPScript `Evaluate` feature completely from the active compiler, transpiler, runtime, samples, documentation, generated API surfaces and CI regression coverage.

Historical Git commits are not changed.

## 1. Inventory active Evaluate references

- [ ] Search the full repository for `Evaluate`, `evaluate`, Evaluate-specific classes, helpers, samples and generated API entries.
- [ ] Classify every active reference as compiler, preprocessing, runtime, sample, documentation, generated output, API metadata or CI/test coverage.
- [x] Identify references that are historical only and must remain untouched.

## 2. Compiler and transpiler

- [ ] Remove parsing or recognition of `Evaluate` from the compiler frontend.
- [x] Remove Evaluate-specific transpiler branches and code generation.
- [x] Remove compiler diagnostics that only exist for Evaluate.
- [x] Remove Evaluate-specific compiler helpers that become unused.
- [ ] Confirm normal expression, function and procedure compilation is unaffected.

## 3. Preprocessor and postprocessor pipeline

- [x] Remove Evaluate-specific preprocessors.
- [x] Remove Evaluate-specific source rewriting.
- [x] Remove Evaluate-specific postprocessing or generated-code cleanup.
- [x] Remove registration of these processors from the pipeline.
- [ ] Remove dead utility methods and imports left after the cleanup.

## 4. Runtime

- [x] Remove the Evaluate runtime entry points.
- [x] Remove the Evaluate parser/interpreter implementation.
- [x] Remove Evaluate-only runtime models, tokens, AST nodes and helpers.
- [x] Remove runtime registrations and dispatch cases for Evaluate.
- [x] Remove public runtime APIs that expose Evaluate.
- [ ] Verify no dynamic execution path remains under another name by accident.

## 5. Samples and fixtures

- [x] Remove all `samples/evaluate-*` files.
- [ ] Remove Evaluate-specific test fixtures and expected-output files.
- [ ] Remove references to deleted Evaluate samples from sample indexes and scripts.
- [ ] Verify no generic sample depends on Evaluate indirectly.

## 6. Documentation

- [x] Remove `docs/evaluate.md` if present.
- [ ] Remove Evaluate sections and links from language documentation.
- [ ] Remove Evaluate examples from README files and tutorials.
- [ ] Remove Evaluate references from runtime/reference documentation.
- [x] Remove obsolete Evaluate TODO or closeout documents once their information is no longer needed.
- [x] Keep historical Git history unchanged.

## 7. Generated API and IntelliSense surfaces

- [ ] Remove Evaluate from generated API documentation.
- [ ] Remove Evaluate from IntelliSense metadata and completion data.
- [ ] Remove Evaluate from API manifests, runtime registries and exported symbol lists.
- [ ] Regenerate generated artifacts where required.
- [ ] Verify API-sync checks no longer expect Evaluate.

## 8. CI and automated tests

- [ ] Remove Evaluate-specific CI jobs and workflow steps.
- [ ] Remove Evaluate-specific regression tests from FullTests.
- [ ] Remove Evaluate-specific fixtures used by CI.
- [ ] Remove scripts that compile or execute Evaluate samples.
- [ ] Update test counts or expected test manifests if they are explicit.
- [ ] Verify permanent cross-platform FullTests still run on supported platforms.
- [ ] Verify API-sync and documentation checks pass without Evaluate.

## 9. Build and regression verification

- [ ] Build the full solution after removal.
- [ ] Run compiler tests.
- [ ] Run runtime tests.
- [ ] Run FullTests locally where possible.
- [ ] Verify Windows CI.
- [ ] Verify Linux CI.
- [ ] Verify macOS CI.
- [ ] Verify generated API and documentation checks.
- [ ] Fix all warnings caused by dead Evaluate code or unused imports.

## 10. Final repository sweep

- [ ] Search the repository again for `Evaluate` and `evaluate`.
- [ ] Review every remaining hit manually.
- [ ] Confirm remaining hits are historical, explanatory or intentionally unrelated to the removed feature.
- [ ] Confirm no active source, test, sample, CI workflow, generated API or documentation exposes Evaluate.

## 11. Completion

- [ ] Review the branch diff for accidental unrelated changes.
- [x] Ensure `main` was not modified directly.
- [ ] Move this TODO to `todo/done/` after all checks pass.
- [ ] Open the pull request with a summary of removed surfaces and CI verification results.
