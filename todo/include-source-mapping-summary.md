# Include source mapping status

(c) xpagedeveloper.com 2026

Implemented and verified:

- Include expansion produces a line-by-line source map containing physical source path, local line number and source text.
- Compiler diagnostics with XPScript source locations are remapped from flattened compilation-unit lines to physical include-file locations.
- Line numbering restarts at 1 for every included source file.
- The included file name is preserved in returned compiler diagnostics.
- Normal compilation and `xpscriptc run` use the same source-map behavior.
- Text, JSON and XML result formats are regression-tested.
- Windows, Linux and macOS are covered by the `Include Source Mapping` workflow.

Still outstanding:

- `code` / `markedCode` should use the physical included source line instead of the same-numbered root source line.
- Runtime `Erl` / source-line tracking should retain physical include-file line identity through later source transformations.
