# Compiler and host CLI reference

This page is the searchable reference for the XPScript compiler and host command-line interface. Each command/option includes syntax, parameter meaning, behavior, and a complete `.xps` program that can be used with it.

## `xpscript` compiler and runtime

Running `xpscript` (or the shipped `xpscriptc` executable) with no arguments prints the built-in CLI usage reference. `compile` and `run` use the AST compiler by default. The explicit `ast-compile` and `ast-run` names are available when a script or automation wants to state that choice directly. During migration, `legacy-compile`/`compilev1` and `legacy-run`/`runv1` retain the previous compiler behavior. For `run` and `compile`, `--info` may be placed before or after the command name. Program arguments for `run` are accepted only through `--Args`, which keeps XPScript CLI options unambiguous regardless of their position.


| Command/option | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| compile | `xpscript compile source.xps -o output [options]` | `source.xps`: source file; `output`: generated application path. | Compiles an XPScript program through the AST compiler and reports progress/timing to stderr while keeping result output on stdout. | [hello.xps](../demo/console/hello.xps) |
| `ast-compile` | `xpscript ast-compile source.xps -o output [options]` | source file and output path. | Explicit AST compiler spelling; equivalent to `compile`. | [hello.xps](../demo/console/hello.xps) |
| `legacy-compile` / `compilev1` | `xpscript legacy-compile source.xps -o output [options]` | source file and output path. | Uses the legacy compiler during migration. `compilev1` is an equivalent compatibility alias. | [hello.xps](../demo/console/hello.xps) |
| `run` | `xpscript run source.xps [--Args "arg1 arg2 ..."]` | `source.xps`: program to compile/run; `--Args`: explicit argument string whose values are exposed through `Application.Args`. | Builds into an isolated framework-dependent temporary output and runs the program immediately. Compiler lifecycle output is quiet by default. | [application-runtime.xps](../samples/application-runtime.xps) |
| `ast-run` | `xpscript ast-run source.xps [--Args "arg1 arg2 ..."]` | source and optional explicit program arguments. | Explicit AST compiler spelling; equivalent to `run`. | [application-runtime.xps](../samples/application-runtime.xps) |
| `legacy-run` / `runv1` | `xpscript legacy-run source.xps [options]` | source and legacy run options. | Uses the legacy compiler/runtime path during migration. `runv1` is an equivalent compatibility alias. | [application-runtime.xps](../samples/application-runtime.xps) |
| `validate` | `xpscript validate source.xps [--platform RID] [--result-format text\|json\|xml]` | source and optional target/result format. | Validates the source without producing or running an application. | [compiler-errors.xps](../samples/compiler-errors.xps) |
| `validate --stdin` | `xpscript validate --stdin [--filename NAME.xps]` | source is read from standard input; optional virtual filename. | Validates piped source while preserving a stable filename in diagnostics. `--stdin` cannot be combined with a source path. | [compiler-errors.xps](../samples/compiler-errors.xps) |
| `mcp` | `xpscript mcp` | none. | Starts the compiler machine/MCP server on standard input/output. | [Compiler machine interface](compiler-machine-interface.md) |
| `mcp install` | `xpscript mcp install codex\|claude [--scope user\|project] [--force]` | client, scope and optional overwrite flag. | Installs the compiler MCP registration for the selected client and scope. | [Compiler machine interface](compiler-machine-interface.md) |
| `--info` | `xpscript --info run source.xps` or `xpscript run source.xps --info` | none | Shows compilation/runtime progress and automatically enables application dependency security warning mode unless `--security=off` is explicit. Option names are case-insensitive. | [application-runtime.xps](../samples/application-runtime.xps) |
| `--debug` | `xpscript run source.xps --debug` or `xpscript compile source.xps --debug` | none | Enables `--info`, forces a fresh run compilation, shows timing and generated-C# diagnostics, and enables detailed handled-runtime exception tracing. | [application-runtime.xps](../samples/application-runtime.xps) |
| `--No-Daemon` | `xpscript run source.xps --No-Daemon` or `xpscript legacy-run source.xps --No-Daemon` | none | Prevents the run command from using the warm compiler daemon and forces an isolated local compilation. It is accepted by `ast-run` for CLI compatibility but has no daemon to disable there. | [application-runtime.xps](../samples/application-runtime.xps) |
| `--security` | `--security=off|warn|strict` | security mode. | Controls NuGet application dependency auditing. `warn` reports findings; `strict` also fails when audit data is unavailable and blocks high/critical findings. | [Dependency security and package patching](dependency-security-and-patching.md) |
| `-o` | `-o path` | `path`: output executable/application path. | Selects the compiler output path. | [hello.xps](../demo/console/hello.xps) |
| `--output` | `--output path` | `path`: output executable/application path. | Long-form alias for `-o`. | [hello.xps](../demo/console/hello.xps) |
| `--platform` / `--rid` | `--platform RID` or `--rid RID` | `RID`: one of the supported runtime identifiers such as `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`. | Compiles/publishes for an explicit target operating system and architecture. | [platform-shell.xps](../samples/platform-shell.xps) |
| `--target` | `--target desktop\|console\|webiis` | target kind. | Selects the output host: desktop (default), console, or an IIS-deployable WebIIS package. | [hello.xps](../demo/console/hello.xps) |
| `--runtime` | `--runtime=true|false` | boolean | Includes (`true`) or excludes (`false`, default) the .NET 10 runtime from the published application. | [hello.xps](../demo/console/hello.xps) |
| `--single-file` | `--single-file=true|false` | boolean | Bundles application-managed files into the executable when `true` (default); emits them as separate files when `false`. | [hello.xps](../demo/console/hello.xps) |
| `--singlefile` | `--singlefile=true|false` | boolean | Compatibility alias for `--single-file`; the spelling is accepted case-insensitively. | [hello.xps](../demo/console/hello.xps) |
| `--embed-assets` | `--embed-assets` | none | Embeds a UIForm application's `assets/` tree in a desktop executable; startup materializes it beside the executable. | [hello.xps](../demo/console/hello.xps) |
| `--restricted` | `--restricted` | none | Restricts `Include` reads to the script directory, unless additional `--source-root` directories are supplied. | [application-runtime.xps](../samples/application-runtime.xps) |
| `--source-root` | `--source-root DIR` (repeatable) | directory path. | Adds an allowed root for restricted `Include` processing; using it automatically enables restricted Include processing. | [application-runtime.xps](../samples/application-runtime.xps) |
| `--preprocessor` | `--preprocessor SPEC` (repeatable) | preprocessor specification. | Runs the selected preprocessors after the complete Include graph has been expanded. | [application-runtime.xps](../samples/application-runtime.xps) |
| `--result-format text` | `--result-format text` | none | Emits human-readable compiler results and diagnostics. | [compiler-errors.xps](../samples/compiler-errors.xps) |
| `--result-format json` | `--result-format json` | none | Emits structured JSON compiler results and diagnostics. | [compiler-errors.xps](../samples/compiler-errors.xps) |
| `--result-format xml` | `--result-format xml` | none | Emits structured XML compiler results and diagnostics. | [compiler-errors.xps](../samples/compiler-errors.xps) |
| `--Args` | `--Args "first second"` | one quoted argument string. | Passes arguments only to the program started by `run`. `xpscript run main.xps --Args "first second"` is equivalent to starting the generated Windows program as `main.exe first second`. | [application-runtime.xps](../samples/application-runtime.xps) |

