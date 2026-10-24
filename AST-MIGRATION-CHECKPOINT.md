# AST migration checkpoint

- Platform CI verification was extended to `investigate-ast-compiler`: `Platform FullTest` now runs on AST pushes, and the Kestrel hardening workflow runs its security matrix on the same branch with explicit report/exit-code verification. Elevated local REST, Kestrel, CGI and FastCGI smoke tests passed with `PLATFORM_HOSTING_SMOKE_OK` on 2026-10-23. The non-elevated REST attempt was blocked only by the Windows sandbox denying the compiler temporary `references` directory.

- Fixed compiler validation/run temporary-root handling to honor `XPSCRIPT_TEMP_ROOT` consistently. The CLI compatibility suite now completes in the restricted Windows environment with `CLI_COMMAND_LINE_COMPATIBILITY_OK`; Release build remains at 0 warnings and 0 errors.

Updated 2026-10-23 on branch `investigate-ast-compiler`.

## Current state

- Latest pushed commit: `e6a834bc` (`Honor configured compiler temp root everywhere`). The preceding `811fbb86` commit aligned legacy `--target desktop|console` behavior and its CLI regression coverage.
- The CLI compatibility suite was re-run successfully with `XPSCRIPT_TEMP_ROOT` directed to a writable repository output directory: `CLI_COMMAND_LINE_COMPATIBILITY_OK`. It covers AST/default/legacy compile and run aliases, target/platform/RID handling, runtime and single-file switches, result formats, security/restricted/source-root/preprocessor options, validation and executable output.
- The target-matrix probe was rebuilt with MSBuild node parallelism disabled (`-m:1`) and executed successfully with `TARGET_MATRIX_OK`; the workload resolver was the source of the earlier silent parallel-build failure.
- AST-generated compile/run projects now set `RestoreIgnoreFailedSources=true`, allowing cached packages to build when the NuGet service index is unavailable. The focused SQLite native-surface regression passed with `SQLITE_AST_OK` after the change.
- The working tree is clean apart from the unrelated private `scripts/restart-chatgpt-after-5-minutes.ps1` helper and generated probe output directories.

- The focused XPImage AST regression passed, including byte-array interop and image getters/setters/indexed properties.
- The full AST CLI suite was run from `tests/ast-compile-probe/run-ast-cli.ps1` in PowerShell session `59322` and completed successfully.
- The latest full-suite markers reached before this checkpoint include `AST_TIME_PARTS_OK`, followed by shell-id, MsgBox, file runtime/get/text/input/lock, and `AST_ISDIR_DRIVE_OK` markers.
- The full AST CLI suite subsequently completed successfully with exit code 0 and `AST CLI compilation probe passed.` in session `59322`.

- The multidimensional whole-array parameter binding gap was fixed: `ParameterSymbol` array parameters now receive the same rank-aware indexed binding as local array symbols. Positive procedure and constructor regressions print `7`, the focused array suite reaches `AST_ARRAY_SIGNATURES_OK`, and the full AST CLI suite remains green.
- Graphify update completed successfully after source changes using `C:\Python315\Scripts\graphify.exe update .`; the `.local\\bin` trampoline is still not the working executable. Its 149 no-symbol warnings are informational and do not indicate a graph update failure.

## Recent verified fixes

- Path objects through `Variant`.
- Scalar AST class properties.
- Optional array class-method defaults.
- Explicit lower/upper array bounds.
- Numeric CLR arrays passed to `XPImage` byte input.

## Required continuation

1. Continue the complete legacy command-line parameter audit; desktop/console target aliases and the configured temp-root behavior are now covered, while broader diagnostic and execution parity remains open.
2. If the next point fails, isolate the failing probe, add or update focused regression coverage, run the focused test and relevant build, then commit and push the fix.
3. After each accepted green item, create a descriptive commit and push it to GitHub.
4. Run Graphify after source changes. Use `C:\Python315\Scripts\graphify.exe update .` while the `.local\\bin` trampoline still fails to canonicalize its script path.

## Verified suite areas in the completed run

The completed run covered the AST CLI language/runtime probes, including constructors, destructors, properties, indexed properties, arrays/redim, lists, ByRef/reference storage, XML/CSV/JSON, HTTP/XPAI, XPImage, Notes HTTP, control flow, static initialization, and object/class lifecycle.

