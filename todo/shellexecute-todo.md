# ShellExecute TODO

Goal: add a synchronous process API that complements `Shell()` and `ShellArgs()`.

## API and runtime
- [ ] Add an `XPShellResult` runtime type with `ExitCode`, `Output` and `Error`.
- [ ] Add `ShellExecute(executable, arguments)` using structured arguments.
- [ ] Decide whether a command-string convenience overload is needed and document its trust boundary.
- [ ] Redirect and concurrently consume stdout and stderr.
- [ ] Wait for completion and return the real child exit code.
- [ ] Treat non-zero child exit codes as results, not automatically as XPScript runtime errors.
- [ ] Define timeout, process-start failure and cancellation behavior.
- [ ] Reuse existing executable resolution and platform-specific script handling where appropriate.
- [ ] Keep existing `Shell()` and `ShellArgs()` behavior unchanged.
- [ ] Support Windows, Linux and macOS consistently.
- [ ] Define unsupported behavior for browser/WASM.
- [ ] Normalize process-start exceptions through existing runtime error handling.
- [ ] Keep secrets out of diagnostics.

## Compiler integration and tests
- [ ] Expose `ShellExecute` to XPScript.
- [ ] Make `XPShellResult` properties available through normal property syntax.
- [ ] Add the smallest success test first, known stdout and exit code 0.
- [ ] Test stdout capture.
- [ ] Test stderr capture.
- [ ] Test non-zero exit codes.
- [ ] Test simultaneous high-volume stdout/stderr to catch pipe deadlocks.
- [ ] Test executable-not-found behavior.
- [ ] Test structured arguments containing spaces and quotes.
- [ ] Test timeout behavior if implemented.
- [ ] Test Windows, Linux and macOS.
- [ ] If a large test fails, move the failing case first or add a smaller focused test first, following repository test knowledge.

## Documentation
- [ ] Document differences between `Shell()`, `ShellArgs()` and `ShellExecute()`.
- [ ] Add a stdout/stderr/exit-code sample.
- [ ] Document command-injection risks and recommend structured arguments.
- [ ] Add the API to command and language references.
