# Application.Debug

Use `Application.Debug` for developer-facing runtime diagnostics.

## Activation

`--appdebug` enables application debug output.

`--debug` also enables application debug output because the compiler's internal debug mode mirrors application debug.

`Application.Debug` never enables the compiler's internal debug mode.

## API

- `Application.Debug.Enabled`
- `Application.Debug.Print(value)`
- `Application.Debug.Write(value)`
- `Application.Debug.Info(value)`
- `Application.Debug.Warning(value)`
- `Application.Debug.Error(value)`

## Platform routing

- Android: Android logcat, tag `XPScript`.
- Windows: process console, or an allocated console for a non-interactive GUI process.
- Linux: process console when available.
- macOS: process console when available.

`Application.Debug` remains separate from `Application.Log`.
