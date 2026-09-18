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


## Machine-readable compiler contract

Use `--result-format json` for CI, editor integrations and other machine consumers. Schema version 1 is defined by:

- `schemas/compiler-result.schema.json`
- `schemas/compiler-diagnostic.schema.json`

A result identifies the schema, schema version, compiler version and operation. When available it also contains the normalized target runtime identifier and the entry XPScript filename. Validation results never contain an output artifact.

Schema version 1 retains the historical `errors` collection name and the historical diagnostic `code` field. In v1, `code` contains the source line, not the stable diagnostic identifier. New consumers should use `diagnosticCode` for stable `XPSxxxx` identifiers and `sourceText` when they want the source-line alias.

Optional fields may be added only when doing so remains compatible with the published schema. Consumers should select behavior from `schema` and `schemaVersion`, not from the compiler version. A change that removes or renames a field, changes its meaning or type, changes required-field semantics, or otherwise makes an existing valid v1 document invalid requires a new schema version.

The repository schemas are intentionally closed with `additionalProperties: false`. Therefore any new wire field must first be represented by the current schema if it is to remain in schema version 1. Changes that cannot be represented without invalidating the v1 contract require schema version 2 or later.

## Stable diagnostic identifiers

Stable XPScript diagnostic identifiers are separate from human-readable messages and from upstream compiler identifiers. The current ranges are:

```text
XPS1xxx  parser and syntax
XPS2xxx  symbols, types, arguments and overload resolution
XPS3xxx  semantic validation
XPS4xxx  target, platform and execution boundaries
XPS5xxx  security and static analysis
XPS6xxx  code generation
XPS7xxx  external and runtime dependencies
XPS8xxx  project, configuration and invocation
XPS9xxx  internal compiler diagnostics
```

An upstream compiler identifier such as `CS0103` is exposed separately as `upstreamCode` when useful. Consumers must not parse the human-readable `description` to discover either identifier.

Severity values in the machine contract are `error`, `warning` and `info`.

## Validation-only mode

```text
xpscript validate program.xps --result-format json
```

Validation runs the normal XPScript preprocessing and transpilation pipeline and validates the generated C# through the .NET compiler. It performs this without publishing or returning an executable. This keeps validation diagnostics aligned with compilation failures that are only detectable after C# generation.

The JSON document is written to standard output. Machine consumers should treat standard output as the result channel and must not depend on human-readable diagnostic text.
