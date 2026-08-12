# XPScript Application object

`Application` is a read-only runtime object that describes the currently running compiled XPScript program and exposes the command-line arguments supplied to it.

The object is populated automatically before `Sub Main()` or `Sub Initialize()` is called.

## Command-line arguments

Compile an application and run it with arguments:

```text
myapp.exe build --configuration Release "file with spaces.txt"
```

Inside XPScript:

```xpscript
Sub Main()
    Dim i As Integer

    Print "Arguments: " & CStr(Application.ArgCount)

    For i = 0 To Application.ArgCount - 1
        Print CStr(i) & ": " & Application.Args(i)
    Next
End Sub
```

The arguments are zero-based:

- `Application.Args(0)` = `build`
- `Application.Args(1)` = `--configuration`
- `Application.Args(2)` = `Release`
- `Application.Args(3)` = `file with spaces.txt`

The executable filename itself is not part of `Application.Args`; this follows the normal .NET `Main(string[] args)` contract.

`Application.ArgCount` returns the number of available arguments.

Reading an index below zero or greater than or equal to `Application.ArgCount` raises XPScript error 9.

## Args array

`Application.Args` without an index returns a defensive-copy XPScript String array containing the current arguments. For normal command-line processing, direct `Application.Args(index)` access is preferred.

The array uses zero-based argument indexes. When no arguments were supplied, `Application.ArgCount` is zero and callers should not index `Application.Args`.

The returned array is detached from the runtime-owned argument list. Modifying a copied array cannot alter the values stored by `Application`.

## Executable information

`Application.ExecutablePath` returns the full path to the currently running executable.

```xpscript
Print Application.ExecutablePath
```

`Application.ExecutableFileName` returns only the executable filename:

```xpscript
Print Application.ExecutableFileName
```

`Application.ExecutableDirectory` returns the directory containing the executable:

```xpscript
Print Application.ExecutableDirectory
```

Convenience aliases are also available:

- `Application.Path` = `Application.ExecutablePath`
- `Application.FileName` = `Application.ExecutableFileName`

## Temporary directory

`Application.TempPath` returns the operating system temporary directory reported by .NET.

```xpscript
Print Application.TempPath
```

`Application.TempFolder` is an alias for `Application.TempPath`.

This is the OS/user temporary directory, not the compiler's private build workspace.

## CommandLine

`Application.CommandLine` returns the command-line arguments joined as text.

```xpscript
Print Application.CommandLine
```

Use `Application.Args(index)` when exact argument boundaries matter. `Application.CommandLine` is a convenience representation and does not preserve the original quoting characters used by the launching shell.

## Read-only behavior

`Application` is runtime-owned and read-only. XPScript source cannot overwrite properties or individual arguments.

For example, this is invalid:

```xpscript
Application.Args(0) = "changed"
```

and produces a compiler diagnostic.

The identifier `Application` is reserved by the XPScript runtime and cannot be redeclared as a variable, parameter, procedure, class, Type or Enum name.

## Sample

See:

- `samples/application-runtime.xps`

The sample prints executable metadata and enumerates every command-line argument.

## Verification status

The Application runtime is continuously verified by `.github/workflows/application-runtime-build.yml` on Windows, Ubuntu and macOS. The workflow compiles and executes the runtime sample, checks command-line argument handling including spaces, empty strings and Unicode, validates executable and temp-path metadata, verifies error 9 for invalid argument indexes, and verifies the read-only compiler rules.
