# Compiler and host CLI reference

This page is the searchable reference for the XPScript compiler and host command-line interface. Each command/option includes syntax, parameter meaning, behavior, and a complete `.xps` program that can be used with it.

## `xpscriptc` compiler

| Command/option | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| compile | `xpscriptc source.xps -o output [options]` | `source.xps`: source file; `output`: generated application path. | Compiles an XPScript program and reports progress/timing to stderr while keeping result output on stdout. | [hello.xps](../demo/console/hello.xps) |
| `run` | `xpscriptc run source.xps [arguments...]` | `source.xps`: program to compile/run; following values are exposed through `Application.Args`. | Builds into an isolated framework-dependent temporary output and runs the program immediately. Compiler lifecycle output is quiet by default. | [application-runtime.xps](../samples/application-runtime.xps) |
| `--info` | `xpscriptc run source.xps --info` | none | Shows run compilation progress, elapsed compile time, program start, and exit code on stderr. Without `--info`, `run` emits only program output and errors. | [application-runtime.xps](../samples/application-runtime.xps) |
| `-o` | `-o path` | `path`: output executable/application path. | Selects the compiler output path. | [hello.xps](../demo/console/hello.xps) |
| `--runtime` | `--runtime RID` | `RID`: one of the supported runtime identifiers such as `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`. | Compiles/publishes for an explicit target operating system and architecture. | [platform-shell.xps](../samples/platform-shell.xps) |
| `--framework-dependent` | `--framework-dependent` | none | Produces framework-dependent output instead of a self-contained application. | [hello.xps](../demo/console/hello.xps) |
| `--result-format text` | `--result-format text` | none | Emits human-readable compiler results and diagnostics. | [compiler-errors.xps](../samples/compiler-errors.xps) |
| `--result-format json` | `--result-format json` | none | Emits structured JSON compiler results and diagnostics. | [compiler-errors.xps](../samples/compiler-errors.xps) |
| `--result-format xml` | `--result-format xml` | none | Emits structured XML compiler results and diagnostics. | [compiler-errors.xps](../samples/compiler-errors.xps) |
| `--` | `-- scriptArg1 ...` | all following values are script arguments. | Ends compiler option parsing so option-looking values can be passed to the program. | [application-runtime.xps](../samples/application-runtime.xps) |

## `xpscript service` host installation

| Command/option | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `xpscript service install` | `xpscript service install EXECUTABLE --name NAME --display-name "DISPLAY NAME" [--start MODE]` | `EXECUTABLE`: compiled `[Service]` application. | Registers the compiled executable with Windows SCM, Linux systemd or macOS launchd. Installation refuses to overwrite an existing service with the same internal name. | [service-interval.xps](../samples/service-interval.xps) |
| service `--name` | `--name NAME` | stable native service identifier. | Required. The value is validated before any native service-manager change is attempted. | [service-interval.xps](../samples/service-interval.xps) |
| `--display-name` | `--display-name "DISPLAY NAME"` | human-readable service name. | Required. Windows uses the native DisplayName. Linux uses the systemd Description. macOS stores the display name as XPScript service metadata. | [service-interval.xps](../samples/service-interval.xps) |
| service `--start` | `--start auto|manual|disabled` | startup mode. | Optional, defaults to `manual`. `auto` enables boot startup, `manual` permits manual start without boot startup, and `disabled` configures the native service manager to prevent service start. | [service-interval.xps](../samples/service-interval.xps) |

Service files use `[Service]`, optional `[StopTimeout:value]`, `ServiceStart()`, one or more optional `[Interval:value]` procedures, and `ServiceStop()`. See [XPScript services](services.md) for the lifecycle contract.

## `xpscript web` Kestrel host

