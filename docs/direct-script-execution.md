# Direct XPScript execution

(c) xpagedeveloper.com 2026

XPScript source files can be compiled and executed in one command without creating a permanent application file.

## Basic usage

```text
xpscriptc run script.xps
xpscriptc run script.xps first "second value"
```

The compiler builds the script into an isolated compiler-owned temporary directory, starts the generated application, waits for it to exit, returns the application's exit code, and removes the temporary directory when possible.

## Script arguments

Arguments after the source file are passed to the generated XPScript program unchanged.

```text
xpscriptc run script.xps first "second value"
```

Inside the script:

```xpscript
Print Application.ArgCount
Print Application.Args(0)
Print Application.Args(1)
```

Run options are parsed before the first ordinary script argument. Use `--` when a script argument looks like a compiler option.

```text
xpscriptc run script.xps -- --runtime value-for-the-script
```

## Working directory and relative files

Direct execution sets the process working directory to the directory containing the `.xps` source file.

For example, if the script is `/srv/xps/site/index.xps`, then `CurDir()` starts as `/srv/xps/site` and relative file operations such as `Open "data.txt"` resolve from that directory unless the script changes its working directory.

This behavior is independent of the directory from which `xpscriptc` was started.

## Runtime target

Direct execution can run only the current OS and architecture because the generated process must execute on the current host.

The following is valid when the current host matches the selected RID:

```text
xpscriptc run script.xps --runtime linux-x64
```

A foreign target is rejected with a clear diagnostic. Use normal compilation to create artifacts for another platform.

## Application executable properties

During direct execution, `Application.ExecutablePath`, `Application.ExecutableFileName`, `Application.ExecutableDirectory`, `Application.Path`, and `Application.FileName` describe the temporary generated application that is actually running.

They do not return the `.xps` source path.

Use the process working directory for source-relative file behavior. Direct execution sets that working directory to the source script directory before the script starts.

## Output and errors

The generated process inherits normal stdout and stderr behavior from `xpscriptc`.

Compilation failures return compiler diagnostics and exit code `2`.

CLI/setup failures return exit code `1`.

A successfully started script returns the generated application's process exit code.

## Temporary files and concurrency

Each direct execution receives its own GUID-based compiler-owned temporary directory. Concurrent executions therefore do not share generated executables or build artifacts.

Cleanup validates that the target remains inside the compiler-owned XPScript temporary root and does not recursively follow symbolic links or reparse points.
