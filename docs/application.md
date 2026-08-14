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

## Argument ownership and Args array

When the generated .NET entry point initializes `Application`, the runtime copies the supplied `Main(string[] args)` array. The runtime therefore owns its argument state independently; later changes to the original .NET array cannot change values exposed through `Application`.

`Application.Args` without an index returns a defensive-copy XPScript String array containing the current arguments. For normal command-line processing, direct `Application.Args(index)` access is preferred.

The array uses zero-based argument indexes. When no arguments were supplied, `Application.ArgCount` is zero and callers should not index `Application.Args`.

Every full-array read is detached from the runtime-owned argument list and from other returned copies. Modifying one copied array cannot alter the values stored by `Application`, another previously returned copy or a later fresh copy.

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

- `Application.Path` is a strict alias of `Application.ExecutablePath` and returns the same string value.
- `Application.FileName` is a strict alias of `Application.ExecutableFileName` and returns the same string value.

```xpscript
Print Application.Path
Print Application.FileName
```

## Temporary directory

`Application.TempPath` returns the operating system temporary directory reported by .NET.

```xpscript
Print Application.TempPath
```

`Application.TempFolder` is a strict alias for `Application.TempPath`; both properties return the same string value.

```xpscript
Print Application.TempFolder
```

This is the OS/user temporary directory, not the compiler's private build workspace.

## CommandLine

`Application.CommandLine` returns the runtime argument values joined with one space.

```xpscript
Print Application.CommandLine
```

Examples:

- no arguments -> empty string
- `one` -> `one`
- arguments `first`, `two words`, `ÅÄÖ-漢字`, and an empty final argument -> `first two words ÅÄÖ-漢字 `

`Application.CommandLine` is intentionally a convenience representation. It does **not** reconstruct quoting characters removed by the launching shell and it does not preserve exact argument boundaries. In the example above, the text does not reveal whether `two words` was one argument or two separate arguments, and the final empty argument is represented only by the trailing separator.

Use `Application.Args(index)` or the defensive-copy `Application.Args` array whenever exact argument boundaries matter.

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

The Application runtime is continuously verified by `.github/workflows/application-runtime-build.yml` on Windows, Ubuntu and macOS. The workflow compiles and executes the runtime sample, checks command-line argument handling including spaces, empty strings and Unicode, verifies the documented lossy `Application.CommandLine` representation, verifies `Application.Path` exactly equals `Application.ExecutablePath`, verifies `Application.FileName` exactly equals `Application.ExecutableFileName`, verifies `Application.TempFolder` exactly equals `Application.TempPath`, validates executable and temp-path metadata, verifies error 9 for invalid argument indexes, and verifies the read-only compiler rules.

`.github/workflows/application-runtime-isolation.yml` independently compiles the exact generated runtime source on Windows, Ubuntu and macOS. It verifies that runtime initialization copies the original .NET `Main(string[] args)` array and that every full `Application.Args` result is detached from runtime-owned storage and from other returned copies.
