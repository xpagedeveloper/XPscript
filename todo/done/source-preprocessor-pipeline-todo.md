# XPScript source preprocessor pipeline TODO

(c) xpagedeveloper.com 2026

Status: completed and verified.

Goal: allow one or more configurable source preprocessors to run in a deterministic order before normal parsing/transpilation/compilation, but only after the complete Include graph has been resolved so every included source file is present in the preprocessing input.

Final implementation model:

- Include expansion and canonical-path deduplication happen before configured source preprocessing.
- Preprocessors receive the complete expanded source plus source-map context.
- `--preprocessor` is repeatable and preserves declaration order exactly.
- Current execution is intentionally built-in only. Arbitrary executables, shell commands, managed assemblies and dynamically loaded plugins are rejected.
- External/custom preprocessors remain unsupported until a separate trust, timeout, isolation and path-security design is implemented.
- Structured diagnostics retain originating `.xps` filename, physical line, position and redacted/original mapped source text where available.
- The permanent `Source Preprocessor Pipeline` workflow passes on Windows, Ubuntu and macOS.

## Required pipeline order

1. Load the root `.xps` source file.
2. Resolve and expand all `Include` directives recursively.
3. Deduplicate included files by canonical physical path and reject/report include cycles according to `todo/done/include-source-files-todo.md`.
4. Build one complete logical source graph / combined compilation source while retaining source-map metadata back to original files and physical line numbers.
5. Run the configured source preprocessors in the exact configured order.
6. Pass the final preprocessed source to the normal XPScript parser/transpiler/compiler pipeline.

## Preprocessor configuration and ordering

- [x] support zero, one or multiple source preprocessors per compilation
- [x] allow preprocessors to be explicitly ordered; order is deterministic and preserved exactly
- [x] define a stable preprocessor interface/contract for receiving source text plus compilation/source-map context
- [x] allow a preprocessor to return transformed source for the next preprocessor in the chain
- [x] make the output of preprocessor N the input of preprocessor N+1
- [x] do not run user-configured source preprocessors independently per included file before Include expansion
- [x] ensure all included source is available to every configured preprocessor
- [x] define CLI configuration syntax for selecting and ordering preprocessors through repeatable `--preprocessor`
- [x] define repeated-run semantics: duplicate specifications are allowed and execute repeatedly in declared order

## Source mapping and diagnostics

- [x] preserve original filename/line mappings through Include expansion and supported preprocessor transformations
- [x] compiler diagnostics after preprocessing identify the originating `.xps` file and useful source line/position
- [x] `Erl` / physical source-line tracking remains meaningful after Include expansion and preprocessing
- [x] preprocessor failures include preprocessor name, mapped source file, original line/position where available, and a clear description
- [x] malformed or invalid transformed source uses normal source-mapped compiler diagnostics

## Execution modes

- [x] use the exact same Include -> preprocessor chain -> compile pipeline for normal compilation
- [x] use the same pipeline for direct `.xps` execution / temporary-exe execution described in `todo/done/direct-script-execution-todo.md`
- [x] publish/cross-platform compilation uses the same preprocessing semantics

## Safety and isolation

- [x] execution model is explicitly defined as compiler-owned built-in preprocessors only for v1
- [x] external/custom preprocessors are intentionally unsupported until trust, path validation, timeout, failure and isolation semantics are separately implemented
- [x] the preprocessor contract cannot change compiler output paths or unrelated files; configured built-ins transform source/source-map data only
- [x] concurrent compiler runs use scoped preprocessor configuration and isolated compiler temporary state

## Regression coverage

- [x] one preprocessor transforms code after an included file has been expanded
- [x] multiple preprocessors run in a declared order and later preprocessors see earlier output
- [x] reversing preprocessor order produces the expected different result
- [x] included source is processed exactly once as part of the complete expanded source
- [x] duplicate includes do not cause duplicated preprocessing input
- [x] nested includes are available to preprocessors
- [x] preprocessing errors retain useful original source locations including source filename
- [x] normal compile and direct-script execution produce identical preprocessing behavior
- [x] Windows, Linux and macOS coverage passes for the built-in cross-platform execution model
