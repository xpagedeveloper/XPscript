# AST compiler intentional behavior

This document records compatibility boundaries that are deliberate while the
AST compiler is being migrated. They are diagnostics or routing decisions, not
silent changes to XPscript semantics.

## Explicit legacy fallback

- Sources containing `XPImage` are routed through the legacy compiler until
  the existing image runtime is represented by AST-bound nodes.
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

Multidimensional arrays, complete error-handler transfer semantics, class
lowering and the remaining XPImage operations stay deferred until their shared
bound/runtime representations are available. These features must not be
silently approximated with dynamic values or generated C# behavior.
