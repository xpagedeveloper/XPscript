# XPScript documentation

XPScript is a BASIC-style programming language implemented on .NET 10. Source files use the `.xps` extension. The same language can be compiled into normal applications, executed directly from the command line, hosted as web routes through Kestrel, FastCGI or CGI, packaged directly for IIS, and used with the shared UIForm model for desktop and web interfaces.

## Start here

- [Runnable demo catalog](../demo/README.md): small programs for console, desktop UI, browser WebAssembly, web/REST, Kestrel, FastCGI, CGI, WebIIS, SQLite, SQL Server, HTTP databases, HTTP client and XPAi/AITool.
- [Language and built-in command reference](language-reference.md): statements, operators, scalar/built-in functions, file I/O, process commands, interop and compiler CLI. Every row has syntax, parameters, behavior and a complete `.xps` example.
- [Runtime API reference](api-reference.md): Application, HTTP, JSON, databases, XPAi/AITool, UIForm/UIListView and web/REST runtime objects with the same searchable five-field format.
- [Compact command index](commands.md): the older compact overview. Use the two references above for the complete searchable catalog.

## Documentation map

1. [Getting started](getting-started.md), install/build, compile, run, CGI, FastCGI, Kestrel, test hosting and command-line parameters.
2. [Programming language](language.md), syntax, variables, procedures, control flow, types and coding rules.
3. [Language and built-in command reference](language-reference.md), the primary language/built-in catalog with parameters and executable examples.
4. [Runtime API reference](api-reference.md), HTTP/JSON/database/AI/UI/web runtime members with parameters and executable examples.
5. [Compact command index](commands.md), a concise compatibility overview.
6. [Core command examples](command-examples.md), minimal copy/paste examples for common language constructs.
7. [Date and time](date-time.md), date functions, Date object enhancements and OS formatting metadata.
8. [Evaluate](evaluate.md), dynamic XPScript evaluation.
9. [Classes](classes.md), classes, constructors, properties, object references and module state.
10. [Web programming](web.md), routing, HTTP methods, Request, Response, Session, Application, route rules and precompile.
11. [REST API development](rest-api.md), explicit routes, binding, validation, Response helpers, CORS, rate limiting and state scopes.
12. [CSRF protection](csrf.md), automatic UIForm protection, manual forms, custom browser requests, bearer APIs and browser WebAssembly challenge/retry.
13. [WebIIS deployment target](webiis.md), build a normal IIS deployable package with ASP.NET Core Module V2, ZIP and Web Deploy support.
14. [Hosting on IIS](iis-hosting.md), alternative production hosting on Windows Server with IIS, Kestrel reverse proxy, CGI, TLS, permissions and troubleshooting.
15. [UIForm](uiform.md), shared form API for desktop and web, including the web Bootstrap grid.
16. [Extended UIForm fields](uiform-fields.md), file uploads, multi-value fields, telephone/week/decimal/currency, rich text, lookup and autocomplete data sources.
17. [HTTP client](http-client.md), outgoing REST calls, JSON requests, query/form encoding and direct UIForm load/save helpers.
18. [HTTP database clients](httpdb.md), Supabase Cloud/self-hosted CRUD and SQL administration plus self-hosted HCL Domino REST API data access.
19. [SQLite database](sqlite.md), local parameterized SQL, JSON query results, transactions and file-path boundaries.
20. [SQL Server database](mssql.md), SQL Server and SQL Server Express connections, parameterized SQL, JSON results and transactions.
21. [XPAi client](ai.md), OpenAI-compatible AI requests, provider configuration, response metadata and SSE streaming.
22. [XPAi tools and session memory](ai-tools-sessions.md), AITool schemas/callbacks, automatic tool execution and provider session continuation.
23. [Browser WebAssembly](browser-wasm.md), browser UIForm hosting, WASM compilation/cache and browser runtime behavior.
24. [Documentation rules](documentation-rules.md), the required structure and CI validation for command/API documentation and demos.

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

For a feature tour, start with the [demo catalog](../demo/README.md). When looking up a command, search [language-reference.md](language-reference.md) first and [api-reference.md](api-reference.md) for runtime objects. The topical pages then provide deeper behavior, security and deployment guidance.