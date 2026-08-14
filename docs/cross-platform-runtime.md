# Cross-platform runtime

(c) xpagedeveloper.com 2026

XPScript supports Windows, Linux and macOS runtime targets through explicit runtime identifiers and host-default target selection.

## Platform branching

Use `Platform()` when application behavior must differ by operating system. Both `Platform()` and bare `Platform` are supported expression forms.

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

`Shell()` is routed through the cross-platform runtime.

Windows supports direct executables, `.cmd` and `.bat` through `cmd.exe`, and PowerShell scripts through `pwsh.exe` when available with Windows PowerShell as fallback.

Linux and macOS support direct executable files and scripts, `.sh` and `.bash` through `/bin/sh`, and PowerShell scripts through `pwsh` when installed.

For direct executables and PowerShell/script routing, arguments are parsed into structured values and added with `ProcessStartInfo.ArgumentList`. Quoted arguments, spaces, Unicode text and explicit empty arguments are preserved instead of being flattened into an OS shell command line.

XPScript does not add an implicit shell-expansion mode for pipes, redirection or globbing. Code that intentionally needs shell syntax must opt in explicitly by launching the platform shell, for example `cmd.exe /c ...` on Windows or `/bin/sh -c ...` on Unix-like systems. Once an explicit command shell is requested, that shell's parsing and injection rules apply to the command string supplied by the application.

Program and script resolution accepts explicit paths or searches only absolute directories from `PATH`; relative/current-directory PATH entries are ignored. Windows batch routing additionally rejects command-shell metacharacters in structured arguments because `cmd.exe` would otherwise reinterpret them. Prefer a directly executable program or a PowerShell script when arbitrary structured data must be passed.

Do not build explicit shell command strings from untrusted input. Keep untrusted data in structured arguments whenever possible.

## File locking

Windows and Linux use .NET `FileStream.Lock` and `FileStream.Unlock` for operating-system-backed byte-range locking. Cross-process contention is verified by the `Cross Platform Runtime Verification` workflow.

.NET 10 does not support `FileStream.Lock` or `FileStream.Unlock` on macOS. XPScript therefore returns XPScript runtime error 5 with an explicit unsupported-platform diagnostic on macOS instead of silently weakening the lock semantics. The workflow verifies this behavior on a GitHub-hosted macOS runner.

A future native macOS range-lock implementation must preserve the same range semantics and coexist safely with .NET file sharing before this limitation can be removed.
