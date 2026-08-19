# Getting started

## Contents

- [Build the tools](#build-the-tools)
- [Run XPScript directly](#run-xpscript-directly)
- [Compile code](#compile-code)
- [Compiler parameters](#compiler-parameters)
- [Run Kestrel for local testing](#run-kestrel-for-local-testing)
- [Kestrel hosting](#kestrel-hosting)
- [Kestrel parameters](#kestrel-parameters)
- [FastCGI hosting](#fastcgi-hosting)
- [FastCGI parameters](#fastcgi-parameters)
- [CGI hosting](#cgi-hosting)
- [CGI configuration](#cgi-configuration)
- [Deployment packages](#deployment-packages)

## Build the tools

XPScript targets .NET 10.

```powershell
dotnet build .\src\XPScript.Compiler\XPScript.Compiler.csproj -c Release
dotnet build .\src\XPScript.Cli\XPScript.Cli.csproj -c Release
dotnet build .\src\XPScript.Web.Cgi\XPScript.Web.Cgi.csproj -c Release
```

`xpscriptc` is the compiler executable. `xpscript` is the CLI that also hosts Kestrel and FastCGI.

## Run XPScript directly

```text
xpscriptc run hello.xps
xpscriptc run hello.xps first "second value"
```

Arguments are available through `Application.ArgCount` and `Application.Args(index)`.

## Compile code

```text
xpscriptc hello.xps -o hello
```

Target another runtime:

```text
xpscriptc hello.xps --runtime win-x64 -o hello.exe
xpscriptc hello.xps --runtime linux-x64 -o hello
xpscriptc hello.xps --runtime osx-arm64 -o hello
```

Supported deployment RIDs include `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64`.

## Compiler parameters

| Parameter | Purpose |
|---|---|
| `source.xps` | Source file to compile. |
| `run source.xps` | Compile to an isolated temporary directory and run immediately. |
| `-o path` | Output executable/path. |
| `--runtime RID` | Select target runtime. |
| `--framework-dependent` | Require a compatible .NET runtime on the target instead of producing self-contained output. |
| `--result-format text` | Human-readable compiler result. |
| `--result-format json` | Structured JSON result. |
| `--result-format xml` | Structured XML result. |
| `--` | End compiler option parsing when following values belong to the script. |

For automation, JSON/XML errors include source location and compiler diagnostics.

## Run Kestrel for local testing

Create `site/index.xps`:

```xpscript
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/html; charset=utf-8"
    Response.Write("<h1>Hello from XPScript</h1>")
End Sub
```

Start the development server:

```text
xpscript web --root ./site --address 127.0.0.1 --port 8080
```

Open `http://127.0.0.1:8080/`. Keep a local test listener on loopback unless another machine must reach it.

## Kestrel hosting

Basic production-style invocation:

```text
xpscript web --root /srv/xpsite --address 0.0.0.0 --port 8080 --host www.example.com
```

Kestrel can use a JSON `web.cfg`. Explicit command-line values override config values.

## Kestrel parameters

| Parameter | Purpose |
|---|---|
| `--root PATH` | Required web root. |
| `--default-document FILE.xps` | Default route file, normally `index.xps`. |
| `--address IP`, `--bind IP` | Listener address. |
| `--port N` | Listener port. |
| `--host NAME`, `--allowed-host NAME` | Add an accepted Host value. Repeatable. |
| `--https-cert PATH` | PFX certificate. |
| `--https-cert-password-env NAME` | Environment variable containing the PFX password. |
| `--protocols http1|http2|http1+2` | HTTP protocol selection. |
| `--health` | Enable health endpoint. |
| `--metrics` | Enable metrics endpoint. |
| `--sessions` | Enable in-memory sessions. |
| `--session-cookie NAME` | Session cookie name. |
| `--session-timeout-seconds N` | Session idle timeout. |
| `--session-same-site VALUE` | Session SameSite setting. |
| `--session-secure` | Require Secure session cookie. |
| `--operational-external` | Permit enabled health/metrics endpoints beyond loopback. |
| `--structured-log PATH` | JSON-line request log. |
| `--static-files` | Enable static file serving. |
| `--static-max-bytes N` | Static file size limit. |
| `--config FILE` | Load host configuration. |

## FastCGI hosting

Start a persistent FastCGI worker:

```text
xpscript fastcgi --root /srv/xpsite --listen 127.0.0.1:9000
```

A reverse proxy such as nginx forwards FastCGI requests to that private endpoint. Do not expose the FastCGI listener directly to untrusted networks.

## FastCGI parameters

| Parameter | Purpose |
|---|---|
| `--root PATH` | Required web root. |
| `--default-document FILE.xps` | Default route file. |
| `--listen ADDRESS:PORT` | FastCGI TCP endpoint. |
| `--address IP`, `--bind IP` | Listener address. |
| `--port N` | Listener port. |
| `--unix-socket PATH` | Unix socket on Linux/macOS. |
| `--config FILE` | Load FastCGI configuration. |

## CGI hosting

CGI uses the separate `XPScript.Web.Cgi` executable. The web server starts it per request. The host reads CGI variables and stdin, dispatches the `.xps` route and writes CGI headers/body to stdout.

Publish example:

```powershell
dotnet publish .\src\XPScript.Web.Cgi\XPScript.Web.Cgi.csproj -c Release -r win-x64 --self-contained false -o C:\XPScript\cgi
```

CGI is process-per-request. Prefer Kestrel or FastCGI for persistent workers and higher throughput.

## CGI configuration

The CGI host reads its routing information from CGI environment variables. Configure `XPSCRIPT_WEB_ROOT` explicitly when possible. If it is absent, `DOCUMENT_ROOT` can provide the root. `SCRIPT_FILENAME` is used only as a final root-discovery fallback.

Important CGI values include `REQUEST_METHOD`, `QUERY_STRING`, `CONTENT_TYPE`, `CONTENT_LENGTH`, `SCRIPT_NAME`, `PATH_INFO`, `SCRIPT_FILENAME`, `SERVER_NAME`, `SERVER_PORT`, `SERVER_PROTOCOL`, `REMOTE_ADDR` and `HTTPS`.

## Deployment packages

Use `scripts/publish-distributions.ps1` to create clean runtime packages instead of copying `src` directories:

```powershell
./scripts/publish-distributions.ps1 -Package all
```

Outputs are created below `artifacts/distributions/` for compiler, desktop runtime, CGI, FastCGI and Kestrel deployment.
