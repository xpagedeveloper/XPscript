# XPScript documentation

XPScript is a BASIC-style programming language implemented on .NET 10. Source files use the `.xps` extension. The same language can be compiled into normal applications, executed directly from the command line, hosted as web routes through Kestrel, FastCGI or CGI, packaged directly for IIS, and used with the shared UIForm model for desktop and web interfaces.

XPScript is distributed under the [Apache License, Version 2.0](../LICENSE). See [NOTICE](../NOTICE) and [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) for the separate terms and attribution required by dependencies and reference material.

## Start here

- [Runnable demo catalog](../demo/README.md): small programs for console, desktop UI, browser WebAssembly, web/REST, Kestrel, FastCGI, CGI, WebIIS, SQLite, SQL Server, HTTP databases, HTTP client and XPAi/AITool.
- [Language and built-in command reference](language-reference.md): statements, operators, scalar/built-in functions and process commands. Every row has syntax, parameters, behavior and a complete `.xps` example.
- [Application and state reference](application-reference.md): complete `Application` runtime aliases/UI metadata plus `Application.State`, `Process.State`, `Session.State` and `Request.State`.
- [File and filesystem command reference](file-io-reference.md): complete file I/O, metadata, filesystem and locking command catalog, including `Seek`, `Loc`, `LOF` and `Reset`.
- [Desktop UI command reference](desktop-ui-reference.md): `MsgBox`, desktop choice/input dialogs and native open/save file dialogs.
- [Database UI data sources](database-ui-datasources.md): `QueryArray`, `GetRow`, `SaveRow` and shared `XPJsonObject` binding for UIListView/UIForm across SQLite, SQL Server, Supabase and Domino.
- [Native and managed interop reference](native-interop-reference.md): native declarations plus every OS/RID-specific `Lib`/`Alias` selector and managed/native dependency directive.
- [Compiler and host CLI reference](cli-reference.md): `xpscriptc`, Kestrel, FastCGI and WebIIS commands/options.
- [Dependency security and package patching](dependency-security-and-patching.md): application-specific dependency inspection, NuGet vulnerability checks, security modes, version-scoped compatible patches, `patch all`, `--security-only`, and the SBOM model.
- [Runtime API reference](api-reference.md): HTTP, JSON, native Notes/Domino, databases, XPAi/AITool, UIForm/UIListView and web/REST runtime objects with the same searchable five-field format.
- [Native Notes/Domino C API](notes-c-api.md): `NotesSession`, databases, views, documents, items, names, date/time values, agents and native lifecycle semantics.
- [Notes MIME](notes-mime-entity.md): native `NotesMIMEEntity`/`NotesMIMEHeader` traversal, nested mutation, headers, parameters, streams and attachment handling.
- [Compact command index](commands.md): the older compact overview. Use the references above for the complete searchable catalog.

## Documentation map

