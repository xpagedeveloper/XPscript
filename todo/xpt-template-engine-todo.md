# XPT Template Engine TODO

Design and implement a first-class server-side template engine for XPScript web applications.

## Goals

- Use `.xpt` as the XPScript template format.
- Keep application/business logic in `.xps` and presentation logic in `.xpt`.
- Make `XPJsonObject` the primary root model passed to templates.
- Support `XPJsonArray`, nested `XPJsonObject` values, primitives and null values.
- Cache parsed/compiled templates, never dynamic model data by default.
- HTML-encode dynamic output by default.
- Keep XPT sandboxed from filesystem, process, network, database, reflection and arbitrary runtime APIs.
- Keep the public XPT language independent of the underlying implementation so the engine can later be compiled directly by XPScript.

## Phase 1: Language specification and parser

- [ ] Define the XPT grammar and document it before exposing the format as stable.
- [ ] Add `.xpt` recognition to the web/compiler tooling.
- [ ] Implement literal HTML/text nodes.
- [ ] Implement escaped expression output: `{{ expression }}`.
- [ ] Define the supported expression subset using XPScript-style syntax.
- [ ] Support property lookup on `XPJsonObject`, including nested properties such as `User.Address.City`.
- [ ] Support indexing/iteration over `XPJsonArray`.
- [ ] Preserve primitive types rather than converting model values to strings before expression evaluation.
- [ ] Define strict Boolean condition semantics. Do not introduce JavaScript-style truthiness.
- [ ] Define null/missing-property behavior.
- [ ] Implement XPT-only comments that are not emitted to the response.
- [ ] Produce template parse errors with filename, line and column.

## Phase 2: Core rendering API

- [ ] Add a web runtime API equivalent to `Render("view.xpt", model)`.
- [ ] Require/accept `XPJsonObject` as the primary root model.
- [ ] Allow an `XPJsonObject` returned by `JsonParse` to be passed directly to `Render`.
- [ ] Render directly to the XPScript web response without unnecessary intermediate string copies where practical.
- [ ] Set the appropriate HTML content type when rendering XPT.
- [ ] Ensure the same rendering API works across Kestrel, FastCGI, CGI and IIS hosting.
- [ ] Define deterministic conversion/formatting for String, Boolean, numeric, date/time and null values.
- [ ] Add isolated renderer tests using XPJsonObject input and expected HTML output.

## Phase 3: Security and escaping

- [ ] HTML-encode `{{ expression }}` output by default.
- [ ] Add explicit trusted/raw HTML support.
- [ ] Prefer a dedicated trusted HTML value/type so arbitrary strings cannot accidentally bypass encoding.
- [ ] Investigate context-aware encoding for HTML text, HTML attributes and URLs.
- [ ] Prevent templates from accessing filesystem APIs.
- [ ] Prevent templates from accessing process/environment APIs.
- [ ] Prevent templates from making network requests.
- [ ] Prevent templates from accessing databases directly.
- [ ] Prevent reflection/arbitrary CLR invocation.
- [ ] Do not automatically expose Request, Response, Session, Application, Server or environment objects to templates.
- [ ] Add security tests for XSS, malformed expressions, raw HTML boundaries and sandbox escape attempts.

## Phase 4: Conditional HTML

- [ ] Implement `[If expression Then]`.
- [ ] Implement `[ElseIf expression Then]`.
- [ ] Implement `[Else]`.
- [ ] Implement `[End If]`.
- [ ] Support XPScript-style comparison operators.
- [ ] Support `And`, `Or` and `Not`.
- [ ] Support nested conditional blocks.
- [ ] Add compile/parse diagnostics for mismatched or unclosed conditional blocks.

Example target syntax:

```html
[If User.IsLoggedIn Then]
    <p>Hello {{User.Name}}</p>
[Else]
    <a href="/login">Log in</a>
[End If]
```

## Phase 5: Iteration

