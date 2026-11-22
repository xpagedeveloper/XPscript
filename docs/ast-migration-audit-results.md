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

This verifies opt-in process-local bound-tree and method-emission reuse, source/include invalidation and clean/cached output equivalence for the probe. It now also verifies the opt-in persistent artifact manifest (`XPSCRIPT_AST_PERSISTENT_CACHE=1`): an unchanged output directory is reused, while an included-file edit invalidates it. The persistent layer reuses a completed target artifact; selective per-file C# project emission and a persistent cross-file symbol workspace remain future architectural work.

## CLI compilation benchmark

The existing 500-line CLI benchmark completed successfully on the same Windows host:

```text
CLI_BENCHMARK_AST_SECONDS=14.181
CLI_BENCHMARK_AST_SECOND_SECONDS=3.329
CLI_BENCHMARK_AST_BYTES=160256
CLI_BENCHMARK_AST_TOTAL_BYTES=271797
```

These are local observations only, not a cross-runner performance claim. Phase-level legacy-versus-AST timings still require instrumentation around preprocessing, lexing/parsing, binding, emission and Roslyn/build.

## AST phase timing instrumentation

Set `XPSCRIPT_AST_PHASE_TIMINGS=1` to emit one JSON record per successful AST compilation on stderr:

```text
AST_PHASE_TIMINGS={"preprocessing":27.8,"lexing_parsing":315.8,"binding":77.3,"emission":39.8,"roslyn_build":1387.5,"source":"shared-runtime.xps","total_ms":1849.6}
```

The legacy compiler entry point is intentionally disabled in this branch, so a legacy-versus-AST phase comparison requires running the published legacy-compatible compiler separately. The AST phase instrumentation is now available for that controlled comparison without changing normal output.
