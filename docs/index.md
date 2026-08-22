# XPScript documentation

XPScript is a BASIC-style programming language implemented on .NET 10. Source files use the `.xps` extension. The same language can be compiled into normal applications, executed directly from the command line, hosted as web routes through Kestrel, FastCGI or CGI, packaged directly for IIS, and used with the shared UIForm model for desktop and web interfaces.

## Documentation map

1. [Getting started](getting-started.md), install/build, compile, run, CGI, FastCGI, Kestrel, test hosting and command-line parameters.
2. [Programming language](language.md), syntax, variables, procedures, control flow, types and coding rules.
3. [Command reference](commands.md), built-in language statements, functions, parameters and executable samples.
4. [Evaluate](evaluate.md), dynamic XPScript evaluation.
5. [Classes](classes.md), classes, constructors, properties, object references and module state.
6. [Web programming](web.md), routing, HTTP methods, Request, Response, Session, Application, route rules and precompile.
7. [CSRF protection](csrf.md), automatic UIForm protection, manual forms, custom browser requests, bearer APIs and browser WebAssembly challenge/retry.
8. [WebIIS deployment target](webiis.md), build a normal IIS deployable package with ASP.NET Core Module V2, ZIP and Web Deploy support.
9. [Hosting on IIS](iis-hosting.md), alternative production hosting on Windows Server with IIS, Kestrel reverse proxy, CGI, TLS, permissions and troubleshooting.
10. [UIForm](uiform.md), shared form API for desktop and web, including the web Bootstrap grid.
11. [Extended UIForm fields](uiform-fields.md), file uploads, multi-value fields, telephone/week/decimal/currency, rich text, lookup and autocomplete data sources.
12. [HTTP client](http-client.md), outgoing REST calls, JSON requests, query/form encoding and direct UIForm load/save helpers.
13. [HTTP database clients](httpdb.md), Supabase Cloud/self-hosted CRUD and SQL administration plus self-hosted HCL Domino REST API data access.
14. [SQLite database](sqlite.md), local parameterized SQL, JSON query results, transactions and file-path boundaries.
15. [Browser WebAssembly](browser-wasm.md), browser UIForm hosting, WASM compilation/cache and browser runtime behavior.
16. [Documentation rules](documentation-rules.md), the required structure and update process for this documentation.

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

For web development, start with [Getting started](getting-started.md) and then read [Web programming](web.md). For direct IIS deployment, use [WebIIS deployment target](webiis.md). For alternative Windows Server hosting topologies, use [Hosting XPScript on IIS](iis-hosting.md). For browser form security, read [CSRF protection](csrf.md). For UIForm field types and data-bound lookup controls, use [Extended UIForm fields](uiform-fields.md). For calling REST services or loading and saving UIForm data, use [HTTP client](http-client.md). For direct Supabase or Domino REST API data access, use [HTTP database clients](httpdb.md). For local relational storage, use [SQLite database](sqlite.md).
