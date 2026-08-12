# Include source files TODO

(c) xpagedeveloper.com 2026

Goal: allow an XPScript source file to include additional `.xps` source files that are compiled as part of the same program.

## Syntax and resolution

- [ ] define and document `Include` syntax for source files, for example `Include "common.xps"`
- [ ] resolve relative include paths relative to the file that contains the `Include` statement
- [ ] support nested includes
- [ ] normalize paths before duplicate detection, including `.` / `..` segments and platform path separators
- [ ] use canonical/full paths for duplicate detection so the same physical file cannot be included more than once through different relative path spellings
- [ ] define case-sensitivity behavior consistently with the target/source filesystem
- [ ] preserve deterministic include order

## Duplicate and cycle handling

- [ ] keep an include set for every compilation and add each physical source file at most once
- [ ] repeated includes of an already included file must not duplicate declarations or executable source
- [ ] detect direct cycles such as `a.xps -> a.xps`
- [ ] detect indirect cycles such as `a.xps -> b.xps -> c.xps -> a.xps`
- [ ] provide a clear compiler diagnostic containing the include chain when a cycle is detected

## Diagnostics and source mapping

- [ ] missing include files must produce a clear compiler diagnostic with source file and source line
- [ ] syntax/type errors inside included files must report the included file name plus correct physical line/position
- [ ] `Erl` / source-line tracking must remain correct for included files
- [ ] diagnostics should distinguish the root script from included source files

## Security and build behavior

- [ ] prevent unintended include path traversal outside allowed source roots when secure/restricted compilation is enabled
- [ ] ensure includes cannot overwrite or alter unrelated files during compilation
- [ ] include files are source-only inputs and must not be copied beside output unless explicitly required by another feature
- [ ] direct `.xps` execution and normal compiled execution must use identical include resolution semantics
- [ ] temporary/direct-run builds must resolve includes before creating the temporary executable

## Regression coverage

- [ ] root file includes one additional source file
- [ ] nested includes
- [ ] same file referenced twice is included only once
- [ ] same physical file referenced through different relative paths is included only once
- [ ] duplicate function/class declarations are not created merely because the same include appears twice
- [ ] missing include diagnostic
- [ ] direct include cycle diagnostic
- [ ] indirect include cycle diagnostic with include chain
- [ ] included-file compiler error reports correct source file and physical source line
- [ ] paths containing spaces and Unicode
- [ ] Windows, Linux and macOS path-resolution regression tests
