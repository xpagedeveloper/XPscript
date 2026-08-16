# Include source mapping status

(c) xpagedeveloper.com 2026

Implemented and verified:

- Include expansion produces a line-by-line source map containing physical source path, local line number and source text.
- Compiler diagnostics with XPScript source locations are remapped from flattened compilation-unit lines to physical include-file locations.
- Line numbering restarts at 1 for every included source file.
- The included file name is preserved in returned compiler diagnostics.
- Normal compilation and `xpscriptc run` use the same source-map behavior.
- `code` and `markedCode` use the physical included source line rather than the same-numbered root source line.
- Runtime `Erl` / source-line tracking retains physical include-file line identity through later source transformations.
- Text, JSON and XML result formats are regression-tested.
- Windows, Linux and macOS are covered by the `Include Source Mapping` workflow.

Verification:

- `samples/include-source-map/lib/compile-error.xps` verifies include-local compiler diagnostics, source code and marker position.
- `samples/include-erl/` verifies include-local runtime `Erl` identity.
- `.github/workflows/include-source-mapping.yml` runs the compiler and runtime checks on Windows, Ubuntu and macOS.
