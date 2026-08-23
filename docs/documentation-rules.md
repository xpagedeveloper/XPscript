# Documentation rules

This file defines how XPScript documentation must be maintained. Treat it as part of the project contract whenever language syntax, runtime APIs, web behavior, UI APIs or command-line options change.

All user-facing documentation in `docs/` must be written in English.

## Required top-level documents

The primary documentation entry points are:

- `index.md`, overview and navigation.
- `getting-started.md`, build, compile, run and hosting setup.
- `language.md`, BASIC language guide.
- `language-reference.md`, complete language statement, built-in function, file/process, interop and compiler CLI reference.
- `api-reference.md`, complete searchable runtime-object API reference.
- `commands.md`, compact compatibility/quick command index.
- `command-examples.md`, small runnable examples for common core language commands.
- `date-time.md`, Date/date-time command details.
- `evaluate.md`, Evaluate behavior.
- `classes.md`, classes/types/object model.
- `web.md` and `rest-api.md`, web runtime, routing, state and REST behavior.
- `uiform.md` and `uiform-fields.md`, UIForm/UIListView behavior and controls.
- `documentation-rules.md`, this maintenance contract.
- `../demo/README.md`, runnable feature/application demonstrations.

Do not create a new page for every small feature. Extend the appropriate reference or topical page unless the subject is large enough to need its own navigable page.

## Source of truth

Documentation must describe implemented behavior. Verify syntax and parameters against compiler/runtime source and executable tests before documenting them. Do not invent LotusScript-compatible functions simply because a similarly named function exists elsewhere.

Implementation checklists under `todo/` may be used to discover implemented areas, but compiler/runtime source and executable regressions remain authoritative for syntax and behavior.

## Required command/API entry format

Every user-callable command, built-in function, runtime method/property, route rule or compiler command that belongs in a reference must have these five fields:

1. **Title/member name**, using the documented spelling.
2. **Syntax**, showing the accepted call/statement form.
3. **Parameters**, naming each argument/option and briefly stating what it controls. Use `none` when there are no parameters.
4. **Description/behavior**, a short statement of what the command does. Include return behavior when it is important to using the command correctly.
5. **Example**, linking to a complete `.xps` file under `demo/` or `samples/` that can be copied and compiled.

`language-reference.md` owns language statements, built-in scalar functions, file/process commands, interop syntax and compiler CLI options. `api-reference.md` owns runtime objects such as HTTP, JSON, databases, XPAi/AITool, UIForm/UIListView and web state. Topical pages may repeat important members with longer explanations, but they should link back to the appropriate reference when useful.

## When a language command changes

For every new language statement, built-in function, operator family, file/process command, interop declaration or compiler CLI option, update `language-reference.md`. Keep `commands.md` in sync when the command belongs in the compact index. If it is a common core statement, also add or update a minimal runnable program in `command-examples.md`.

## When a runtime API changes

For every new public runtime object, method, property, route/binding rule or response/state helper, update `api-reference.md` using the five-field format. Also update the relevant topical page for behavior that needs more explanation, limits, security guidance or platform boundaries.

Examples:

- HTTP/JSON: `http-client.md` plus `api-reference.md`.
- SQLite/SQL Server/HTTP DB: database topical page plus `api-reference.md`.
- XPAi/AITool/session memory: `ai.md`, `ai-tools-sessions.md` and `api-reference.md`.
- UIForm/UIListView: `uiform.md`, `uiform-fields.md` and `api-reference.md`.
- Web/REST/Session/Application/RequestScope: `web.md`, `rest-api.md` and `api-reference.md`.

## When a web feature changes

Update `web.md` for route rules, Request/Response/Session/Application members, HTTP methods, precompile/cache semantics, CGI variables, security behavior or transport-independent behavior. Update `rest-api.md` for REST binding/validation/response helpers. Update `getting-started.md` when host configuration or command-line parameters change.

## When UI changes

Update `uiform.md` or `uiform-fields.md` whenever UIForm/UIListView APIs, field types, layout, desktop behavior, web behavior, browser-WASM behavior, Bootstrap version or rendering semantics change. Examples must compile with the current language.

## Demo catalog

`demo/` is the user-facing feature demonstration tree. It is intentionally different from `samples/`:

- `demo/` contains short programs intended to be run by a developer evaluating or demonstrating XPScript.
- `samples/` contains regression/compatibility programs and may exercise many edge cases in one file.

Every `.xps` file under `demo/` must be listed in `demo/README.md` with the command/environment needed to run it. Add a new demo when a new application/runtime type is introduced or when an important integration has no simple demonstration.

## Examples

Examples must be executable, minimal and focused on the documented behavior. Use `.xps` syntax exactly as accepted by the compiler. Prefer a matching file under `demo/` for user-facing walkthroughs and an existing tested file under `samples/` for low-level API reference coverage.

A documentation table must not link to an invented or future example filename. CI validates `.xps` links in the primary reference files.

## CI validation

`scripts/validate-docs-demos.ps1` validates the documentation/demo contract. The required PR gate must run it before restore/build. The validator checks:

- the primary language/runtime reference files exist;
- reference rows contain the required fields and an `.xps` example link;
- every referenced `demo/` or `samples/` `.xps` file exists;
- high-risk/easy-to-miss command families remain present in the master references;
- every `demo/**/*.xps` program appears in `demo/README.md`.

Relevant standalone demos should also be compiler-checked in CI so documentation drift cannot silently leave copyable examples syntactically invalid.

## Links and navigation

`index.md` is the documentation entry point. The demo catalog and both primary references must be visible near the top of that page. Every primary topical page must be reachable from `index.md`.

## Review checklist

Before merging a documentation change, verify that names and casing match source, examples compile where CI coverage exists, parameters match current CLI/runtime code, reference/example links resolve, obsolete behavior has been removed, and the documentation does not claim unimplemented compatibility.