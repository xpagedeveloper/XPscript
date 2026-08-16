# ShellArgs

`ShellArgs(program, arguments [, windowStyle])` starts a process with structured arguments.

`arguments` must be an array or list. Each value is passed as one element of .NET `ProcessStartInfo.ArgumentList`. XPScript does not join the values into a command string and does not parse the values as shell syntax.

```xpscript
Option Declare

Sub Main()
    Dim args(2) As String
    args(0) = "--name"
    args(1) = "value with spaces"
    args(2) = "literal & | characters"

    Call ShellArgs("myprogram", args)
End Sub
```

Bare program names use the same hardened executable lookup as `Shell()`. Only absolute directories from `PATH` are searched. Relative `PATH` entries and implicit current-directory lookup are ignored.

For scripts, invoke the interpreter explicitly. For example:

```xpscript
Dim args(2) As String
args(0) = "-NoProfile"
args(1) = "-File"
args(2) = "./script.ps1"
Call ShellArgs("pwsh", args)
```

Use `ShellArgs` when argument boundaries must be preserved or values may contain spaces or shell metacharacters. Use `Shell()` when compatibility with a command-style string is required. Explicit calls to `cmd.exe`, `/bin/sh`, `pwsh` or another interpreter remain an application-controlled shell boundary.
