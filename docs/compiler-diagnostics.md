# Compiler diagnostics

XPScript compiler diagnostics use the original `.xps` source location by default.

## Default diagnostics

Normal `compile` output and `run --info` diagnostics report the original XPScript file, line and position whenever the generated C# compiler can map the error through XPScript source directives.

Generated `Program.cs` locations are internal compiler details and are not shown in normal diagnostics. This applies to text, JSON and XML result formats.

Example:

```text
result: error
errors:
  file: main.xps
  line: 12
  position: 9
  description: Unable to assign String to Integer.
```

## Debug diagnostics

Use `--debug` when investigating compiler or transpiler failures:

```text
xpscript compile main.xps --debug
xpscript run main.xps --info --debug
```

Debug mode keeps the source-mapped `.xps` diagnostic and may also include the generated C# diagnostic with its physical `Program.cs` line and C# diagnostic identifier.

Example debug-only generated diagnostic:

```text
  file: Program.cs
  line: 184
  position: 13
  description: CS0029: Unable to assign String to Integer.
```

`--debug` changes diagnostic visibility only. It does not change XPScript parsing, generated program behavior or error handling. Expected ComputeWithForm validation errors handled by `On Error` are omitted from runtime debug exception traces, while `Err` and the failed-field array retain their normal values.
