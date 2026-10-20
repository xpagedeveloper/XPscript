# AST migration checkpoint

Updated 2026-10-20 on branch `investigate-ast-compiler`.

## Current state

- Latest pushed commit: `af85d0ec` (`Normalize numeric arrays for XPImage byte input`).
- Working tree was clean when this checkpoint was written.
- The focused XPImage AST regression passed, including byte-array interop and image getters/setters/indexed properties.
- The full AST CLI suite was run from `tests/ast-compile-probe/run-ast-cli.ps1` in PowerShell session `48478` and completed successfully.
- The latest full-suite markers reached before this checkpoint include `AST_TIME_PARTS_OK`, followed by shell-id, MsgBox, file runtime/get/text/input/lock, and `AST_ISDIR_DRIVE_OK` markers.
- The full AST CLI suite subsequently completed successfully with exit code 0 and `AST CLI compilation probe passed.` in session `48478`.

## Recent verified fixes

- Path objects through `Variant`.
- Scalar AST class properties.
- Optional array class-method defaults.
- Explicit lower/upper array bounds.
- Numeric CLR arrays passed to `XPImage` byte input.

## Required continuation

1. Poll session `48478` until the full suite finishes.
2. If it fails, isolate the next failing probe, add or update focused regression coverage, run the focused test and relevant build, then commit and push the fix.
3. After each accepted green item, create a descriptive commit and push it to GitHub.
4. Run `C:\Users\FredrikNorling\.local\bin\graphify.exe update .` after source changes. The last attempts were blocked by `uv trampoline failed to canonicalize script path`; do not claim graphify succeeded unless it actually does.

## Verified suite areas in the completed run

The completed run covered the AST CLI language/runtime probes, including constructors, destructors, properties, indexed properties, arrays/redim, lists, ByRef/reference storage, XML/CSV/JSON, HTTP/XPAI, XPImage, Notes HTTP, control flow, static initialization, and object/class lifecycle.

The legacy command-line compatibility baseline also passed with `tests/cli-compatibility/run-cli-compatibility.ps1` (`CLI_COMMAND_LINE_COMPATIBILITY_OK`). This verifies the currently implemented AST option surface; complete legacy `--target` behavior parity remains open in `todo/ast-compiler-migration-todo.md`.

