# Direct XPScript execution TODO

(c) xpagedeveloper.com 2026

Goal: allow an `.xps` source file to be executed directly without requiring the user to manually create and manage a permanent executable first.

Status:
- `[x]` implemented and verified
- `[ ]` intentionally not implemented yet

## Required behavior

- [x] Add a command/runtime mode that executes an `.xps` script directly with `xpscriptc run script.xps`.
- [x] Support an execution path where source is compiled to a temporary executable and started automatically.
- [ ] If a true hosted/direct execution mode is added later, keep its externally visible behavior compatible with the temporary-executable mode.
- [x] Default working directory is the directory containing the `.xps` script, not the compiler executable directory and not the caller's current directory.
- [x] Relative file paths used by the script resolve relative to the script file by default.
- [x] `Application.ExecutablePath` and executable-related runtime values have documented semantics for direct/temporary execution in `docs/direct-script-execution.md`.
- [x] Script command-line arguments are forwarded unchanged to the executed XPScript program. `--` can terminate compiler option parsing explicitly.
- [x] Preserve the XPScript process exit code and return it from the direct-run command.
- [x] Forward stdout and stderr normally unless a future explicit capture/output mode is selected.
- [x] Temporary build output uses an isolated GUID-based per-run directory to avoid collisions between concurrent executions.
- [x] Temporary executable, generated source/project files and other transient build artifacts are cleaned up after execution when possible.
- [x] Cleanup is constrained to the compiler-owned XPScript temporary root and does not recursively follow symbolic links/reparse points.
- [x] Relative paths, absolute paths, paths containing spaces and non-ASCII characters are supported through normal `Path.GetFullPath` semantics and runtime-verified with Unicode paths.
- [x] Works on Windows, Linux and macOS using the existing target/runtime selection rules. Direct execution rejects a foreign RID because it cannot execute that artifact on the current host.
- [x] Clear compiler diagnostics are returned when direct execution cannot compile the script, and setup/start failures return a non-zero CLI result.

## CLI behavior

```text
xpscriptc run script.xps arg1 arg2
xpscriptc run script.xps -- --runtime value-for-the-script
```

Direct execution compiles framework-dependent output into an isolated compiler-owned temporary directory, starts it with the source directory as working directory, waits for completion, propagates its exit code and removes the temporary directory.

## Regression coverage

The `Direct Script Execution` GitHub Actions matrix runs on Windows, Ubuntu and macOS.

- [x] direct run prints expected output
- [x] command-line arguments arrive in the script unchanged
- [x] current/default path inside the script is the script directory
- [x] relative input/output files are created/read beside the script by default
- [x] paths containing spaces and Unicode characters work
- [x] script compilation errors return failed exit code `2` and normal compiler diagnostics
- [x] script runtime exit codes propagate exactly to the caller
- [x] temporary artifacts are removed after success
- [x] temporary artifacts are safely handled after compile/runtime failure
- [x] simultaneous direct executions use separate temporary build directories and both complete successfully
- [x] Windows/Linux/macOS verification
