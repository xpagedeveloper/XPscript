# Cross-platform runtime

(c) xpagedeveloper.com 2026

XPScript supports Windows, Linux and macOS runtime targets through explicit runtime identifiers and host-default target selection.

## Platform branching

Use `Platform()` when application behavior must differ by operating system.

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

`Shell()` is routed through the cross-platform runtime and can run ordinary executables, supported script files, operating-system shell built-ins and commands that use shell operators such as pipes and redirection.

`Shell()` waits for the launched process to finish, captures its standard output, and returns that output as an XPScript `String`.

```xpscript
Dim Result As String
Result = Shell("dotnet --version")
Print Result

Result = Shell("echo hello | findstr hello")   ' Windows
Result = Shell("printf hello | grep hello")   ' Linux/macOS
```

`Call Shell("...")` is also valid when the caller does not need the returned text; in that form the captured standard output is simply ignored.

For ordinary executables, XPScript first resolves the executable from an explicit path or absolute PATH entries and uses structured `ProcessStartInfo.ArgumentList` arguments. This path avoids unnecessary shell parsing.

When the first command is a shell built-in or the command contains unquoted shell operators such as `|`, `>`, `<`, `&` or `;`, XPScript intentionally invokes the platform command interpreter: `cmd.exe /d /s /c` on Windows and `/bin/sh -c` on Linux/macOS. Standard output from that command interpreter is returned by `Shell()` in the same way as direct executable output.

Windows also supports `.cmd` and `.bat` through `cmd.exe`, and PowerShell scripts through `pwsh.exe` when available with Windows PowerShell as fallback. Linux and macOS support direct executable files and scripts, `.sh` and `.bash` through `/bin/sh`, and PowerShell scripts through `pwsh` when installed.

Direct executables, PowerShell scripts and Unix shell scripts use structured arguments. Windows `.cmd`/`.bat` execution requires a validated `cmd.exe /c` command string; XPScript rejects command-shell metacharacters in batch paths and arguments before building that command string.

A raw shell command is intentionally interpreted by the operating-system shell. Do not concatenate untrusted data into such commands. Prefer direct executable invocation and structured arguments whenever user-controlled values are involved.

## File locking

Windows and Linux use .NET `FileStream.Lock` and `FileStream.Unlock` for operating-system-backed byte-range locking. Cross-process contention is verified by the `Cross Platform Runtime Verification` workflow.

.NET 10 does not support `FileStream.Lock` or `FileStream.Unlock` on macOS. XPScript therefore returns XPScript runtime error 5 with an explicit unsupported-platform diagnostic on macOS instead of silently weakening the lock semantics. The workflow verifies this behavior on a GitHub-hosted macOS runner.

A future native macOS range-lock implementation must preserve the same range semantics and coexist safely with .NET file sharing before this limitation can be removed.
