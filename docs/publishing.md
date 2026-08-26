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
