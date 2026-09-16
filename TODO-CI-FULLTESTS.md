# TODO - Consolidate XPscript CI FullTests

## Goal

Simplify XPscript CI by replacing permanent feature-specific runtime workflows with one comprehensive cross-platform regression workflow.

## Permanent CI structure

The consolidated `FullTest` workflow runs all regression areas on one runner per operating system:

1. Language
2. Notes
3. XP Runtime
4. Platform

Windows is the gate. The complete Windows suite must pass before Ubuntu and macOS start. Each platform checks out once, sets up .NET once, builds the compiler once, and then runs all FullTests on that same runner.

Necessary build/meta checks such as Compile, IntelliSense API documentation, Documentation site, and Runtime Placeholder Guard remain separate where appropriate.

## Migration status

- [x] Create shared FullTest/assertion structure.
- [x] Build Language FullTest coverage.
- [x] Build Notes FullTest coverage.
- [x] Build XP Runtime FullTest coverage, including XPSpreadsheet and native runtime regressions.
- [x] Build Platform FullTest coverage, including Archive security and runtime regressions.
- [x] Run new and old tests in parallel.
- [x] Move XPSpreadsheet Runtime coverage into the permanent FullTest.
- [x] Move Archive Runtime coverage into the permanent FullTest.
- [x] Move NotesMail and NotesDocument Send coverage into the permanent FullTest.
- [x] Verify the consolidated Windows -> Ubuntu/macOS workflow is green.
- [x] Remove superseded focused and per-area FullTest workflows after replacements are green.
- [x] Document the feature-test lifecycle rule.
- [ ] Run the complete post-removal CI before merge.

## Rule for future features

Focused workflows may be introduced while a feature is under development. Once stable, their regression coverage must move into the consolidated FullTest and the focused workflow should be removed.

Platform execution must preserve the Windows-first gate so failed Windows validation does not consume Ubuntu and macOS runners unnecessarily.
