# TODO – Consolidate XPscript CI FullTests

## Goal

Simplify XPscript CI by replacing permanent feature-specific runtime workflows with a small number of comprehensive regression suites.

The permanent runtime/build test structure should be divided into four main areas:

1. Language FullTest
2. Notes FullTest
3. XP Runtime FullTest
4. Platform FullTest

Existing focused workflows remain active during migration and are removed only after equivalent FullTest coverage is green.

## Permanent CI structure

Keep the four FullTests plus necessary build/meta checks: Compile, IntelliSense API documentation, Documentation site, and Runtime Placeholder Guard.

## Migration order

- [ ] Create shared FullTest/assertion structure.
- [ ] Build Language FullTest.
- [ ] Build Notes FullTest.
- [ ] Build XP Runtime FullTest, including XPDb, XPAi, XPSpreadsheet, XPCsv, XPHttp, XPJson and XPJsonSchema.
- [ ] Build Platform FullTest, including Application, Archive, filesystem, NetworkTools and platform helpers.
- [ ] Run new and old tests in parallel.
- [ ] Move XPSpreadsheet Runtime coverage to XP Runtime FullTest.
- [ ] Move Archive Runtime coverage to Platform FullTest.
- [ ] Move NotesMail, NotesDocument Send and Notes Database QueryAccess coverage to Notes FullTest.
- [ ] Remove old focused workflows only after replacements are green.
- [ ] Document the feature-test lifecycle rule.
- [ ] Run the complete CI matrix before merge.

## Rule for future features

Focused workflows may be introduced while a feature is under development. Once stable, their regression coverage must move into the appropriate permanent FullTest and the focused workflow should be removed.
