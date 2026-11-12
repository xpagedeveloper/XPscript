# AST GoSub lowering notes

Status: design captured; implementation has not started.

## Current behavior

The lexer and syntax parser already recognize `GoSub label` and `Return`. The
AST binder deliberately rejects GoSub with the diagnostic that GoSub is not
implemented. Ordinary `GoTo` labels and procedure `Return` are already emitted
by the AST path.

The legacy compatibility transpiler uses a separate return stack per procedure.
It pushes a generated return id before jumping to the label and pops that id at
`Return`, dispatching to the generated return label. This supports multiple
sequential GoSubs and preserves access to the caller's local variables.

Existing fixtures include:

- `tests/ast-compile-probe/gosub-parity-audit.xps`
- `tests/ast-compile-probe/multiple-gosub.xps`
- `tests/ast-compile-probe/unsupported-gosub.xps`

## Chosen AST direction

Lower GoSub labels to generated local helper functions that capture the
surrounding procedure locals. This keeps the caller's local state shared while
avoiding a fixed global stack id. A GoSub call invokes the helper and continues
after it; `Return` inside the extracted label block returns from that helper.

The lowering must happen after syntax parsing and before final C# emission. It
must extract only the label block, remove that block from the main statement
flow, and leave ordinary labels, `GoTo`, `Exit Sub` and procedure `Return`
unchanged.

## Implementation order

1. Add a lowering representation for one label block and one GoSub call.
2. Generate a local helper for the simple `gosub-parity-audit.xps` case.
3. Bind `Return` in the extracted block to helper return and keep normal
   procedure returns unchanged.
4. Add the sequential `multiple-gosub.xps` regression.
5. Handle nested GoSub and loops in a separate change.
6. Handle `On Error`/`Resume` interaction in a separate change.

Do not replace GoSub with a normal top-level XPscript Function: that would
change implicit local-variable access and would require changing existing
source code.

