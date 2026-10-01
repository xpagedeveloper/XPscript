# Application debug console

XPScript should distinguish the compiler's internal `--debug` option from application runtime debugging.

## CLI switch

Use `--appdebug` for application debug output.

`--debug` remains the internal compiler/CLI diagnostic switch, but it also enables application debug output as a one-way compatibility mirror.

The effective application debug state is enabled when either `--appdebug` or `--debug` is present.

`--debug` must not be enabled by `Application.Debug` or by `--appdebug`.

## XPScript API

Add an `Application.Debug` runtime object with platform-independent methods such as:

- `Application.Debug.Print(value)`
- `Application.Debug.Write(value)`
- `Application.Debug.Info(value)`
- `Application.Debug.Warning(value)`
- `Application.Debug.Error(value)`

Calls should be no-ops when the application was not started with `--appdebug`, unless the platform has an explicitly configured debug console.

The API should expose a read-only enabled state, for example `Application.Debug.Enabled`, so application code can avoid expensive debug formatting when debugging is disabled.

## Platform routing

The runtime should route enabled application debug messages to the platform's available debug console:

- Android: logcat, using a stable `XPScript` tag.
- Windows: a console window when the application has no usable console and app-debug mode requests one.
- Linux: the process console when available, otherwise an explicitly created debug terminal only where supported.
- macOS: the process console when available, otherwise an explicitly created debug console where supported.

The platform adapter must not require a debug window for normal application execution.

## Separation of concerns

`Application.Debug` is application runtime functionality.

`--debug` is an internal compiler/CLI diagnostic facility.

`--appdebug` controls application debug output at process startup.

Existing `Application.Log.*` remains a separate application logging API. It must not silently become dependent on `--appdebug`.

## Testability

Add platform-neutral compiler regression coverage for `Application.Debug` calls.

Add platform-specific smoke tests verifying output routing:

- Android via logcat.
- Windows via a debug console.
- Linux via the process/debug console.
- macOS via the process/debug console.

Use a stable machine-readable exit marker such as `XPSCRIPT-EXIT=<code>` for automated runtime tests.
