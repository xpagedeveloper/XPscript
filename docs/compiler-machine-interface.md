# Compiler machine interface

The compiler machine interface is the stable boundary used by CLI tooling, CI, tests and future IDE/LSP or MCP integrations. Normal XPScript compilation remains the source of truth. Validation reuses the same preprocessing, dependency validation, transpilation and generated-code validation pipeline without publishing an executable.

## Result-format implementation

`XPScriptCompilerCommandLine` parses `--result-format text|json|xml` for compile and validate operations and routes the final `CompileResult` through `WriteResult`. JSON is the primary machine-readable representation. XML uses the same result and diagnostic objects. Text is the human representation.

Schema version 1 keeps the historical `errors` collection and the historical diagnostic `code` field, where `code` means redacted source text rather than a diagnostic identifier. New consumers should use `diagnosticCode` for stable XPS identifiers and `sourceText` for source text. `upstreamCode` preserves identifiers such as Roslyn CS codes.

The result contract is implemented by `CompileResult`, `CompileSource`, `CompileDiagnostic` and `CompileDiagnosticProperty`. JSON Schemas are stored in `schemas/compiler-result.schema.json` and `schemas/compiler-diagnostic.schema.json`.

## Diagnostic producers

Diagnostics currently originate from several layers:

* compiler-owned language validators, including source type and class overload/member validation;
* `CompilerException`, which can carry structured generated diagnostics;
* `CompilerDiagnosticParser`, which maps compiler/build output back to XPScript source diagnostics and preserves upstream identifiers;
* `CompilerDriver` configuration, runtime identifier and dependency validation;
* generated C# validation, whose Roslyn diagnostics are source-mapped back to XPScript where source-map information is available;
* target-specific packaging such as WebIIS.

Stable XPS codes are registered in `CompilerDiagnosticCodes`. `CompilerDiagnosticClassifier` maps selected source-mapped upstream diagnostics to stable XPS codes.

## Where structure is still lost

Not every diagnostic producer is structured yet. Important remaining boundaries are:

* parser/transpiler failures that still surface primarily as formatted exception text;
* Roslyn diagnostics where type/member details exist only in upstream diagnostic text and are deliberately not reconstructed by parsing English messages;
* target/platform and ServerSide restrictions that do not yet expose a common structured property contract;
* diagnostics without exact end spans, where schema-v1 normalization currently uses the start location as the end location;
* symbol lookup paths that do not yet expose compiler symbol-table candidates or containing-scope metadata.

`CompilerDiagnosticParser.Humanize` may transform selected Roslyn messages for human readability. It is not a source of structured semantic metadata.

## Reusable metadata

Current reusable machine metadata includes source maps, XPScript source line/column, stable diagnostic code, upstream code, severity/category, source type-validator symbol/type/parameter information, overload candidate signatures, parameter passing mode, class/member conflict metadata, active runtime target and entry source.

Compiler-owned validators should attach semantic data through `CompileDiagnostic.Properties` before throwing `CompilerException`. New machine metadata should not be recovered by parsing localized or human-oriented diagnostic messages.

## Output channels

For machine-readable operation, stdout is reserved for the final JSON or XML result. Progress and non-result logging must use stderr. CI verifies this separation.

## Source-location semantics

Machine diagnostics use 1-based XPScript source coordinates. `line` and `column` identify the start location; `endLine` and `endColumn` identify the end location when an exact span is available. Schema version 1 may normalize diagnostics without an exact end span so the end location equals the start location.

Locations refer to the physical XPScript source that owns the diagnostic, not generated C#. For root-source diagnostics, `file` is the root `.xps` filename. For `Include` diagnostics, `file` is the physical included filename and the location is remapped through the compiler source map. Nested includes preserve their include ancestry in `includeTrace` when available.

Generated C# coordinates are an internal implementation detail. They may appear only in explicit debug diagnostics and must not replace the normal XPScript location in the machine contract.

Line-ending style does not change semantic locations: equivalent LF and CRLF source must report the same XPScript line and column. Source mapping is also required to behave equivalently across Windows, Linux and macOS.

For stdin validation, locations are relative to the submitted source text and use the caller-supplied virtual `--filename`. The virtual filename must be a simple `.xps` filename and cannot contain directory traversal.