The legacy command-line compatibility baseline passes with `tests/cli-compatibility/run-cli-compatibility.ps1` (`CLI_COMMAND_LINE_COMPATIBILITY_OK`). It now also verifies successful legacy `--target desktop` and `--target console` output; broader legacy option diagnostics and execution parity remain open in `todo/ast-compiler-migration-todo.md`.

The AST target-matrix probe also passed with `TARGET_MATRIX_OK` after an external NuGet restore. The probe verifies the configured desktop/browser target metadata and target routing; physical Android device/emulator validation remains a local setup gate.

The local Android probes also passed: `ANDROID_AST_BACKEND_OK` and `ANDROID-AST-COMPILER-PROBE=OK`. The probe validates Android AST backend/project generation for the configured Android RIDs; physical device/emulator installation remains outside this run.

## Current continuation state (2026-10-20)

- The REST smoke test exposed two AST/web issues: explicit routes with a wrong HTTP method now return 405, and JSON binding exceptions are mapped to 400.
- The web compiler's JSON-schema helper declaration was renamed from the reserved `__xps_rest_json_schema_runtime` identifier to `xpsCompilerGeneratedJsonSchemaRuntime`.
- These three source changes are currently uncommitted while verification is blocked: `src/XPScript.Web.Compiler/XpsRestRouteIndex.cs`, `src/XPScript.Web.Compiler/XpsWebDispatcher.cs`, and `src/XPScript.Web.Compiler/XpsWebCompiler.cs`.
- The resolver/build path is now working when run with elevated access. The focused REST smoke currently builds cleanly and passes route binding, malformed JSON classification, CORS, JSON-schema validation, schema error classification, and schema cache-content-hash checks.
- The REST blocker is resolved: routes combining `[JsonSchema]` with model rules such as `[Email]` now run both validation layers and merge their problem-detail errors without losing `validationErrors` schema detail.
- The latest attempted schema-detail fix is retained in the working tree: `minimum` validation now reports the expected value as `>= 18`, and the web adapter can read validation details through `Stringify()` fallback when reflective `Get()` extraction is unavailable.
- Graphify was invoked for the current codebase query and still fails before execution with `uv trampoline failed to canonicalize script path`; the required post-change `graphify update .` remains pending for the same environment issue.
- Focused verification is green: `dotnet build tests/WebRestApiSmoke/WebRestApiSmoke.csproj -c Release --no-restore -p:UseSharedCompilation=false` produced 0 warnings and 0 errors, and `dotnet run --project tests/WebRestApiSmoke/WebRestApiSmoke.csproj -c Release --no-build` reached `WEB-REST-API-SMOKE=OK`.
- Commit `ebdf8e48` (`Complete AST web REST validation and binding`) was pushed to `origin/investigate-ast-compiler`.
- Platform follow-up: the Kestrel smoke project restored and built with 0 warnings/errors, but its runtime suite currently fails in `AssertRequestHeadersTimeoutAsync` at `tests/WebKestrelSmoke/Program.cs:950` with `SocketError (10053)`, before CGI/FastCGI were run. This is the next investigation point; no platform commit was made.
- CGI verification is green: `WEB-CGI-SMOKE=OK` after restoring its project assets.
- FastCGI verification is green: `WEB-FASTCGI-SMOKE=OK` after fixing concurrent same-file web-log opening/writes with process-wide synchronization and `FileShare.ReadWrite`.
- The complete `tests/ast-compile-probe/run-ast-cli.ps1` suite completed successfully with `AST CLI compilation probe passed.` It covered the current include/use, file-error, database, system inventory, constructors/destructors, properties, arrays, ByRef, JSON/XML/CSV, HTTP/TLS, XPAI, Notes HTTP, UI/runtime and control-flow probes.
- The legacy CLI compatibility probe `tests/cli-compatibility/run-cli-compatibility.ps1` completed successfully with `CLI_COMMAND_LINE_COMPATIBILITY_OK`.
- Target verification is green: `TARGET_MATRIX_OK`, all native target RID/multiline/fallback markers, `ANDROID_AST_BACKEND_OK`, and `ANDROID-AST-COMPILER-PROBE=OK`. The builds produced no compiler errors; restore emitted only `NU1900` because the NuGet vulnerability service index was unavailable.
- OpenAPI focused regressions are green: request-body assignment, client model arrays/enums, referenced server/client model names, composed request-body assignment, C# keyword handling, and regeneration-collision checks all passed. The OpenAPI regeneration run emitted only `NU1900` restore warnings.
- The complete `tests/OpenApiGeneratorSmoke/OpenApiGeneratorSmoke.csproj` run is green. Its permanent regression now verifies that the 672-schema Fortnox fixture is rejected by the 300-class import limit, while a bounded valid Fortnox-shaped specification covers import skeleton generation; all server/client generation, security, dialect, compile and preservation markers passed.


