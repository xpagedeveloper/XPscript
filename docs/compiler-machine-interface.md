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