## Security and redaction

Machine diagnostics are designed to provide repair context without exposing secrets. The compiler may return a redacted XPScript source line in `sourceText` / schema-v1 `code`, but string-literal contents are masked rather than returned verbatim. Secret-bearing values such as API keys, Authorization headers, credentials and sensitive directive values must never be emitted as structured diagnostic properties or source snippets.

Diagnostic descriptions are not a trusted channel for secret transport. Compiler-owned diagnostics should attach only the minimum structured metadata needed to identify the failing symbol, type, argument, target or rule. Internal temporary paths and compiler workspace paths are sanitized before they reach the public result.

Validation is non-executing. Submitted XPScript is parsed, preprocessed, transpiled and type-checked, but the submitted program is not launched. This boundary is regression-tested in CI.

Consumers should treat redacted source text as display context only. Automated repair logic should prefer `diagnosticCode`, source coordinates and structured `properties`.

## CI and external tooling examples

A CI pipeline can fail deterministically on XPScript validation errors while preserving JSON for later inspection:

```bash
set +e
xpscript validate program.xps --result-format json > compiler-result.json
status=$?
set -e

if [ "$status" -ne 0 ]; then
  cat compiler-result.json
  exit "$status"
fi
```

PowerShell:

```powershell
& xpscript validate program.xps --result-format json > compiler-result.json
if ($LASTEXITCODE -ne 0) {
    Get-Content compiler-result.json
    exit $LASTEXITCODE
}
```

External tools should parse the result contract rather than scrape console text. A typical flow is:

1. Read `schema` and `schemaVersion`.
2. Inspect `result` and `errors`.
3. Match stable `diagnosticCode` values.
4. Use `file`, line/column and structured `properties`.
5. Apply a source-level XPScript change.
6. Re-run validation and consume the next machine result.

For latency-sensitive local integrations, prefer the reusable `CompilerDriver` API or the local MCP server instead of starting a fresh CLI process for every edit. One-shot CI remains a good fit for the CLI.

## Compatibility

Schema version 1 is additive. Existing fields remain compatible. A breaking rename, removal or semantic change requires a new `schemaVersion`. Stable diagnostic codes must not be reused for a different meaning.

## stdin validation

Machine clients can validate source without creating a project source file:

```text
xpscript validate --stdin --filename program.xps --result-format json
```

The compiler reads XPScript source from standard input and runs the normal validation pipeline. `--filename` is a virtual simple `.xps` filename used for `source.entryPoint` and source-mapped diagnostics; directory components are rejected so the virtual name cannot be used for path traversal. Line and column positions refer to the submitted stdin source.

Schema-v1 JSON remains on stdout. Human/debug logging remains separate from the machine result. Stdin validation currently accepts at most 1,048,576 characters. The implementation may use a temporary physical source file internally, but that path is not part of the public machine contract and is removed after validation.

## Symbol introspection

The compiler exposes a deterministic catalog of public XPScript language/runtime symbols for IDE, LSP, MCP and AI clients. Exact lookup and search are available without compiling source:

```text
xpscript describe XPJsonSchema.FromJson --result-format json
xpscript symbols --search XPJson --result-format json
```

Symbol definitions contain the public XPScript name, kind, XPScript signature, parameters, return type, target restrictions, stable documentation ID and deprecation state. Lookup is case-insensitive to match XPScript symbol semantics, while output uses canonical casing and deterministic ordering.

The catalog is deliberately an XPScript API contract rather than reflection over implementation assemblies. Internal runtime implementation types such as `XPScriptJsonSchema` must not be exposed. Every symbol documentation ID is cross-checked against the stable compiler documentation catalog.


## Remote operation permissions

Future remote compiler hosts must treat validation, compilation and execution as separate capabilities. Permission to call `validate` must not imply permission to publish artifacts or execute submitted XPScript, and permission to compile must not imply permission to execute the resulting application.

The local validation pipeline is intentionally non-executing: it may preprocess and transpile XPScript and invoke the platform compiler to type-check generated C#, but it does not launch the submitted XPScript program. CI includes a sentinel regression that would create a file if submitted `Shell` code were executed and verifies that validation leaves the sentinel absent.

