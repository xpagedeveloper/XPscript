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


## Diagnostic catalog for AI repair

AI, MCP, IDE and CI consumers should key on `diagnosticCode`, use structured `properties` as authoritative repair context, and never parse `description` or generated C# to reconstruct compiler-owned semantics. After a repair, run validation again and use the new structured result.

### XPS1001-XPS1011: syntax diagnostics

These codes identify XPScript syntax/construct errors: unescaped quote (1001), invalid Nothing comparison (1002), invalid increment syntax (1003), invalid compound assignment (1004), empty Dim (1005), unterminated string (1006), invalid date comparison (1007), missing constructor argument (1008), invalid native constructor (1009), invalid native argument list (1010), and removed native API (1011).

Repairs should modify the reported XPScript construct only. When properties such as `foundOperator` or `expectedConstruct` are present, use them instead of inferring intent from the message.

### XPS2001-XPS2012: semantic diagnostics

`XPS2001` type mismatch, `XPS2002` argument count mismatch, `XPS2003` argument type mismatch, `XPS2004` no matching overload, `XPS2005` ambiguous overload, `XPS2006` duplicate overload, `XPS2007` conflicting class member, `XPS2008` unknown symbol, `XPS2009` unknown member, and `XPS2010`-`XPS2012` callback name/resolution/arity failures.

For overload and symbol repairs, prefer compiler-owned properties such as `symbol`, `receiverType`, `suppliedSignature`, `candidateSignature`, parameter index and expected/actual type. Preserve XPScript symbol casing and ByRef/ByVal semantics. `upstreamCode` is supporting information only.

### XPS3001: target API unavailable

**Category:** `target`

An API, runtime feature, or native dependency is incompatible with the active compilation target.

**Properties:** `symbol`, `target`, `allowedTargets` when known.

**Repair:** move the operation to an allowed target or use an API supported by the active target. For Browser-WASM, privileged resources and credentials remain on the server. Never move secrets into browser code to suppress this diagnostic.

### XPS3002: server-side execution context required

**Category:** `execution-context`

Server-only code was found in a Browser-WASM client context. Typical sources include AI, server databases, Notes runtime state, or other server-only state.

**Properties:** `symbol`, `target`, `currentContext`, `requiredContext`.

**Repair:** move the operation into a supported module Function or Sub and mark that procedure `[ServerSide]` when the server bridge supports it.

```xps
[ServerSide]
Function LoadData() As Variant
    Dim db As New XPDBSQLite("data.db")
    LoadData = db.Query("SELECT * FROM users")
End Function
```

Do not expose database credentials, AI credentials, Notes server state, or equivalent privileged state to Browser-WASM.

### XPS8001-XPS8008: invocation/configuration diagnostics

These cover missing/invalid source input (8001-8003), unsupported runtime identifier (8004), Web/IIS entry/output constraints (8005-8006), invalid source preprocessor configuration (8007), and source preprocessor failure (8008).

Repair project/configuration failures at the configuration boundary rather than rewriting valid application semantics to hide them.

### XPS9001-XPS9002: compiler fallback/internal diagnostics

`XPS9001` is a compilation-failed fallback when no more specific stable diagnostic is available. Prefer accompanying specific diagnostics.

`XPS9002` represents an internal compilation failure. AI repair should not mutate application semantics merely to bypass it. Preserve the reproducing source, target and compiler version for investigation.

## AI repair rules

1. Match the exact stable `diagnosticCode`.
2. Use source range and structured properties before human message text.
3. Make the smallest semantics-preserving XPScript change.
4. Never edit generated C# as the repair.
5. Never weaken a target, security, or execution-context boundary merely to make validation pass.
6. Run `validate --result-format json` after every repair iteration.
7. Ignore unknown properties for forward compatibility.