- WebHostConfigSmoke API-doc regression fixed: fixture now includes explicit [JsonSchema] and typed fallback endpoints; WebHostConfigSmoke green.
- WebIisPackageProbe green, including missing-main.xps rejection.
- Graphify query/update attempted; graphify binary still fails with uv trampoline canonicalize error.

- Kestrel smoke is green after accepting Windows transport-level connection reset/aborted outcomes for oversized chunked bodies and request-header timeout enforcement; handler-reachability assertions remain active.

- Unified native class property Get/Let/Set type gating with the supported class-method native signature matrix; DB, SystemInventory, XPAI and Notes native accessor signatures no longer hit the stale rejection list.
- Compiler Release build and complete tests/ast-compile-probe/run-ast-cli.ps1 passed with AST CLI compilation probe passed.
- Graphify update attempted after source change; still fails with uv trampoline canonicalize error.

- Spreadsheet AST runtime regression initially timed out while exercising intentionally unsafe XLSX packages: the generated rejection process can leave inherited output handles open after reporting the expected failure. The focused probe now uses a bounded expected-failure path that terminates only that child tree and does not wait on inherited pipes. It still rejects every unsafe fixture and accepts only the expected diagnostic/timeout classification.
- Focused verification is green: `dotnet run --project tests/AstSpreadsheetRuntimeProbe -c Release --no-restore -- .` reached `AST_SPREADSHEET_RUNTIME_OK`.
- Graphify update attempted after the probe change; still fails with `uv trampoline failed to canonicalize script path`.

- Migrated AST `--target webiis` from an explicit rejection to the deployable WebIIS package path, preserving `--runtime`, `--output`, and result-format behavior. The CLI compatibility fixture now compiles `demo/webiis/main.xps` in AST mode and requires a successful `.zip` package.
- Focused verification is green: Release compiler build (only the existing NU1900 vulnerability-service warning) and `tests/cli-compatibility/run-cli-compatibility.ps1` reached `CLI_COMMAND_LINE_COMPATIBILITY_OK`.

- Migrated the AST ZIP/in-memory `Archive` path through the existing `ArchiveObjectPreprocessor` and archive runtime sources. The AST compiler now lowers `New Archive`, archive members, and memory round-trips without routing the source to legacy; extended archive formats remain separately gated.
- Added the AST Archive memory check to the permanent archive FullTest. Direct verification is green: `archive-memory.xps` prints `Format: ZIP`, `Files: 2`, and `hello from memory`; `ArchiveCapabilityProbe` reaches `ARCHIVE-CAPABILITY-PROBE=OK`.
- The full archive FullTest was attempted but stopped before the new AST case because the environment lacks the required `bzip2` executable. No software or MSI was installed.

- Extended Archive memory coverage is also green through AST: `archive-extended-memory.xps` reaches `ARCHIVE_EXTENDED_MEMORY_OK`, proving the existing extended reader/writer/factory runtime injection works for `New Archive(..., True)` without an external tool.
- Added the extended Archive memory execution to the permanent Archive FullTest. The suite remains environment-blocked before these AST cases when `bzip2` is unavailable.

- Began NetworkTools AST migration using the existing object preprocessor, native runtime, and Phase-2 postprocessor. NetworkTools/result types are now AST-routed; `New NetworkTools` has native constructor binding/emission and generated code includes the required certificate namespaces.
- Added `tests/ast-compile-probe/networktools-local-safe.xps` to Platform FullTest. Focused verification is green with `NETWORKTOOLS_AST_SAFE_OK`; the broader port-enumeration sample remains sensitive to OS access restrictions and is not claimed green here.

- Expanded the NetworkTools AST safe regression to cover Phase-2 address-family, link-local, multicast, subnet parsing and containment methods. The fixture still passes with `NETWORKTOOLS_AST_SAFE_OK`.

