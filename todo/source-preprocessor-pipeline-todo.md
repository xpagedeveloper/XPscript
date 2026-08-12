# XPScript source preprocessor pipeline TODO

(c) xpagedeveloper.com 2026

Goal: allow one or more configurable source preprocessors to run in a deterministic order before normal parsing/transpilation/compilation, but only after the complete Include graph has been resolved so every included source file is present in the preprocessing input.

## Required pipeline order

1. Load the root `.xps` source file.
2. Resolve and expand all `Include` directives recursively.
3. Deduplicate included files by canonical physical path and reject/report include cycles according to `todo/include-source-files-todo.md`.
4. Build one complete logical source graph / combined compilation source while retaining source-map metadata back to original files and physical line numbers.
5. Run the configured source preprocessors in the exact configured order.
6. Pass the final preprocessed source to the normal XPScript parser/transpiler/compiler pipeline.

## Preprocessor configuration and ordering

- [ ] support zero, one or multiple source preprocessors per compilation
- [ ] allow preprocessors to be explicitly ordered; order must be deterministic and preserved exactly
- [ ] define a stable preprocessor interface/contract for receiving source text plus compilation/source-map context
- [ ] allow a preprocessor to return transformed source for the next preprocessor in the chain
- [ ] make the output of preprocessor N the input of preprocessor N+1
- [ ] do not run user-configured source preprocessors independently per included file before Include expansion
- [ ] ensure all included source is available to every configured preprocessor
- [ ] define configuration syntax/CLI/project configuration for selecting and ordering preprocessors
- [ ] reject duplicate/conflicting preprocessor registrations where appropriate, or define explicit repeated-run semantics

## Source mapping and diagnostics

- [ ] preserve original filename/line mappings through Include expansion and preprocessor transformations where possible
- [ ] compiler diagnostics after preprocessing must still identify the originating `.xps` file and useful source line/position
- [ ] `Erl` / physical source-line tracking must remain meaningful after Include expansion and preprocessing
- [ ] when a preprocessor itself reports an error, include preprocessor name, source file, original line/position where available, and a clear description
- [ ] define diagnostics for malformed or invalid transformed source produced by a preprocessor

## Execution modes

- [ ] use the exact same Include -> preprocessor chain -> compile pipeline for normal compilation
- [ ] use the same pipeline for direct `.xps` execution / temporary-exe execution described in `todo/direct-script-execution-todo.md`
- [ ] ensure publish/cross-platform compilation uses the same preprocessing semantics

## Safety and isolation

- [ ] define whether preprocessors are built-in, managed plugins, external processes, or support more than one execution model
- [ ] if external/custom preprocessors are supported, define trust, path validation, timeout, failure and isolation semantics
- [ ] prevent a preprocessor from silently changing compiler output paths or unrelated files unless explicitly allowed by its contract
- [ ] ensure concurrent compiler runs use isolated preprocessor state and temporary files

## Regression coverage

- [ ] one preprocessor transforms code after an included file has been expanded
- [ ] multiple preprocessors run in a declared order and later preprocessors see earlier output
- [ ] reversing preprocessor order produces the expected different result
- [ ] included source is processed exactly once as part of the complete expanded source
- [ ] duplicate includes do not cause duplicated preprocessing input
- [ ] nested includes are available to preprocessors
- [ ] preprocessing errors retain useful original source locations
- [ ] normal compile and direct-script execution produce identical preprocessing behavior
- [ ] Windows, Linux and macOS coverage where the selected preprocessor execution model is cross-platform
