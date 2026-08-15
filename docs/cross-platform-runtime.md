# Cross-platform runtime

(c) xpagedeveloper.com 2026

XPScript supports Windows, Linux and macOS runtime targets through explicit runtime identifiers and host-default target selection.

## Platform branching

Use `Platform()` when application behavior must differ by operating system. Bare `Platform` is also supported as an expression and returns the same value as `Platform()`.

```xpscript
Sub Main()
    If Platform() = "Windows" Then
        Print "Running on Windows"
    ElseIf Platform() = "Linux" Then
        Print "Running on Linux"
    ElseIf Platform() = "MacOS" Then
        Print "Running on macOS"
    Else
        Print "Unsupported platform: " & Platform()
    End If
End Sub
```

The runtime names used by XPScript are `Windows`, `Linux`, `MacOS`, `FreeBSD` and `Unknown`. GitHub-hosted runtime verification currently exercises Windows, Linux and macOS.

## Paths

Do not hard-code Windows path separators in portable code.

Windows normally uses `\` as the directory separator. Linux and macOS use `/`.

Prefer paths supplied by configuration, command-line arguments or runtime APIs. XPScript file operations use .NET `Path`, `File`, `Directory` and `FileStream` behavior and therefore follow the target filesystem semantics for absolute paths, case sensitivity, permissions and symbolic links.

## Shell

`Shell()` is routed through the cross-platform runtime with `UseShellExecute = false`. Program/script arguments are tokenized and then passed through `ProcessStartInfo.ArgumentList`, so quoted values containing spaces, Unicode text, empty quoted values and ordinary special characters can be passed as structured arguments without an unnecessary second shell parse.

Windows supports direct executables, `.cmd` and `.bat` through `cmd.exe`, and PowerShell scripts through `pwsh.exe` when available with Windows PowerShell as fallback. Batch-file arguments are deliberately more restrictive because `cmd.exe` is itself a command interpreter: command metacharacters such as `&`, `|`, `<`, `>`, `^`, `%` and `!` are rejected in structured batch arguments rather than being reinterpreted.

Linux and macOS support direct executable files and executable/shebang scripts, `.sh` and `.bash` through `/bin/sh`, and PowerShell scripts through `pwsh` when installed.

### Pipes, redirection and globbing

XPScript does **not** provide an implicit "shell expression" mode inside `Shell()`. A value such as `program | other-program` is not automatically sent to a command interpreter. This is intentional: structured process execution is the safe default and avoids command-injection surprises.

When shell syntax is genuinely required, invoke the desired command interpreter explicitly and pass the shell command as its argument, for example `cmd.exe /d /c ...` on Windows or `/bin/sh -c ...` on Unix-like systems. Code doing this is responsible for shell quoting and must not concatenate untrusted input into the command string.

For untrusted or externally supplied values, prefer a directly executable program or a PowerShell/script file with structured arguments. `Shell()` resolves program names only from absolute `PATH` entries and explicit paths are normalized before execution.

## File locking

Windows and Linux use .NET `FileStream.Lock` and `FileStream.Unlock` for operating-system-backed byte-range locking. Cross-process contention is verified by the `Cross Platform Runtime Verification` workflow.

.NET 10 does not support `FileStream.Lock` or `FileStream.Unlock` on macOS. XPScript therefore returns XPScript runtime error 5 with an explicit unsupported-platform diagnostic on macOS instead of silently weakening the lock semantics. The workflow verifies this behavior on a GitHub-hosted macOS runner.

A future native macOS range-lock implementation must preserve the same range semantics and coexist safely with .NET file sharing before this limitation can be removed.