- Added focused class-property regression for a native SystemInventorySnapshot accessor; AST compile and execution passed with CLASS_PROPERTIES_NATIVE_OK.
- Full AST CLI probe had already passed before this focused fixture extension.
- Graphify update attempted; still fails with uv trampoline canonicalize error.

- Migrated `XPHttpDbSupabase` and `XPHttpDbDominoRest` constructors, typed members, runtime type emission, and HTTP database runtime activation into the AST compiler. Added permanent Platform FullTest coverage via `http-database-native-safe.xps`; focused AST compile/run passed `HTTP_DATABASE_AST_SAFE_OK`. Release build passed with the existing NU1900 vulnerability-service warning. Graphify update attempted again; still fails with uv trampoline canonicalize error.

- Migrated the legacy `XPDBMsSQL` spelling to the existing `XPDbMsSql` AST/runtime surface, including alias members and constructor binding. Added `mssql-alias-native-safe.xps` and Platform FullTest coverage.

- Added an executable AST regression for `XPAITool`, `XPAIToolFunction` and `XPAIToolCall` construction, function/parameter registration and lookup. This is the next validation point for the XP-prefixed AI tool surface.

- Migrating `XPJsonValidationResult` into the AST typed object surface; added metadata for validation properties, error lookup and JSON projection.

- Migrating `XPShellResult`: added an AST runtime implementation of `ShellExecute` with exit code, stdout, stderr and timeout state, plus typed result metadata and Platform FullTest coverage.

- Migrating `XPPathValue` onto the existing AST `XpPath` runtime with typed path operations and Platform FullTest coverage.

- Migrating `XPFileInfoValue` onto the existing AST `XpFileInfo` runtime; added typed metadata and a safe existing-file regression.

- Current continuation checkpoint: branch `investigate-ast-compiler` is clean apart from the unrelated private helper and generated probe output directories. Legacy desktop/console target aliases are implemented and pushed; the next open work is the broader legacy CLI parameter parity audit. The HTTP/XML collection-property duplicate-runtime experiment was reverted and remains uncommitted/open.

- Follow-up verification: the isolated `run-ast-array-signature.ps1` suite passes, but the full `run-ast-cli.ps1` wrapper currently terminates after entering that nested suite without exposing a PowerShell exception. No harness change was kept; this wrapper issue must be isolated before claiming the full AST suite green.

- Fixed the nested array-signature harness: expected negative compiler cases now run through `Start-Process`, preserving exit code and diagnostics without PowerShell native-command termination. The isolated suite passes `AST_ARRAY_SIGNATURES_OK` with exit code 0. The full wrapper still needs separate timing/termination investigation.

- Full AST wrapper re-run was observed as a live process rather than a false immediate failure: it passed the nested suite and more than 100 subsequent markers, including file IO, IsDir drive handling, database/AI/XPAI, class methods/properties, HTTP collections and initialized module storage. Observation was stopped after the normal-compiler `array-storage-parity` phase produced its output but did not return a visible process; the full wrapper is not yet claimed green.

- Hardened the normal/AST array-parity wrapper to execute the legacy apphost with `Start-Process` and captured stdout/stderr. The isolated legacy executable produced `-3`, `-1`, `7`, `ARRAY_STORAGE_PARITY_OK` and exited 0; the former direct invocation was the unstable step.

- Removed unused stderr redirection from the parity apphost process after reproducing a full-wrapper wait at that block. Focused execution now returns 0 with the expected four-line parity output; full AST wrapper rerun remains the final confirmation.

- Full AST probe reached `class-properties-native` and exposed an invalid legacy-parity assertion: the fixture intentionally exercises AST-only native property signatures. Removed only that legacy comparison; focused AST execution remains green with `CLASS_PROPERTIES_NATIVE_AST_ONLY_OK`.

- Native database class-property regression exposed unsafe generated dependency runtime; added AllowUnsafeBlocks to generated legacy/dependency projects.
- Verified compiler Release build and focused class-properties-native AST compile/run with SystemInventorySnapshot, XPDBSQLite and XPDbSupabase accessors: CLASS_PROPERTIES_NATIVE_OK.
- Graphify update attempted; still fails with uv trampoline canonicalize error.

