# Include source files TODO

(c) xpagedeveloper.com 2026

Goal: allow an XPScript source file to include additional `.xps` source files that are compiled as part of the same program.

Status markers: `[x]` implemented and regression-verified, `[>]` partially implemented/documented, `[ ]` outstanding.

## Syntax and resolution

- [x] define and document `Include` syntax for source files, for example `Include "common.xps"`
- [x] resolve relative include paths relative to the file that contains the `Include` statement
- [x] support nested includes
- [x] normalize paths before duplicate detection, including `.` / `..` segments and platform path separators
- [x] use canonical/full paths for duplicate detection so the same physical file cannot be included more than once through different relative path spellings
- [>] define case-sensitivity behavior consistently with the target/source filesystem. Current behavior is documented as case-insensitive on Windows and case-sensitive on Linux/macOS; filesystem-capability probing is not implemented.
- [x] preserve deterministic include order

## Duplicate and cycle handling

- [x] keep an include set for every compilation and add each normalized physical source path at most once
- [x] repeated includes of an already included file must not duplicate declarations or executable source
- [x] detect direct cycles such as `a.xps -> a.xps`
- [x] detect indirect cycles such as `a.xps -> b.xps -> c.xps -> a.xps`
- [x] provide a clear compiler diagnostic containing the include chain when a cycle is detected

## Diagnostics and source mapping

- [x] missing include files produce a clear compiler diagnostic with containing source file and source line
- [x] syntax/type errors inside included files report the included file name plus correct physical line/position counted from line 1 of that include file
- [x] `code` / `markedCode` for an included-file diagnostic use the included physical source line and caret position rather than the same-numbered line from the root source
- [ ] `Erl` / source-line tracking must remain correct for included files
- [x] downstream compiler diagnostics that carry an XPScript source location are remapped through the multi-file include source map before being returned

## Security and build behavior

- [ ] prevent unintended include path traversal outside allowed source roots when secure/restricted compilation is enabled
- [x] include processing is read-only and cannot overwrite or alter unrelated files during compilation
- [x] include files are source-only inputs and are not copied beside output
- [x] direct `.xps` execution and normal compiled execution use identical include resolution semantics
- [x] temporary/direct-run builds resolve includes as part of compilation before the temporary executable is produced
- [>] project-level dependency directives such as managed `Reference` should remain in the root source until dependency discovery is moved after Include expansion

## Regression coverage

- [x] root file includes one additional source file
- [x] nested includes
- [x] same file referenced twice is included only once
- [x] same physical file referenced through different relative paths is included only once
- [x] duplicate function/class declarations are not created merely because the same include appears twice
- [x] missing include diagnostic
- [x] direct include cycle diagnostic
- [x] indirect include cycle diagnostic with include chain
- [x] included-file compiler error reports correct source file and physical source line in text, JSON, XML and direct `run`
- [x] included-file compiler error returns `code` and `markedCode` from the included physical source file
- [x] paths containing spaces and Unicode
- [x] Windows, Linux and macOS path-resolution regression tests
- [x] Windows, Linux and macOS include diagnostic source-map regression tests

## Verification

The `Include Source Files` GitHub Actions workflow compiles and runs the include fixture on Windows, Linux and macOS and verifies normal compilation, `xpscriptc run`, nested/duplicate/Unicode paths, missing includes and direct/indirect cycle diagnostics.

The `Include Source Mapping` workflow additionally verifies that an error physically located on line 5 of an included file is reported as line 5 of that file — not as the flattened compilation-unit line — and that `code` / `markedCode` come from the physical include file in text, JSON, XML and direct-run compiler output on Windows, Linux and macOS.
