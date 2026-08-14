# Include source files

(c) xpagedeveloper.com 2026

XPScript source files can include other `.xps` source files into the same compilation unit.

```xps
Include "lib/common.xps"

Sub Main()
    Print CommonMessage()
End Sub
```

## Path resolution

An `Include` path is resolved relative to the `.xps` file that contains the directive. Nested include files therefore resolve their own relative paths from their own directories.

```text
app/
  main.xps
  lib/
    common.xps
    nested/
      helpers.xps
```

`main.xps`:

```xps
Include "lib/common.xps"
```

`lib/common.xps`:

```xps
Include "nested/helpers.xps"
```

Paths are converted to full normalized paths before duplicate/cycle detection, so spellings such as `lib/common.xps` and `./lib/../lib/common.xps` refer to the same include.

Include identity follows the actual source filesystem's case-sensitivity rather than being hard-coded from the operating system. On a case-insensitive filesystem, `Common.xps` and `common.xps` are treated as the same physical include. On a case-sensitive filesystem, two existing files whose names differ only by case remain distinct source files and may both be included. The compiler determines this from existing source filesystem entries without creating probe files in source directories.

This matters on platforms such as macOS where different volumes may use different case-sensitivity settings even though the operating system is the same.

Paths containing spaces and Unicode characters are supported.

## Restricted compilation

Normal compilation keeps the historical Include behavior. Use `--restricted` when compiling untrusted or tenant-controlled source and you want Include reads constrained to trusted source directories.

```text
xpscriptc main.xps --restricted
xpscriptc run main.xps --restricted
```

With `--restricted`, the directory containing the root `.xps` file is the only allowed source root by default. An Include that resolves outside that directory, for example through `..`, is rejected before the file is read.

Trusted shared source directories can be allowed explicitly with `--source-root`. The option may be repeated and automatically enables restricted Include processing.

```text
xpscriptc main.xps --source-root ./src --source-root ../shared-xps
xpscriptc run main.xps --source-root ./src --source-root ../shared-xps
```

Allowed roots and Include paths are normalized to physical paths. Existing symbolic links and reparse points are resolved before containment is accepted, so a path cannot escape an allowed root merely by traversing a link into another directory. The root script itself must also reside beneath one of the configured source roots.

The restriction is enforced during Include expansion immediately before source files are read, and the same policy applies to normal compilation and direct `run` execution.

## Duplicate includes

Each normalized physical source file is expanded at most once per compilation. Repeating an include does not duplicate functions, classes or executable source.

```xps
Include "lib/common.xps"
Include "./lib/../lib/common.xps"
```

The example above includes `common.xps` once. Differently-cased spellings are also deduplicated when the source filesystem itself resolves them to the same physical file.

## Include cycles

Direct and indirect cycles are rejected. Cycle identity uses the same filesystem-aware path rules as duplicate detection. The compiler reports the include chain, for example:

```text
Include cycle detected: a.xps -> b.xps -> c.xps -> a.xps
```

A missing include is also a compile error and reports the source line containing the `Include` directive.

## Diagnostics and source mapping

Include expansion maintains a line-by-line source map. When a compiler diagnostic originates inside an included file, its line number is counted from line 1 of that physical include file and the include file name is included in the diagnostic.

For example, if `lib/common.xps` contains an invalid assignment on its own line 12, the error is reported against `common.xps` line 12 even if the expanded source places that statement hundreds of lines into the combined compilation unit.

The `code` and `markedCode` fields are also loaded from the physical include file. This means the returned source snippet and caret marker correspond to the reported include-local line and position rather than accidentally reading the same line number from the root source file. Existing root-file diagnostics retain their established output contract.

Runtime source-line tracking uses the same include source map. `Erl` therefore reports the physical line number local to the source file containing the running statement. If a runtime error occurs on line 5 of an included file, `Erl` is 5 even when that included statement appears much later in the flattened compilation unit.

This behavior is used by normal compilation and by `xpscriptc run`, and is regression-tested on Windows, Linux and macOS.

## Direct execution

`xpscriptc run` uses the same compiler pipeline, so include resolution, filesystem-aware path identity, restricted source-root enforcement and `Erl` source-line behavior are identical for direct execution and normal compilation.

```text
xpscriptc run main.xps
```

The include files remain source inputs only. They are not copied beside the generated executable.

## Project-level directives

Keep project-level dependency declarations such as managed `Reference` directives in the root source file. Include files are intended for XPScript declarations and executable source. This keeps dependency discovery deterministic before the source files are flattened.