Option names are case-insensitive, so `--Args`, `--args`, `--No-Daemon` and
`--no-daemon` are equivalent. `--Args` is a single quoted CLI value; the
compiler splits that value using the same argument quoting rules used for a
native process and forwards the resulting values to `Application.Args`. It
does not change compiler options inside the argument string.

## Dependency security and package patch commands

| Command/option | Syntax | Parameters | Description |
|---|---|---|---|
| `dependencies` | `xpscript dependencies SOURCE [--platform RID] [--json]` | XPScript source and optional target RID. | Reports the direct and transitive NuGet graph required by that application. |
| `security` | `xpscript security SOURCE [--platform RID] [--json]` | XPScript source and optional target RID. | Checks the application's resolved NuGet graph for known vulnerabilities. Exit code `2` means vulnerabilities were found; `3` means vulnerability data was unavailable. |
| `patch all --check` | `xpscript patch all --check` | none | Shows compatible same-major/minor stable patch releases without changing patch state. |
| `patch all` | `xpscript patch all` | none | Downloads and activates eligible compatible patch releases for the current XPScript release line. |
| `patch PACKAGE` | `xpscript patch PACKAGE [--check]` | NuGet package ID. | Checks or applies a compatible patch for one package. A member of an atomic package group can cause the whole group to be evaluated together. |
| `--security-only` | `xpscript patch all --security-only` | none | Applies only candidates classified as security patches; maintenance patches and unverifiable security states are skipped. |
| `patch status` | `xpscript patch status` | none | Shows patch state for the current XPScript version. |
| `patch list` | `xpscript patch list` | none | Lists the current XPScript version's selected package patches. |
| `patch remove all` | `xpscript patch remove all` | none | Removes the current XPScript version's selected patch state. |

See [Dependency security and package patching](dependency-security-and-patching.md) for the compatibility policy, security classification and version-scoped cache behavior.

## `xpscript service` host installation

| Command/option | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `xpscript service install` | `xpscript service install EXECUTABLE --name NAME --display-name "DISPLAY NAME" [--start MODE]` | `EXECUTABLE`: compiled `[Service]` application. | Registers the compiled executable with Windows SCM, Linux systemd or macOS launchd. Installation refuses to overwrite an existing service with the same internal name. | [service-interval.xps](../samples/service-interval.xps) |
| service `--name` | `--name NAME` | stable native service identifier. | Required. The value is validated before any native service-manager change is attempted. | [service-interval.xps](../samples/service-interval.xps) |
| `--display-name` | `--display-name "DISPLAY NAME"` | human-readable service name. | Required. Windows uses the native DisplayName. Linux uses the systemd Description. macOS stores the display name as XPScript service metadata. | [service-interval.xps](../samples/service-interval.xps) |
| service `--start` | `--start auto|manual|disabled` | startup mode. | Optional, defaults to `manual`. `auto` enables boot startup, `manual` permits manual start without boot startup, and `disabled` configures the native service manager to prevent service start. | [service-interval.xps](../samples/service-interval.xps) |

