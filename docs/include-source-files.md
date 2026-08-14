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

Paths are converted to full normalized paths before duplicate/cycle detection, so spellings such as `lib/common.xps` and `./lib/../lib/common.xps` refer to the same include on the same platform. Windows path comparison is case-insensitive. Linux and macOS path comparison is case-sensitive, matching the compiler's conservative platform path semantics.

Paths containing spaces and Unicode characters are supported.

## Duplicate includes

Each normalized source file is expanded at most once per compilation. Repeating an include does not duplicate functions, classes or executable source.

```xps
Include "lib/common.xps"
Include "./lib/../lib/common.xps"
```

The example above includes `common.xps` once.

## Include cycles

Direct and indirect cycles are rejected. The compiler reports the include chain, for example:

```text
Include cycle detected: a.xps -> b.xps -> c.xps -> a.xps
```

A missing include is also a compile error and reports the source line containing the `Include` directive.

## Direct execution

`xpscriptc run` uses the same compiler pipeline, so include resolution is identical for direct execution and normal compilation.

```text
xpscriptc run main.xps
```

The include files remain source inputs only. They are not copied beside the generated executable.

## Current source-mapping limitation

Include expansion currently produces one flattened compilation unit before the existing validation/transpilation stages. Diagnostics raised by the `Include` processor itself identify the containing source file and line, including missing files and cycles. Full physical file/line mapping for arbitrary syntax/type errors inside included files, and per-file `Erl` preservation, remain follow-up work requiring a multi-file source map.

## Project-level directives

Keep project-level dependency declarations such as managed `Reference` directives in the root source file. Include files are intended for XPScript declarations and executable source. This keeps dependency discovery deterministic before the source files are flattened.
