# ShellExecute TODO

Goal: add a synchronous process API that complements `Shell()` and `ShellArgs()`.

## API and runtime
- [x] Add an `XPShellResult` runtime type with `ExitCode`, `Output` and `Error`.
- [x] Add `ShellExecute(executable, arguments)` using structured arguments.
- [x] Decide whether a command-string convenience overload is needed and document its trust boundary.
- [x] Redirect and concurrently consume stdout and stderr.
- [x] Wait for completion and return the real child exit code.
- [x] Treat non-zero child exit codes as results, not automatically as XPScript runtime errors.
- [x] Define timeout, process-start failure and cancellation behavior.
- [x] Reuse existing executable resolution and platform-specific script handling where appropriate.
- [x] Keep existing `Shell()` and `ShellArgs()` behavior unchanged.
- [x] Support Windows, Linux and macOS consistently.
- [x] Define unsupported behavior for browser/WASM.
- [x] Normalize process-start exceptions through existing runtime error handling.
- [x] Keep secrets out of diagnostics.

## Compiler integration and tests
- [x] Expose `ShellExecute` to XPScript.
- [x] Make `XPShellResult` properties available through normal property syntax.
- [x] Add the smallest success test first, known stdout and exit code 0.
- [x] Test stdout capture.
- [x] Test stderr capture.
- [x] Test non-zero exit codes.
- [x] Test simultaneous high-volume stdout/stderr to catch pipe deadlocks.
- [x] Test executable-not-found behavior.
- [x] Test structured arguments containing spaces and quotes.
- [x] Test timeout behavior if implemented.
- [ ] Test Windows, Linux and macOS.
- [x] If a large test fails, move the failing case first or add a smaller focused test first, following repository test knowledge.

## Documentation
- [x] Document differences between `Shell()`, `ShellArgs()` and `ShellExecute()`.
- [x] Add a stdout/stderr/exit-code sample.
- [x] Document command-injection risks and recommend structured arguments.
- [x] Add the API to command and language references.
