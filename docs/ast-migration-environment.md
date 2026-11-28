# AST migration verification environment

This document records the environments used for AST migration verification.

Recorded: **2026-11-22**  
Branch: `investigate-ast-compiler`  
Commit: `12a0612f`

## Local Windows environment

| Area | Verified value |
|---|---|
| Operating system | Windows 11 Pro, build `10.0.26300`, 64-bit |
| .NET SDK selected by `global.json` | `10.0.404` |
| .NET SDKs installed | `10.0.404`, `11.0.100` |
| .NET runtimes | .NET 8.0.34, 10.0.15 and 11.0.0 runtime families |
| RID | `win-x64` |
| Android workload | Manifest `36.1.118/10.0.100` |
| WASM workload | `wasm-tools` manifest `10.0.115/10.0.100` |
| Android SDK | `C:\Users\fredr\AppData\Local\Android\Sdk` |
| Android platforms | `android-30`, `android-36.1`, `android-37.0` |
| Android Debug Bridge | `37.0.3-16887417` |
| Android AVD | `Pixel_5` |
| Browser | Google Chrome `158.0.8127.37` |
| Playwright | Microsoft.Playwright `1.55.0` |

The local Android emulator verification used Android API 30, x86_64, with the Pixel 5 AVD. The AVD is configured but was not running when this snapshot was recorded.

## Local hosting tools

Local tests covered XPScript Kestrel, CGI, FastCGI and browser-WASM hosting. Nginx, Apache HTTP Server and IIS/ARR were not installed or detected locally at snapshot time. Their procedures are documented in [IIS hosting](iis-hosting.md), [Apache FastCGI](apache-fastcgi.md), [Nginx FastCGI](nginx-fastcgi.md) and [Domino CGI](domino-cgi-hosting.md).

## GitHub Actions platform matrix

| Workflow area | Runners | .NET versions | Coverage |
|---|---|---|---|
| AST .NET cross-platform matrix | Ubuntu and macOS native runners | .NET 10 and .NET 11 | AST runtime fixtures; latest run for `6c59c2d9` succeeded |
| Browser WASM platform matrix | Ubuntu 24.04, Windows, macOS | .NET 10 and .NET 11 | `wasm-tools`, browser-WASM and Playwright smoke |
| Platform FullTest | Windows, Ubuntu and macOS jobs | Matrix-defined | AST platform/runtime and web hosting coverage |
| Android AST/emulator | Ubuntu runner | .NET 10 | Android workload, SDK/API and emulator workflow |

The exact GitHub runner image revision is recorded by each Actions run and should be captured again for a release candidate.

## Reproduction commands

```powershell
dotnet --info
dotnet workload list
dotnet --version
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" version
& "$env:LOCALAPPDATA\Android\Sdk\emulator\emulator.exe" -list-avds
& 'C:\Program Files\Google\Chrome\Application\chrome.exe' --version
```

Use the repository `global.json` during AST verification and record the exact GitHub Actions run URL for Linux/macOS evidence.
