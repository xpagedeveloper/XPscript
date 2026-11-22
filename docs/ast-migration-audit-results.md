# AST migration audit results

Recorded on 2026-11-22 on branch `investigate-ast-compiler`.

## Syntax coverage

The complete Windows AST CLI corpus was rerun with `tests/ast-compile-probe/run-ast-cli.ps1`. The corpus exercises language syntax, diagnostics, classes, constructors/destructors, arrays, control flow, files, databases, HTTP, JSON/XML/CSV, spreadsheets, XPAi, Notes metadata, UIForm/UIListView, platform targets and source-span mapping. The run emits named checkpoints and must finish with `AST CLI compilation probe passed` before it is treated as a green full-corpus result.

The coverage map is maintained in [ast-test-coverage-map.md](ast-test-coverage-map.md). Unsupported syntax must remain an explicit diagnostic; it must not be silently routed through the legacy transpiler.

## Legacy compatibility audit

The legacy compiler entry points are disabled and return the documented compatibility diagnostic. The remaining legacy source is retained only for explicitly justified compatibility surfaces:

- rejected `legacy-compile`/`compilev1` and `legacy-run`/`runv1` aliases;
- legacy output generation used by historical compatibility probes;
- isolated runtime/postprocessor shims where the public runtime contract still requires them.

The remaining shims require a separate removal decision because deleting them would either remove a documented compatibility command or change the generated runtime contract. They are not selected by normal `compile`/`run` AST commands.

## Incremental and cross-file cache evidence

`tests/AstIncrementalCacheProbe` passed with:

```text
AST_BOUND_CACHE_HIT_OK hits=3 misses=8
AST_EDIT_COMPILE_BENCHMARK clean_ms=8342.4 cached_ms=1027.7 include_edit_ms=1051.7 source_edit_ms=1138.3 emission_hits=3
```

This verifies opt-in process-local bound-tree and method-emission reuse, source/include invalidation and clean/cached output equivalence for the probe. It does not yet implement a persistent dependency-aware workspace or selective multi-file C# project emission. Those remain open architectural work and must not be enabled by default until a dependency graph and cross-file symbol snapshot are implemented.

## CLI compilation benchmark

The existing 500-line CLI benchmark completed successfully on the same Windows host:

```text
CLI_BENCHMARK_AST_SECONDS=14.181
CLI_BENCHMARK_AST_SECOND_SECONDS=3.329
CLI_BENCHMARK_AST_BYTES=160256
CLI_BENCHMARK_AST_TOTAL_BYTES=271797
```

These are local observations only, not a cross-runner performance claim. Phase-level legacy-versus-AST timings still require instrumentation around preprocessing, lexing/parsing, binding, emission and Roslyn/build.
