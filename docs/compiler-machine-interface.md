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