Remote MCP, IDE, CI or service integrations should therefore expose independent authorization decisions for `validate`, `compile` and `execute`. An implementation may grant only a subset. Execution should remain an explicit higher-privilege operation rather than a side effect of validation or compilation.


## MCP server

XPScript exposes a local Model Context Protocol server over standard input/output:

```text
xpscript mcp
```

The server implements JSON-RPC MCP initialization, tool discovery and tool calls. It advertises compiler-owned tools for validation, symbol search, exact symbol description and diagnostic explanation. `xpscript_validate` accepts source text plus a virtual simple `.xps` filename and optional runtime identifier, and returns the normal versioned `CompileResult` object. The server keeps one `CompilerDriver` alive for the session so repeated validation uses the measured warm compiler path instead of starting a compiler process for every request.

The MCP transport is local and non-executing. It does not expose compile, run or debugger operations. This keeps AI validation permissions separate from artifact publication and execution permissions.

The existing debugger protocol is not changed by MCP. A later optimization may reuse the same warm compiler-service lifetime inside debugger and run/test hosts after cache invalidation and behavioral equivalence are measured.


## Warm compiler daemon

Normal `xpscript run` operations may reuse the local compiler daemon to avoid paying compiler startup cost for every edit/run cycle. The daemon listens only on loopback, stores its connection state under the user's local application-data directory, and authenticates requests with the random token recorded in that state. Use:

```text
xpscript daemon status
xpscript daemon restart
xpscript daemon quit
```

Use `run --no-daemon` when a one-shot local compilation is required without starting or reusing the daemon. Use `run --debug` for troubleshooting: debug runs deliberately bypass the warm daemon and perform a fresh local validation/build so generated-code diagnostics and detailed runtime tracing correspond to the current source.

The daemon protocol itself also carries a `debug` flag on `compileRun`. Normal daemon requests redact unexpected internal exception details. A request explicitly marked `debug=true` may return detailed exception information, including implementation stack information, for local troubleshooting. Clients must therefore treat debug output as developer-only diagnostic data and must not publish it to untrusted logs or users. Authentication failures never disclose debug detail.

The daemon is an optimization, not a separate compiler implementation. Compiler semantics, security mode, source preprocessors, include restrictions and runtime target are supplied per request and use the same compiler pipeline as direct invocation. Compile requests are serialized inside the daemon because the complete build/dependency-staging pipeline is not treated as concurrently writable.

## MCP usage and debug diagnostics

Start the local MCP server with:

```text
xpscript mcp
```

It communicates over stdin/stdout using JSON-RPC/MCP and currently exposes `xpscript_validate`, `xpscript_symbols`, `xpscript_describe` and `xpscript_explain`. It does not expose application execution or deployment.

A validation tool call accepts `source`, optional `filename`, and optional `runtimeIdentifier`. Compiler debug mode is deliberately not exposed through MCP. Example arguments:

```json
{
  "source": "Sub Main()\n    Print MissingValue\nEnd Sub",
  "filename": "agent.xps" ,
  "runtimeIdentifier": "linux-x64"
}
```

MCP intentionally exposes only compiler diagnostics suitable for machine consumers and source-level repair. Internal compiler debug mode, generated `Program.cs` troubleshooting details, raw exceptions and stack traces are not part of the MCP surface. `--debug` remains a local XPScript development and compiler-troubleshooting facility. MCP transport/parser/internal server exceptions remain standardized JSON-RPC errors.

Recommended integration flow:

1. Call `xpscript_validate` with a simple virtual `.xps` filename.
2. Consume `structuredContent` and key repairs on `diagnosticCode`, source range and structured `properties`.
3. Modify XPScript source, never generated C#.
4. Validate again until the structured result succeeds.
5. If validation reports an internal compiler failure that cannot be repaired from the structured diagnostics, stop automated repair and investigate it locally with the XPScript CLI/compiler development tooling.
6. Compile/run/deploy only through a separately authorized workflow.

### Debug and security contract

`--debug` is an explicit troubleshooting mode, not the default machine interface. Normal machine consumers should expect sanitized paths, redacted secret-bearing source values and no raw internal exception text. Debug mode can intentionally reveal generated-code locations or implementation details needed to diagnose compiler defects. Secret redaction remains required: enabling debug must not be used as a mechanism for returning credentials, bearer tokens or other application secrets.
