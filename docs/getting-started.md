# Getting started

## Build the tools

XPScript targets .NET 10. Build the unified CLI project:

```powershell
dotnet build .\src\XPScript.Cli\XPScript.Cli.csproj -c Release
```

After every normal `Build` or `Rebuild` of `XPScript.Cli`, the complete framework-dependent XPscript toolchain is automatically published to the repository root:

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
    ...transitive runtime dependencies...
```

`publish/` is ignored by Git and must not be committed. Project-local `bin/` and `obj/` directories remain internal build artifacts.

The automatic publish can be disabled for a build with `-p:SkipUnifiedPublish=true`.

## Manual publish

The same unified output can be produced explicitly:

```powershell
./scripts/publish-distributions.ps1
```

Default output:

```text
./publish/xpscript/
```

Target another runtime:

```powershell
./scripts/publish-distributions.ps1 -Runtime win-x64
./scripts/publish-distributions.ps1 -Runtime linux-x64
./scripts/publish-distributions.ps1 -Runtime osx-arm64
```

Create a self-contained toolchain:

```powershell
./scripts/publish-distributions.ps1 -Runtime win-x64 -SelfContained
```

Override the root output when needed:

```powershell
./scripts/publish-distributions.ps1 -OutputRoot publish/custom
```

## Run and compile

The published `xpscript` executable is the primary toolchain entrypoint. It exposes compiler, run, Kestrel, FastCGI and packaging commands through one distribution. The individual source-project `bin/` folders are not deployment inputs.

Examples:

```text
xpscript run hello.xps
xpscript compile hello.xps -o hello
xpscript web --root ./site --address 127.0.0.1 --port 8080
xpscript fastcgi --root /srv/xpsite --listen 127.0.0.1:9000
xpscript compile main.xps --target webiis -o ./deploy
```

## CI and release artifacts

`Distribution Publish` verifies and uploads one `xpscript-toolchain` artifact from `publish/xpscript/`.

`Distribution Publish Matrix` verifies the same unified toolchain for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64`. Manual matrix runs produce one ZIP and SHA-256 manifest per RID.