- [ ] Implement `[For Each Item In Items]`.
- [ ] Implement `[Next]`.
- [ ] Implement nested loops.
- [ ] Add `[Empty]` for empty collections.
- [ ] Add read-only loop metadata.
- [ ] Support `Loop.Index` and `Loop.Index0`.
- [ ] Support `Loop.First` and `Loop.Last`.
- [ ] Support `Loop.Count`.
- [ ] Consider `Loop.Odd` and `Loop.Even`.
- [ ] Ensure loop variables cannot mutate the source XPJsonObject/XPJsonArray.

## Phase 6: Select/Case and local presentation values

- [ ] Implement `[Select expression]`.
- [ ] Implement `[Case value]`.
- [ ] Implement `[Case Else]`.
- [ ] Implement `[End Select]`.
- [ ] Add immutable/local template values using syntax such as `[Let FullName = ...]`.
- [ ] Keep `Let` scoped to the template/block and prevent mutation of the supplied model.

## Phase 7: Partials

- [ ] Implement partial templates.
- [ ] Support `[Partial "user-card.xpt", User]`.
- [ ] Allow a nested XPJsonObject to become the partial's root model.
- [ ] Define variable/model scope isolation between parent and partial.
- [ ] Detect recursive/cyclic partial inclusion.
- [ ] Resolve partial paths only within approved template roots.
- [ ] Prevent path traversal and access outside the application template root.
- [ ] Include partial dependencies in cache invalidation.

## Phase 8: Layouts

- [ ] Implement `[Layout "main.xpt"]`.
- [ ] Add a safe internal body/content value for layout rendering.
- [ ] Ensure layout body content is not treated as an arbitrary untrusted raw string.
- [ ] Define how root model values are visible to layouts.
- [ ] Define layout/partial resolution rules.
- [ ] Detect cyclic layout references.
- [ ] Keep layout inheritance deliberately shallow/simple.

## Phase 9: Safe built-in helpers

- [ ] Define a small allowlist of template-safe helpers.
- [ ] Add deterministic date/time formatting.
- [ ] Add deterministic numeric formatting.
- [ ] Define culture selection from application/request configuration without exposing Request directly.
- [ ] Add URL encoding.
- [ ] Add JSON serialization suitable for HTML contexts.
- [ ] Add a safe default/coalesce helper if the XPScript expression syntax does not already cover it.
- [ ] Do not allow arbitrary XPScript functions to become callable from XPT by default.

## Phase 10: Forms and CSRF integration

- [ ] Integrate with the existing XPScript CSRF implementation.
- [ ] Add a template construct such as `[Csrf]` for generating the correct hidden field.
- [ ] Ensure CSRF generation works consistently across supported web hosts.
- [ ] Keep token generation inside the trusted runtime rather than exposing session internals to templates.
- [ ] Add tests for forms with and without sessions/CSRF configuration as appropriate.

## Phase 11: Template cache

- [ ] Cache the parsed/compiled template representation by canonical template path.
- [ ] Never cache the supplied XPJsonObject model as part of template caching.
- [ ] Ensure a cached template can safely render concurrently with different models.
- [ ] Define cache keys for templates, layouts and partials.
- [ ] Add bounded cache behavior to prevent unbounded memory growth.
- [ ] Add dependency tracking for layouts and partials.
- [ ] Add cache metrics/diagnostics where appropriate.

Runtime model:

```text
users.xpt
   -> parse/compile once
   -> cached template
      -> XPJsonObject request A -> HTML A
      -> XPJsonObject request B -> HTML B
      -> XPJsonObject request C -> HTML C
```

## Phase 12: Development reload

- [ ] Detect changed XPT files in development mode.
- [ ] Invalidate and rebuild changed templates.
- [ ] Invalidate dependent layouts/partials when a dependency changes.
- [ ] Avoid filesystem polling/work in production when templates are precompiled.
- [ ] Ensure failed recompilation does not corrupt an otherwise valid cache entry.
- [ ] Surface useful template errors during development without leaking source/stack traces in production.

