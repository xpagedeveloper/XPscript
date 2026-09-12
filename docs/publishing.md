# Unified XPscript publish layout

XPscript uses one repository-root publish output for the complete CLI toolchain:

```text
publish/
  xpscript/
    xpscript
    xpscript.dll
    XPScript.Compiler.Core.dll
    XPScript.UI.Desktop.dll
    XPScript.Web.Runtime.dll
    XPScript.Web.Compiler.dll
    XPScript.Web.Kestrel.dll
    XPScript.Web.FastCgi.dll
    ...transitive dependencies...
```

`publish/` is listed in the repository `.gitignore` and is never intended to be committed. The `src/*/bin` and `src/*/obj` directories remain internal project build artifacts.

## Automatic publish after Build/Rebuild

Building `src/XPScript.Cli/XPScript.Cli.csproj` automatically publishes the complete framework-dependent CLI dependency closure to `publish/xpscript/` after the build succeeds:

```powershell
dotnet build ./src/XPScript.Cli/XPScript.Cli.csproj -c Release
```

The post-build step uses `dotnet publish --no-build`, so it does not recursively rebuild the project. Disable it for a specific build when necessary:

```powershell
dotnet build ./src/XPScript.Cli/XPScript.Cli.csproj -p:SkipUnifiedPublish=true
```

## Manual publish

The distribution script creates the same layout:

```powershell
./scripts/publish-distributions.ps1
```

Runtime-specific framework-dependent publish:

```powershell
./scripts/publish-distributions.ps1 -Runtime win-x64
```

Self-contained publish:

```powershell
./scripts/publish-distributions.ps1 -Runtime win-x64 -SelfContained
```

The default output root is `publish/xpscript`. `-OutputRoot` can override it.

## CI releases

The normal distribution workflow uploads one `xpscript-toolchain` artifact. The RID matrix produces one unified ZIP plus a SHA-256 manifest for each supported RID.


## Compiled application layout

Desktop compilation has two independent options:

| `--single-file` | `--runtime` | Output |
| --- | --- | --- |
| `true` | `false` | **Default.** Application/managed libraries are bundled into the executable. .NET 10 must already be installed. |
| `true` | `true` | Application libraries and the .NET 10 runtime are bundled into the single-file application. |
| `false` | `false` | Executable, managed libraries, `.deps.json` and `.runtimeconfig.json` are emitted as separate files. .NET 10 must already be installed. |
| `false` | `true` | Executable, managed libraries and the self-contained .NET 10 runtime are emitted as separate files. |

The default is `--single-file=true --runtime=false`. Use `--platform` (or `--rid`) for the target RID, for example `--platform win-x64`. The old `--framework-dependent` option and the old `--runtime RID` platform syntax are no longer accepted.

Framework-dependent builds (`--runtime=false`) keep the native .NET apphost. If .NET 10 is missing, startup fails before managed XPScript code runs and the .NET host reports the missing framework together with Microsoft's installation/download link.

## Generated third-party license file

Every compiled application is published with `Third-party-license.txt` beside the executable. This file is deliberately kept as a sidecar even for single-file builds so redistribution notices remain directly readable.

The compiler builds the file from the exact NuGet dependency graph restored for the application, including transitive packages. For each restored package it reads the package's NuGet license metadata and available package-local license, NOTICE and third-party notice material.

The file starts with XPScript's Apache License 2.0 redistribution requirements, the complete Apache 2.0 license text and XPScript NOTICE content. When `--runtime=true` is used, it also includes the .NET runtime license and the .NET third-party notices from the SDK/runtime installation used for publishing.

`Third-party-license.txt` must be distributed with the compiled application. Any additional license or notice files that a dependency explicitly requires to remain separate must also be preserved.
