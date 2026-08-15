# XPScript source preprocessors

(c) xpagedeveloper.com 2026

XPScript can run an ordered chain of source preprocessors before normal parsing and transpilation.

## Pipeline order

Compilation uses this order:

1. Read the root `.xps` file.
2. Resolve and expand the complete recursive `Include` graph.
3. Deduplicate included files and build the source map back to each physical `.xps` file and line.
4. Run configured source preprocessors in the exact order supplied.
5. Continue with managed/native reference processing, normal parsing, transpilation and compilation.

A preprocessor therefore sees the complete expanded compilation source, including nested included files. It never runs independently once per Include file.

The same pipeline is used by normal compilation, cross-platform publishing and `xpscriptc run` because preprocessing is attached to the common Include expansion stage.

## CLI

Use `--preprocessor` once or repeatedly:

```text
xpscriptc app.xps --preprocessor "replace:__MODE__=Production"

xpscriptc app.xps \
  --preprocessor "replace:__FIRST__=__SECOND__" \
  --preprocessor "replace:__SECOND__=Ready"
```

For direct execution:

```text
xpscriptc run app.xps --preprocessor "replace:__MODE__=Development"
```

Order is significant. The output from preprocessor N becomes the input to preprocessor N+1. Repeating the same specification is allowed and means that the preprocessor runs repeatedly at each declared position.

Zero preprocessors preserves existing compiler behavior.

## Built-in preprocessors

### `identity`

Returns the complete source and source map unchanged. It is useful when verifying pipeline configuration.

### `replace:FROM=TO`

Performs a case-sensitive literal replacement across the complete expanded source.

Example:

```text
--preprocessor "replace:__API_VERSION__=v2"
```

`FROM` must not be empty. `FROM` and `TO` cannot contain line breaks. This keeps the built-in replacement line-count preserving and therefore retains exact source-map behavior.

## Source-map-aware contract

The compiler exposes these source preprocessor contract types:

```csharp
public interface ISourcePreprocessor
{
    string Name { get; }
    SourcePreprocessorResult Transform(SourcePreprocessorContext context);
}
```

`SourcePreprocessorContext` contains:

- the current complete source text,
- the root source path,
- one source-map location for every current source line.

`SourcePreprocessorResult` must return:

- the transformed source text,
- one source-map location for every output source line.

This allows a future line-changing preprocessor to explicitly map every generated line back to the most useful original file and physical line instead of forcing the compiler to guess.

A result whose source-map entry count does not match its output line count is rejected.

A preprocessor can throw `SourcePreprocessorException` and provide an expanded line and position. The pipeline maps that location back through the current source map and emits a compiler diagnostic identifying the preprocessor and original source location.

## Safety and isolation

The initial execution model allows only compiler-owned built-in preprocessors selected by known specifications. Arbitrary executable paths, shell commands, managed assemblies and dynamically loaded plugins are not accepted by `--preprocessor`.

Preprocessor selection is scoped with `AsyncLocal` state and restored after each compile operation. Concurrent compiler operations in one process therefore do not intentionally share mutable preprocessor configuration.

External process or managed plugin preprocessors require a separate security design before implementation. That design must define at minimum:

- explicit trust/allow-list configuration,
- path canonicalization and symlink/reparse-point handling,
- execution timeout and cancellation,
- process privileges and filesystem/network isolation,
- maximum input/output sizes,
- deterministic failure handling,
- diagnostic redaction,
- restrictions preventing modification of compiler output paths or unrelated files.

## Diagnostics

Errors produced after preprocessing continue through the Include source map. If a transformed line originated in an included file, compiler diagnostics use that included file's physical line mapping.

The built-in `replace` preprocessor preserves line count, so source mappings remain one-to-one with the original expanded source.

## Regression gate

`.github/workflows/source-preprocessor-pipeline.yml` verifies on Windows, Ubuntu and macOS that:

- included source is visible to preprocessors,
- duplicate Includes are deduplicated before preprocessing,
- multiple preprocessors run in declared order,
- reversing the order produces a different expected result,
- direct `run` uses the same chain as normal compile,
- an error introduced after preprocessing maps back to the original physical line in an included source file.