Service files use `[Service]`, optional `[StopTimeout:value]`, `ServiceStart()`, one or more optional `[Interval:value]` procedures, and `ServiceStop()`. See [XPScript services](services.md) for the lifecycle contract.

For the complete JSON `--config` file format and all supported `web` and `fastCgi` properties, see [Web and FastCGI host configuration](host-config.md).\n\n## `xpscript web` Kestrel host

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
| `--operational-allow` | `--operational-allow` | none | Compatibility alias for `--operational-external`. | [index.xps](../demo/kestrel/index.xps) |
| `--structured-log` | `--structured-log PATH` | `PATH`: JSON-lines log file. | Writes structured request logging to the selected path. | [index.xps](../demo/kestrel/index.xps) |
| `--log-directory` | `--log-directory PATH` | `PATH`: log directory. | Selects the directory for mandatory structured web logs. | [index.xps](../demo/kestrel/index.xps) |
| `--static-files` | `--static-files` | none | Enables static-file serving from the web root. | [index.xps](../demo/kestrel/index.xps) |
| `--static-max-bytes` | `--static-max-bytes N` | `N`: maximum static response size in bytes. | Limits static-file size. | [index.xps](../demo/kestrel/index.xps) |
| `--static-allow` | `--static-allow PATH` | `PATH`: allowed static path prefix. | Adds an explicit static-file path allowlist entry. | [index.xps](../demo/kestrel/index.xps) |
| `--environment` | `--environment Production|Development` | environment name. | Selects the web runtime environment. | [index.xps](../demo/kestrel/index.xps) |
| `--api-docs` / `--no-api-docs` | `--api-docs` or `--no-api-docs` | none | Enables or disables generated REST/OpenAPI documentation for the web root. | [REST API documentation](rest-api-documentation.md) |
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
| FastCGI `--log-directory` | `--log-directory PATH` | `PATH`: log directory. | Selects the directory for mandatory structured FastCGI logs. | [index.xps](../demo/fastcgi/index.xps) |
| FastCGI `--environment` | `--environment Production|Development` | environment name. | Selects the web runtime environment. | [index.xps](../demo/fastcgi/index.xps) |
| FastCGI `--config` | `--config FILE` | `FILE`: FastCGI configuration file. | Loads FastCGI host configuration. | [index.xps](../demo/fastcgi/index.xps) |

FastCGI should normally listen on a private loopback address or Unix socket behind a reverse proxy.

## `xpscript openapi`

| Command/option | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `openapi generate` | `xpscript openapi generate SPEC [-o OUTPUT] [--force]` | OpenAPI specification and generated server path. | Generates REST server source; existing output requires `--force`. | [REST API](rest-api.md) |
| `openapi import` | `xpscript openapi import SPEC [-o OUTPUT] [--operation SELECTOR] [--status-file PATH]` | specification, operation selector(s), optional status JSON path. | Imports a bounded OpenAPI contract; import does not accept `--force`. | [REST API](rest-api.md) |
| `openapi client generate` | `xpscript openapi client generate SPEC [-o OUTPUT] [--class NAME] [--operation SELECTOR]` | specification, output path, optional class and operation selector(s). | Generates an XPScript REST client and typed models. | [REST API](rest-api.md) |
| `-o` / `--output` | `-o PATH` or `--output PATH` | generated source path. | Selects the server or client output file. | [REST API](rest-api.md) |
| `--force` | `--force` | none. | Allows `openapi generate` to replace an existing server output. | [REST API](rest-api.md) |
| `--operation` | `--operation SELECTOR[,SELECTOR...]` | operationId, path, or `METHOD path`. | Limits generation/import to selected operations. | [REST API](rest-api.md) |
| `--status-file` | `--status-file PATH` | JSON status-file path. | Writes import progress and operation/model counts. | [REST API](rest-api.md) |
| `--class` | `--class NAME` | XPScript class name. | Selects the generated client class name. | [REST API](rest-api.md) |

## `xpscript compile --target webiis`

| Command/option | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `compile` | `xpscript compile main.xps --target webiis [options]` | `main.xps`: mandatory WebIIS build entry. | Builds an IIS-deployable XPScript application package. | [main.xps](../demo/webiis/main.xps) |
| `--target webiis` | `--target webiis` | target value `webiis`. | Selects the direct IIS deployment package target. | [main.xps](../demo/webiis/main.xps) |
| WebIIS `--runtime=false` | `--runtime=false` | none | Builds a WebIIS package that requires the matching .NET runtime/hosting bundle on the server. | [main.xps](../demo/webiis/main.xps) |
| WebIIS `-o` | `-o PATH` | `PATH`: deployment output directory outside the source application directory. | Selects the WebIIS package output directory. | [main.xps](../demo/webiis/main.xps) |

For the generated IIS package layout, permissions, ASP.NET Core Module V2 requirements, and deployment workflow, see [WebIIS deployment target](webiis.md).
