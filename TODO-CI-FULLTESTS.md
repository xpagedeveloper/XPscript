# TODO – Consolidate XPscript CI FullTests

## Goal

Simplify XPscript CI by replacing permanent feature-specific runtime workflows with a small number of comprehensive regression suites.

Small focused tests may be created while a feature is being developed or a regression is being diagnosed. Once the feature is stable, its coverage must move into the appropriate FullTest suite and the dedicated workflow must be removed.

The permanent runtime/build test structure should be divided into four main areas.

## 1. Language FullTest

Create a comprehensive XPscript script that verifies the core language.

Cover at least:

- `Dim`, `Set`, `Const`
- String, Integer, Long, Double, Boolean, Variant and other basic types
- arrays and Lists
- `If / ElseIf / Else`
- `Select Case`
- `For / Next`
- `ForAll`
- `While / Wend`
- `Do / Loop`
- `Exit`
- `And`, `Or`, `Not`
- comparison, arithmetic and string operators
- Functions and Subs
- parameters and return values
- scope
- Classes
- properties, methods and constructors
- object instances
- inheritance/extensions where supported
- error handling
- date/time
- type conversion
- Null/Nothing/Empty
- UDT
- Evaluate and other language functions

Use assertions and finish with a summary such as:

`LANGUAGE_FULLTEST: 184 passed, 0 failed`

CI must fail if any assertion fails.

## 2. Notes FullTest

Consolidate regression coverage for the Notes/Domino runtime surface into one permanent Notes suite.

Cover at least:

- NotesSession
- NotesDatabase
- NotesDocument
- NotesItem
- NotesView
- NotesViewEntry
- NotesViewEntryCollection
- NotesDocumentCollection
- NotesNoteCollection
- NotesDateTime
- NotesRichTextItem
- NotesStream
- NotesMIMEEntity
- NotesAgent
- NotesACL / NotesACLEntry
- NotesHTTPRequest
- NotesJSONNavigator / NotesJSONObject / NotesJSONArray / NotesJSONElement
- NotesMail
- NotesDocument.Send
- NotesDatabase.QueryAccess
- document create/read/update/delete
- views and collections
- items
- mail
- MIME
- RichText
- ACL
- agents

Move existing coverage from the small Notes workflows into this suite.

Example summary:

`NOTES_FULLTEST: 246 passed, 0 failed`

## 3. XP Runtime FullTest

Create one shared integration/runtime suite for XP objects.

### XPDb

Cover common XPDb APIs plus SQLite, MSSQL, Supabase and Domino backends, including queries, parameters, CRUD, transactions and attachments.

### XPAi

Cover constructors/providers, messages, prompts, Complete, Stream, tools, tool calls, sessions, structured output and XPJsonSchema integration. External AI calls should be mocked where necessary so CI does not depend on API keys.

### XPSpreadsheet

Move current XPSpreadsheet Runtime coverage here. Cover workbook, worksheets, cells, ranges, read/write, load/save and data types.

### XPCsv

Cover parse, stringify, escaping, load/save and byte parsing.

### XPHttp

Cover requests, responses, headers, body, JSON, errors and async behavior where supported.

### XPJson

Cover XPJsonDocument, XPJsonObject, XPJsonArray, XPJsonElement, Parse, serialization, mutation and navigation.

### XPJsonSchema

Cover Parse, FromJson, schema builders, required properties, arrays, enums, validation, IsValid, validation errors and XPAi structured-output integration.

XML and other XP data/integration objects should also live here where appropriate.

Example summary:

`XP_RUNTIME_FULLTEST: 312 passed, 0 failed`

## 4. Platform FullTest

Consolidate runtime functions belonging to the XPscript platform rather than the language, Notes or XP data APIs.

Cover at least:

- Application
- Archive / ArchiveEntry
- Extended Archive
- filesystem
- file IO
- text IO
- NetworkTools
- SystemInventory
- runtime arguments
- script directory
- platform detection
- native interoperability where supported by CI
- cross-platform helpers

Move current Archive Runtime coverage here.

Example summary:

`PLATFORM_FULLTEST: 173 passed, 0 failed`

## Permanent CI structure

Create permanent workflows for:

- Language FullTest
- Notes FullTest
- XP Runtime FullTest
- Platform FullTest

Keep necessary build/meta checks such as:

- Compile
- IntelliSense API documentation
- Documentation site
- Runtime Placeholder Guard

These are not feature regression suites and should not be folded into the four FullTests.

## Retire small permanent workflows

After equivalent coverage exists in the FullTests, remove the corresponding workflows from normal PR/build execution, including:

- Archive Runtime
- NotesMail
- NotesDocument Send
- Notes Database QueryAccess
- XPSpreadsheet Runtime

Review `.github/workflows` for additional feature-specific regression workflows that can be absorbed by the four FullTests. Remove obsolete one-shot workflows.

## Rule for future features

A focused workflow may be introduced while developing a feature, for example `XPJsonSchema Ref Runtime`.

When the feature becomes stable:

1. Move the relevant regression tests into `XP Runtime FullTest` (or the appropriate suite).
2. Verify the FullTest detects the same regressions.
3. Remove the focused workflow.

Coverage should grow inside the permanent suites instead of permanently increasing the number of CI workflows.

## Test harness

Each FullTest should have clearly named sections/functions, for example:

- `TestLanguageBasics()`
- `TestConditions()`
- `TestLoops()`
- `TestClasses()`

Provide shared assertion helpers such as:

- `AssertTrue`
- `AssertFalse`
- `AssertEqual`
- `AssertNotEqual`
- `AssertNothing`
- `AssertNotNothing`
- `AssertThrows`

Every assertion increments passed/failed counters. Failures should report suite, section/test name, expected value and actual value.

Example:

```
FAIL XPJSON_SCHEMA_VALIDATE_REQUIRED
Expected: False
Actual: True
```

A FullTest must return a non-zero exit code when `failed > 0`.

## Migration checklist

- [ ] Create shared FullTest/assertion structure.
- [ ] Build Language FullTest.
- [ ] Build Notes FullTest.
- [ ] Build XP Runtime FullTest.
- [ ] Build Platform FullTest.
- [ ] Run new and old tests in parallel during migration.
- [ ] Verify existing coverage is represented in the FullTests.
- [ ] Move XPSpreadsheet Runtime coverage to XP Runtime FullTest.
- [ ] Move Archive Runtime coverage to Platform FullTest.
- [ ] Move NotesMail coverage to Notes FullTest.
- [ ] Move NotesDocument Send coverage to Notes FullTest.
- [ ] Move Notes Database QueryAccess coverage to Notes FullTest.
- [ ] Identify and migrate remaining small permanent runtime workflows.
- [ ] Remove old workflows only after their replacement FullTest is green.
- [ ] Remove obsolete one-shot workflows.
- [ ] Document the feature-test lifecycle rule in `AGENTS.md` or developer documentation.
- [ ] Run the complete CI matrix.
- [ ] Verify Windows/Linux and other relevant targets.
- [ ] Merge only when all four FullTests plus Compile and documentation checks are green.

## Definition of Done

A normal XPscript PR should no longer start an ever-growing collection of small runtime workflows.

The normal build should primarily expose:

- Compile
- Language FullTest
- Notes FullTest
- XP Runtime FullTest
- Platform FullTest
- IntelliSense API documentation
- Documentation site
- Runtime Placeholder Guard

New features should extend coverage inside these suites instead of permanently increasing the number of CI workflows.
