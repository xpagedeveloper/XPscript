# AST compiler intentional behavior

This document records compatibility boundaries that are deliberate while the
AST compiler is being migrated. They are diagnostics or routing decisions, not
silent changes to XPscript semantics.

## Explicit legacy fallback

- XPImage uses the shared native runtime through AST-bound nodes and no longer
  causes legacy fallback. Other registry entries not yet migrated still route
  the complete source file through the legacy compiler.
- The legacy `NotesSAXParser`/`On Event` compatibility surface remains an
  explicit legacy fallback. AST code must use the existing secure `XPXml`
  document/node API for XML processing; the AST compiler must not add a second
  SAX parser or partially lower event bindings.
- The fallback is owned by `AstObjectSupportRegistry` and is exercised by the
  AST CLI and MCP probes.

## Explicit AST diagnostics

The AST path reports a mapped `XPS1012` diagnostic instead of emitting a
partial or misleading C# program for features whose lowering is not complete:

- `On Error` and `Resume` statements;
- `With` and implicit member access;
- `GoSub`;
- entry jumps into `For`/`ForAll` blocks;
- List-element expressions passed directly to `ByRef` parameters;
- unsupported dynamic non-zero `ReDim` bounds;
- multidimensional array shapes.

The diagnostic keeps the XPscript filename and source span. MCP exposes the
same structured diagnostic as the CLI.

## Deliberately deferred semantics

Multidimensional arrays, complete error-handler transfer semantics and class
lowering stay deferred until their shared
bound/runtime representations are available. These features must not be
silently approximated with dynamic values or generated C# behavior.

AST image ownership also covers exception exits and evaluates replacements before
recycling old images. The legacy image preprocessor's end-of-procedure cleanup
does not guarantee cleanup on thrown errors. `xpimage-runtime.xps` tests AST cleanup
independently; `xpimage-parity.xps` compares the shared image API and normal exits.