## Phase 13: Compile-time validation

- [ ] Make `xpscript compile` validate referenced XPT files.
- [ ] Validate syntax for all discovered templates.
- [ ] Validate referenced layouts and partials exist.
- [ ] Report XPT diagnostics using normal XPScript compiler diagnostic conventions.
- [ ] Include filename, line and column.
- [ ] Detect mismatched `If`, `For Each`, `Select` and related blocks.
- [ ] Detect cyclic partial/layout dependencies.
- [ ] Add compiler tests for valid and invalid template trees.

## Phase 14: Precompiled production templates

- [ ] Design an intermediate/compiled representation that does not require parsing XPT per process start/request.
- [ ] Precompile application XPT files during `xpscript compile`.
- [ ] Package precompiled templates with compiled web applications.
- [ ] Ensure production rendering does not require source XPT files when precompiled.
- [ ] Preserve source maps so runtime diagnostics can still reference the original XPT source.
- [ ] Benchmark interpreted/cached and precompiled rendering.
- [ ] Avoid exposing the implementation engine in the public XPT contract.

## Phase 15: Performance and concurrency

- [ ] Benchmark parse time separately from render time.
- [ ] Benchmark cached template rendering with small, medium and large XPJsonObject models.
- [ ] Benchmark XPJsonArray loops.
- [ ] Benchmark layouts and nested partials.
- [ ] Verify cached templates are thread-safe or use an explicitly safe execution model.
- [ ] Minimize allocations in the hot rendering path.
- [ ] Stream/write output where practical instead of constructing multiple full-page strings.
- [ ] Add response-size enforcement consistent with the existing web runtime limits.

## Phase 16: Optional output caching

This is separate from template caching and must remain explicit.

- [ ] Design an opt-in output cache for fully rendered responses.
- [ ] Never enable output caching merely because an XPT template is cached.
- [ ] Define safe cache variation by route/query/culture/authentication context where applicable.
- [ ] Prevent accidental caching of personalized/authenticated content.
- [ ] Integrate with existing response/security headers correctly.
- [ ] Document when output caching is safe to use.

## Phase 17: Tooling and documentation

- [ ] Document XPT syntax.
- [ ] Document the XPJsonObject model contract.
- [ ] Document escaping and trusted HTML behavior prominently.
- [ ] Document layouts and partials.
- [ ] Document template cache versus output cache.
- [ ] Add a complete web example using `.xps` logic plus `.xpt` presentation.
- [ ] Update scaffolding so new web applications can optionally include a views/templates directory and starter XPT files.
- [ ] Consider editor syntax highlighting/grammar support after the language is stable.

## Deferred features

Do not put these in the first implementation unless a concrete requirement appears.

- [ ] Reusable component syntax beyond partials.
- [ ] Advanced whitespace control.
- [ ] Complex template inheritance.
- [ ] User-defined template functions.
- [ ] Arbitrary XPScript execution blocks inside templates.
- [ ] Direct Request/Session/Application access from templates.

## Initial implementation order

1. Grammar, parser and diagnostics.
2. XPJsonObject/XPJsonArray expression access.
3. Render API and default HTML escaping.
4. If/ElseIf/Else.
5. For Each/Empty and loop metadata.
6. Select/Case and Let.
7. Partials.
8. Layouts.
9. Safe helpers and formatting.
10. CSRF integration.
11. Template cache and concurrency.
12. Development reload.
13. Compile-time validation.
14. Production precompilation.
15. Performance benchmarks and optimization.
16. Optional output caching.
17. Tooling, examples and documentation.

## Architectural constraint

XPT is an XPScript language feature. A third-party template engine may be used internally to accelerate an initial implementation, but its syntax, object model and public API must not become the XPT contract. The long-term implementation must remain free to compile XPT directly through XPScript's compiler/runtime pipeline.
