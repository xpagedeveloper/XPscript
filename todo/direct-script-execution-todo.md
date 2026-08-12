# Direct XPScript execution TODO

(c) xpagedeveloper.com 2026

Goal: allow an `.xps` source file to be executed directly without requiring the user to manually create and manage a permanent executable first.

## Required behavior

- [ ] Add a command/runtime mode that executes an `.xps` script directly.
- [ ] Support an execution path where source is compiled to a temporary executable and started automatically.
- [ ] If a true hosted/direct execution mode is added later, keep its externally visible behavior compatible with the temporary-executable mode.
- [ ] Default working directory must be the directory containing the `.xps` script, not the compiler executable directory and not the caller's current directory.
- [ ] Relative file paths used by the script must therefore resolve relative to the script file by default.
- [ ] `Application.ExecutablePath` / executable-related runtime values must have documented semantics for direct/temporary execution.
- [ ] Script command-line arguments must be forwarded unchanged to the executed XPScript program.
- [ ] Preserve the XPScript process exit code and return it from the direct-run command.
- [ ] Forward stdout and stderr normally unless an explicit capture/output mode is selected.
- [ ] Temporary build output must use an isolated per-run directory to avoid collisions between concurrent executions.
- [ ] Temporary executable, generated source/project files and other transient build artifacts must be cleaned up after execution when possible.
- [ ] Cleanup must not delete user source files or unrelated files if execution/build fails.
- [ ] Define behavior for scripts started through relative paths, absolute paths and paths containing spaces/non-ASCII characters.
- [ ] Work on Windows, Linux and macOS using the existing target/runtime selection rules.
- [ ] Add clear diagnostics when direct execution cannot compile or start the script.

## Suggested CLI behavior

Examples of acceptable public interfaces:

```text
xpscriptc run script.xps arg1 arg2
xpscript script.xps arg1 arg2
```

The exact command name can be chosen during implementation, but running a script must not require the caller to manually choose an output `.exe` path.

## Regression coverage

- [ ] direct run prints expected output
- [ ] command-line arguments arrive in the script unchanged
- [ ] current/default path inside the script is the script directory
- [ ] relative input/output files are created/read beside the script by default
- [ ] paths containing spaces and Unicode characters work
- [ ] script compilation errors return a failed exit code and normal compiler diagnostics
- [ ] script runtime exit codes propagate to the caller
- [ ] temporary artifacts are removed after success
- [ ] temporary artifacts are safely handled after compile/runtime failure
- [ ] simultaneous direct executions do not share temporary build directories
- [ ] Windows/Linux/macOS verification