- Extended focused native class-property regression to XPDbMySql and XPDbMsSql; AST compile/run remains green with CLASS_PROPERTIES_NATIVE_OK.

- Extended native class-property support with XPAttachmentCollection; compiler Release build and focused class-properties-native run passed.
- HTTP/XML collection accessor experiment exposed duplicate generated HTTP runtime definitions and was reverted; this remains a separate follow-up investigation.
- Graphify update attempted; still fails with uv trampoline canonicalize error.

- Normalized CLI argument failures so invalid compile options return deterministic exit code 2 diagnostics instead of an unhandled `ArgumentException`. The isolated CLI build is warning-free and `run-cli-compatibility.ps1` completes with `CLI_COMMAND_LINE_COMPATIBILITY_OK`.

- Fixed false database attachment feature activation: source declarations named Attachments no longer inject duplicate HTTP/attachment runtime; actual .Attachments(...) calls still inject and execute correctly.
- Verified compiler Release build, CLASS_PROPERTIES_NATIVE_OK and DATABASE_ATTACHMENTS_OK.
- Graphify update attempted; still fails with uv trampoline canonicalize error.

- Performance baseline: a representative AST compilation averaged 2.8 seconds over three runs. A clean isolated `dotnet build XPScriptCompiler.slnx -c Release --no-restore -p:UseSharedCompilation=false` completed in 72.7 seconds with 0 warnings and 0 errors, including the Windows, Linux x64 and macOS x64/arm64 publish targets. This is a baseline only; the performance investigation remains open.
- Warm/cold run benchmark 2026-10-23: `CompilerRunWarmBenchmark` reported `localColdMs=10960`, `localDebugFreshMs=9836`, `daemonColdMs=19072`, warm samples `10042,11714,18010` (median `11714` ms), and debug warm samples `10576,10209,9710` (median `10209` ms). These timings include process startup and generated-project execution; they are a measurement baseline, not a performance claim.
- Full CLI compatibility verification passed with `CLI_COMMAND_LINE_COMPATIBILITY_OK`, covering AST/legacy compile and run aliases, target validation, WebIIS packaging, runtime identifiers, layout switches, result formats and deterministic invalid-argument diagnostics.
- The broad `tests/ast-compile-probe/run-ast-cli.ps1` regression passed with `AST CLI compilation probe passed.` It covered file-not-found/path errors, `IsDir` drive handling, SQLite/MySQL/MSSQL/Supabase, SystemInventory, AI/XPAI, HTTP/XML/CSV/JSON, NetworkTools, class methods/properties, constructors/destructors, arrays and control flow.
- Local web security and hosting verification passed: `WEB-SECURITY-CLOSEOUT=OK`, `WEB-CSRF-SMOKE=OK`, `WEB-XSS-SMOKE=OK`, `WEB-SERVER-SCRIPT-GUARD=OK`, `WEB-RESPONSE-LIMIT=OK`, `WEB-REST-API-SMOKE=OK`, `WEB-KESTREL-SMOKE=OK`, `WEB-CGI-SMOKE=OK` and `WEB-FASTCGI-SMOKE=OK`. CGI/FastCGI were rerun with isolated output because stale smoke processes had locked the default build artifacts.
- Local Android compiler and project metadata probes now pass directly: `ANDROID-COMPILER-PROBE=OK` and `ANDROID-PROJECT-METADATA-PROBE=OK`. The metadata probe now references `XPScript.Compiler.Core`, matching the compiler API it exercises; physical device/emulator verification remains a separate local setup gate.
- NuGet verification: `System.Security.Cryptography.Pkcs` is already at `10.0.13`; upgraded direct `MimeKit` from `4.18.1` to `4.18.2` and verified the compiler Release build with 0 warnings and 0 errors. Remaining outdated entries are transitive major-version updates and require separate compatibility work.
- Notes JSON AST-surface verification 2026-10-24: removed the legacy `XPJSONNavigator` assertion from the AST `NotesHTTPRequest` runtime probe because Notes JSON types are intentionally excluded from the AST surface; the focused HTTP/TLS probe passed with `AST_NOTES_HTTP_REQUEST_OK`, and the full `tests/ast-compile-probe/run-ast-cli.ps1` suite passed with `AST_NOTES_JSON_EXCLUDED_OK` and `AST CLI compilation probe passed.`