| Command/option | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `web` | `xpscript web --root PATH [options]` | `PATH`: XPScript web root. | Starts the persistent Kestrel web host. | [index.xps](../demo/kestrel/index.xps) |
| `--root` | `--root PATH` | `PATH`: directory containing web `.xps` files. | Selects the required web application root. | [index.xps](../demo/kestrel/index.xps) |
| `--default-document` | `--default-document FILE.xps` | `FILE.xps`: default route file, normally `index.xps`. | Changes the file resolved for directory/default-document requests. | [index.xps](../demo/kestrel/index.xps) |
| `--address` | `--address IP` | `IP`: listener address. | Selects the Kestrel bind address. | [index.xps](../demo/kestrel/index.xps) |
| `--bind` | `--bind IP` | `IP`: listener address. | Alias for the Kestrel listener address option. | [index.xps](../demo/kestrel/index.xps) |
| `--port` | `--port N` | `N`: TCP port. | Selects the Kestrel listener port. | [index.xps](../demo/kestrel/index.xps) |
| `--host` | `--host NAME` | `NAME`: accepted HTTP Host value; repeatable. | Adds an allowed public Host value. | [index.xps](../demo/kestrel/index.xps) |
| `--allowed-host` | `--allowed-host NAME` | `NAME`: accepted HTTP Host value; repeatable. | Alias for adding an allowed Host value. | [index.xps](../demo/kestrel/index.xps) |
| `--https-cert` | `--https-cert PATH` | `PATH`: PFX certificate file. | Enables HTTPS using the selected PFX certificate. | [index.xps](../demo/kestrel/index.xps) |
| `--https-cert-password-env` | `--https-cert-password-env NAME` | `NAME`: environment variable holding the PFX password. | Reads the HTTPS certificate password from environment rather than the command line. | [index.xps](../demo/kestrel/index.xps) |
| `--protocols` | `--protocols http1|http2|http1+2` | protocol selection. | Chooses allowed Kestrel HTTP protocol versions. | [index.xps](../demo/kestrel/index.xps) |
| `--health` | `--health` | none | Enables the host health endpoint. | [index.xps](../demo/kestrel/index.xps) |
| `--metrics` | `--metrics` | none | Enables the host metrics endpoint. | [index.xps](../demo/kestrel/index.xps) |
| `--sessions` | `--sessions` | none | Enables in-memory XPScript web sessions. | [index.xps](../demo/web-state/index.xps) |
| `--session-cookie` | `--session-cookie NAME` | `NAME`: cookie name. | Overrides the session cookie name. | [index.xps](../demo/web-state/index.xps) |
| `--session-timeout-seconds` | `--session-timeout-seconds N` | `N`: idle timeout in seconds. | Sets session idle expiration. | [index.xps](../demo/web-state/index.xps) |
| `--session-same-site` | `--session-same-site VALUE` | `VALUE`: supported SameSite setting. | Selects SameSite behavior for the session cookie. | [index.xps](../demo/web-state/index.xps) |
| `--session-secure` | `--session-secure` | none | Requires the session cookie to use the Secure attribute. | [index.xps](../demo/web-state/index.xps) |
| `--operational-external` | `--operational-external` | none | Allows enabled health/metrics endpoints beyond loopback. | [index.xps](../demo/kestrel/index.xps) |
| `--structured-log` | `--structured-log PATH` | `PATH`: JSON-lines log file. | Writes structured request logging to the selected path. | [index.xps](../demo/kestrel/index.xps) |
| `--static-files` | `--static-files` | none | Enables static-file serving from the web root. | [index.xps](../demo/kestrel/index.xps) |
| `--static-max-bytes` | `--static-max-bytes N` | `N`: maximum static response size in bytes. | Limits static-file size. | [index.xps](../demo/kestrel/index.xps) |
| `--config` | `--config FILE` | `FILE`: host configuration file. | Loads Kestrel host configuration. Explicit CLI values override config values. | [index.xps](../demo/kestrel/index.xps) |

## `xpscript fastcgi` host

| Command/option | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `fastcgi` | `xpscript fastcgi --root PATH --listen ADDRESS:PORT` | web root and private FastCGI listener. | Starts a persistent FastCGI worker. | [index.xps](../demo/fastcgi/index.xps) |
| FastCGI `--root` | `--root PATH` | `PATH`: directory containing web `.xps` files. | Selects the required FastCGI web root. | [index.xps](../demo/fastcgi/index.xps) |
| FastCGI `--default-document` | `--default-document FILE.xps` | `FILE.xps`: default route file. | Selects the FastCGI default document. | [index.xps](../demo/fastcgi/index.xps) |
| `--listen` | `--listen ADDRESS:PORT` | listener IP/host and TCP port. | Selects the private FastCGI TCP endpoint. | [index.xps](../demo/fastcgi/index.xps) |
| FastCGI `--address` | `--address IP` | `IP`: listener address. | Selects the FastCGI bind address. | [index.xps](../demo/fastcgi/index.xps) |
| FastCGI `--bind` | `--bind IP` | `IP`: listener address. | Alias for the FastCGI bind address. | [index.xps](../demo/fastcgi/index.xps) |
| FastCGI `--port` | `--port N` | `N`: TCP port. | Selects the FastCGI listener port. | [index.xps](../demo/fastcgi/index.xps) |
| `--unix-socket` | `--unix-socket PATH` | `PATH`: Unix-domain socket path on Linux/macOS. | Uses a Unix-domain socket instead of TCP. | [index.xps](../demo/fastcgi/index.xps) |
| FastCGI `--config` | `--config FILE` | `FILE`: FastCGI configuration file. | Loads FastCGI host configuration. | [index.xps](../demo/fastcgi/index.xps) |

FastCGI should normally listen on a private loopback address or Unix socket behind a reverse proxy.

## `xpscript compile --target webiis`

| Command/option | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `compile` | `xpscript compile main.xps --target webiis [options]` | `main.xps`: mandatory WebIIS build entry. | Builds an IIS-deployable XPScript application package. | [main.xps](../demo/webiis/main.xps) |
| `--target webiis` | `--target webiis` | target value `webiis`. | Selects the direct IIS deployment package target. | [main.xps](../demo/webiis/main.xps) |
| WebIIS `--framework-dependent` | `--framework-dependent` | none | Builds a WebIIS package that requires the matching .NET runtime/hosting bundle on the server. | [main.xps](../demo/webiis/main.xps) |
| WebIIS `-o` | `-o PATH` | `PATH`: deployment output directory outside the source application directory. | Selects the WebIIS package output directory. | [main.xps](../demo/webiis/main.xps) |

For the generated IIS package layout, permissions, ASP.NET Core Module V2 requirements, and deployment workflow, see [WebIIS deployment target](webiis.md).