1. [Getting started](getting-started.md), install/build, compile, run, CGI, FastCGI, Kestrel, test hosting and command-line parameters.
2. [Programming language](language.md), syntax, variables, procedures, control flow, types and coding rules.
3. [Language and built-in command reference](language-reference.md), the primary language/built-in catalog with parameters and executable examples.
4. [Application and state reference](application-reference.md), complete executable/runtime path aliases, UI metadata and state-scope API.
5. [File and filesystem command reference](file-io-reference.md), complete file handles, text/binary I/O, positioning, reset, locking, metadata and filesystem commands.
6. [Desktop UI command reference](desktop-ui-reference.md), `MsgBox`, `ShowDialog` and desktop file pickers.
7. [Database UI data sources](database-ui-datasources.md), complete list/row/document JSON binding and native save semantics for UIListView and shared UIForm data.
8. [Native and managed interop reference](native-interop-reference.md), complete native target selectors and reference directives.
9. [Compiler and host CLI reference](cli-reference.md), complete compiler/Kestrel/FastCGI/WebIIS command-line catalog.
10. [Dependency security and package patching](dependency-security-and-patching.md), application dependency graphs, vulnerability auditing, security modes, compatible package patching and SBOM guidance.
11. [Runtime API reference](api-reference.md), HTTP/JSON/native Notes/database/AI/UI/web runtime members with parameters and executable examples.
12. [Native Notes/Domino C API](notes-c-api.md), Notes object model, native view lookup, document save semantics, names and date/time values.
13. [Notes MIME](notes-mime-entity.md), root/direct/nested MIME entities, headers, parameters, streams, encoding and lifecycle.
14. [Compact command index](commands.md), a concise compatibility overview.
15. [Core command examples](command-examples.md), minimal copy/paste examples for common language constructs.
16. [Date and time](date-time.md), date functions, Date object enhancements and OS formatting metadata.
17. [Evaluate](evaluate.md), dynamic XPScript evaluation.
18. [Classes](classes.md), classes, constructors, properties, object references and module state.
19. [Web programming](web.md), routing, HTTP methods, Request, Response, Session, Application, route rules and precompile.
20. [REST API development](rest-api.md), explicit routes, binding, validation, Response helpers, CORS, rate limiting and state scopes.
21. [CSRF protection](csrf.md), automatic UIForm protection, manual forms, custom browser requests, bearer APIs and browser WebAssembly challenge/retry.
22. [WebIIS deployment target](webiis.md), build a normal IIS deployable package with ASP.NET Core Module V2, ZIP and Web Deploy support.
23. [Hosting on IIS](iis-hosting.md), alternative production hosting on Windows Server with IIS, Kestrel reverse proxy, CGI, TLS, permissions and troubleshooting.
24. [UIForm](uiform.md), shared form API for desktop and web, including the web Bootstrap grid.
25. [Extended UIForm fields](uiform-fields.md), file uploads, multi-value fields, telephone/week/decimal/currency, rich text, lookup and autocomplete data sources.
26. [HTTP client](http-client.md), outgoing REST calls, JSON requests, query/form encoding and direct UIForm load/save helpers.
27. [HTTP database clients](httpdb.md), Supabase Cloud/self-hosted CRUD and SQL administration plus self-hosted HCL Domino REST API data access.
28. [SQLite database](sqlite.md), local parameterized SQL, JSON query results, transactions and file-path boundaries.
29. [SQL Server database](mssql.md), SQL Server and SQL Server Express connections, parameterized SQL, JSON results and transactions.
30. [XPAi client](ai.md), OpenAI-compatible AI requests, provider configuration, response metadata and SSE streaming.
31. [XPAi tools and session memory](ai-tools-sessions.md), AITool schemas/callbacks, automatic tool execution and provider session continuation.
32. [Browser WebAssembly](browser-wasm.md), browser UIForm hosting, WASM compilation/cache and browser runtime behavior.
33. [Documentation rules](documentation-rules.md), the required structure and CI validation for command/API documentation and demos.

## How XPScript runs

A normal program starts in `Sub Main()` or, when applicable, `Sub Initialize()`. The compiler translates XPScript into a .NET application. A compiled application does not need the compiler source tree at runtime. Deploy the complete publish output for the selected target.

A web application is a directory of `.xps` files. The web dispatcher maps request paths to files and exported procedures. `/`, `/index` and `/index.xps` resolve to the same default route when `index.xps` is the configured default document. Route matching and precompile cache keys are normalized independently of URL spelling.

A WebIIS application uses `main.xps` as its application entry file and can be packaged with:

```text
xpscript compile main.xps --target webiis
```

## Minimal program

```xpscript
Sub Main()
    Print "Hello from XPScript"
End Sub
```

Compile it with:

```text
xpscriptc hello.xps -o hello
```

For a feature tour, start with the [demo catalog](../demo/README.md). For lookup use [language-reference.md](language-reference.md), [application-reference.md](application-reference.md), [file-io-reference.md](file-io-reference.md), [desktop-ui-reference.md](desktop-ui-reference.md), [database-ui-datasources.md](database-ui-datasources.md), [native-interop-reference.md](native-interop-reference.md), [cli-reference.md](cli-reference.md), [dependency-security-and-patching.md](dependency-security-and-patching.md), or [api-reference.md](api-reference.md) according to the command family. The topical pages then provide deeper behavior, security and deployment guidance.