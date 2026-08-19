# Documentation rules

This file defines how XPScript documentation must be maintained. Treat it as part of the project contract whenever language syntax, runtime APIs, web behavior, UI APIs or command-line options change.

## Required top-level documents

Keep the documentation small and navigable. The `docs` directory must contain these primary pages:

- `index.md`, overview and navigation.
- `getting-started.md`, build, compile, run and hosting setup.
- `language.md`, BASIC language guide.
- `commands.md`, complete command/function reference.
- `command-examples.md`, small runnable examples for core language commands.
- `evaluate.md`, Evaluate behavior.
- `classes.md`, classes/types/object model.
- `web.md`, web runtime and web commands.
- `uiform.md`, UIForm/UIListView desktop and web behavior.
- `documentation-rules.md`, this maintenance contract.

Do not create a new page for every small feature. Extend the relevant primary page unless the subject becomes too large to navigate.

## Source of truth

Documentation must describe implemented behavior. Verify syntax and parameters against compiler/runtime source and executable tests before documenting them. Do not invent LotusScript-compatible functions simply because a similarly named function exists elsewhere.

## When a language command changes

Update `commands.md`. Include command name, accepted syntax, parameters, return value when applicable, behavior and a link to a runnable sample. If it is a core statement, also add or update its minimal runnable program in `command-examples.md`.

## When a web feature changes

Update `web.md` for route rules, Request/Response/Session/Application members, HTTP methods, precompile/cache semantics, CGI variables, security behavior or transport-independent behavior. Update `getting-started.md` when host configuration or command-line parameters change.

## When UI changes

Update `uiform.md` whenever UIForm/UIListView APIs, field types, layout, desktop behavior, web behavior, Bootstrap version or rendering semantics change. Examples must compile with the current language.

## Examples

Examples must be executable, minimal and focused on the documented behavior. Use `.xps` syntax exactly as accepted by the compiler. Prefer existing tested files under `samples/` when they already demonstrate the API. New reference APIs should normally receive a regression sample/test at the same time as documentation.

## Links and navigation

`index.md` is the documentation entry point. Every primary page must be reachable from it. `getting-started.md` must keep its internal contents links for compile, CGI, FastCGI, Kestrel, Kestrel testing and program parameters.

## Review checklist

Before merging a documentation change, verify that names and casing match source, examples compile where CI coverage exists, parameters match current CLI/runtime code, links resolve, obsolete behavior has been removed, and the documentation does not claim unimplemented compatibility.
