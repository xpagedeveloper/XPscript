# AST runtime compatibility

The AST compiler emits the same runtime contracts used by the existing
XPscript runtime wherever a supported object already has a runtime
implementation. Runtime code is selected from the source that is being
compiled; an unrelated optional object does not add its external runtime or
NuGet dependency to the generated application.

## Runtime selection

`AstCompiler` composes the generated program from the following
feature-specific runtime sources when the corresponding syntax or object is
used:

| XPscript area | AST selection / implementation |
| --- | --- |
| HTTP, HTTP JSON and database HTTP helpers | `AstHttpSupport` and the shared HTTP runtime sources |
| TLS validation | `AstTlsRuntimeCode`, included only when HTTP or AI code needs it |
| JSON/XML/CSV/spreadsheets | The corresponding `Ast*Support` selector and existing runtime source |
| Images | `AstImageSupport.RuntimeCode` and the existing image runtime contract |
| Files, archives and environment helpers | `AstFileRuntimeCode`, `FileSystemPortabilityRuntimeSource` and existing file runtime APIs |
| Errors and callbacks | `AstErrorRuntimeCode` and `AstCallbackRuntimeCode` |
| SQLite, MySQL, MS SQL and Supabase | Their feature selectors and database runtime sources |
| Notes HTTP compatibility | `AstNotesJsonSupport` and the shared HTTP compatibility runtime. Notes-specific JSON navigator/object/array/element types are not an AST surface; use the canonical `XPJsonDocument`, `XPJsonObject`, `XPJsonArray` and `XPJsonElement` APIs. |

The generated source must not use a fabricated constant or empty property as
a substitute for a runtime value. The guard
`scripts/validate-runtime-placeholders.py` scans compiler runtime sources and
fails when it finds an unapproved constant getter or empty setter. Run it from
the repository root with:

```powershell
python scripts/validate-runtime-placeholders.py
```

## Explicit compatibility exceptions

The guard allowlist is intentionally narrow and is not an approval for new
placeholders. The current entries are:

| Runtime member | Why it remains allowlisted |
| --- | --- |
| `NotesAgentPostProcessor.IsNotesAgent` | A `NotesAgent` wrapper is always an agent, so this is a compatibility discriminator rather than missing state. |
| `NotesRuntimeValueSource.IsValidDate` | A constructed `NotesDateTime` is valid by construction; invalid native values fail during construction/conversion. |

Rich-text postprocessor files are excluded from the source scan because they
contain intermediate declarations. The final rich-text surface is checked by
the rich-text postprocessor/compiler gate instead.

## Remaining migration item

The AST migration is not complete until the remaining experimental runtime
helpers (including any HTTP/TLS, image, UI, error or environment helper that
still lacks real backing) have been compared with and connected to the
existing runtime implementation. A new allowlist entry requires a concrete
compatibility reason and must not be used to hide an unimplemented API. This
work remains tracked in `todo/ast-compiler-migration-todo.md`.
