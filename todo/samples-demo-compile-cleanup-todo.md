# Samples and Demo Compile Cleanup TODO

## Goal

Make intended runnable scripts under `samples/` and `demo/` compile successfully with the existing XPscript compiler.

This cleanup is intentionally separate from the AST compiler migration. Existing compiler failures discovered by the AST repository sweep must not block AST development.

## Rules

- [ ] Run `tests/ast-compat/run-repository-sweep.ps1` and collect every `AST_REPOSITORY_SKIPPED=` entry.
- [ ] Do not treat intentionally negative compiler fixtures as defects. Files named as error/invalid/ambiguous/duplicate/no-match fixtures remain negative tests.
- [ ] For each unexpected compile failure, create or move a small focused reproducer so it runs before larger tests.
- [ ] Determine whether the defect is in the sample/demo script or the existing compiler before changing behavior.
- [ ] Fix one compiler feature or sample family at a time.
- [ ] After a fix, remove that script from the skipped-failure set by verifying it compiles in the repository sweep.
- [ ] Keep normal compiler and machine/MCP diagnostics synchronized when compiler diagnostics change.
- [ ] Move permanent regression coverage into the appropriate FullTest suite once stable.

## Failures discovered so far

- [ ] `demo/archive/archive-extended.xps` — existing compiler returns `XPS9001` during compilation on Linux CI.

## Completion

- [ ] Repository sweep reports no unexpected compile failures for intended runnable `samples/` scripts.
- [ ] Repository sweep reports no unexpected compile failures for intended runnable `demo/` scripts.
- [ ] All intentionally negative fixtures remain classified separately and continue to test their expected diagnostics.
