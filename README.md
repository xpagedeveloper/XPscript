# XPScript

(c) xpagedeveloper.com 2026

XPScript is licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE).

Third-party components retain their own license terms. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for dependencies, attribution, license texts and redistribution requirements. See [TERMS-OF-USE.md](TERMS-OF-USE.md) for the distribution notice that applies when compiled applications are redistributed.

XPScript is a standalone programming language compiler implemented in C#/.NET 10. Source files use the `.xps` extension and can target Windows, Linux and macOS executables without requiring an external scripting runtime.
The Language is a work in progress but still very capable, please report any findings in issues.

We have support for CLI, Desktop, WebAssembly Desktop, Webb Server (Internal Kestrel, IIS, Fast CGI, CGI) and a special Rest server setup.
There is also Lotus of support for databases but the biggest one is HCL Notes C-API access to Databases, Views, Documents and lots more.

## Documentation

The language/runtime reference is maintained under `docs/` and intentionally reuses source fixtures already present under `samples/`.

Start with:

- `docs/index.md` — documentation index mapped to sample files
- `docs/core-language.md`
- `docs/arrays-lists-operators.md`
- `docs/types-classes-modules.md`
- `docs/strings-conversion-base64.md`
- `docs/math-functions.md`
- `docs/date-time.md`
- `docs/file-io-filesystem.md`
- `docs/console-process-formatting.md`
- `docs/platform-native.md`
- `docs/native-http-json.md`
- `docs/sqlite.md`
- `docs/mssql.md`
- `docs/evaluate.md`
- `docs/security.md` — security boundaries and powerful APIs
- `docs/diagnostics-security.md` — diagnostic redaction and secret-safe error policy

Negative samples intentionally demonstrate errors and are identified as such in the documentation. Older compatibility fixtures are not automatically presented as the preferred standalone XPScript API.

## Compiler

Build the compiler:

```powershell
dotnet build .\src\XPScript.Compiler\XPScript.Compiler.csproj -c Release
```

Compile for the current platform:

```powershell
xpscriptc program.xps -o program
```

Compile for a specific runtime:

```powershell
xpscriptc program.xps --runtime win-x64 -o program.exe
xpscriptc program.xps --runtime linux-x64 -o program
xpscriptc program.xps --runtime linux-arm64 -o program
xpscriptc program.xps --runtime osx-x64 -o program
```
