---
name: ast-compiler-development
description: Extend XPScript AST compiler support for XP objects through the shared registry, binder, emitter and runtime contract.
---

# AST compiler development

Use this workflow when adding support for an XP object or AST-only language feature.

## Object support contract

1. Add the object name and detection rules to `AstObjectSupportRegistry`.
2. Define the shared semantic symbols and signatures in the binder catalog.
3. Bind declared members with stable XPScript types and source spans.
4. Emit calls and member access through existing runtime APIs; do not duplicate runtime behavior.
5. Add the runtime surface in its own source file when the object needs generated support.
   For a native source-defined object, see `AstImageSupport`: derive compiler metadata from the existing runtime, preserve native overloads and static/instance distinctions, and share native type mappings across ordinary/protected locals, parameters and Static storage. Test ownership, returned references (including Variant), ByVal/ByRef and exceptional cleanup as well as API calls.
   For a required NuGet addon, register its version and runtime-use detection in `ApplicationDependencyCatalog`. The shared SDK backend adds selected package references and native assets, and generates `Third-party-license.txt` from the restored graph. Do not add a separate package-copy or license implementation for each object; verify both use and absence through `tests/GeneratedDependencyProbe` and the permanent Runtime FullTest.
6. Keep unsupported-object diagnostics explicit until the complete runtime contract exists.

## Verification

- Add a focused AST source fixture for success and unsupported cases.
- Run AST CLI compilation and execute the generated program when applicable.
- Run MCP/CLI validation for the same source and compare structured diagnostics.
- Run the relevant compiler probe and a normal `xpscript compile`.
- Commit and push only after build and tests pass.

## Design rule

IndexedPropertySymbol now carries explicit ByRef index modes. Preserve them in both bound call emission and generated accessor wrapper signatures, sharing index argument emission for Get/Let. Require matching scalar types and named writable locals/parameters/bare class fields, reject mismatched getter/setter modes, and defer complex locations explicitly. Verify same-variable aliases, forwarding, RHS order and exceptional exits; never implement a second copy-back engine for named storage.

Scalar indexed properties reuse IndexedPropertySymbol/BoundIndexedPropertyExpression and the common accessor-body binder/emitter. Register independent read/write permissions, typed index signatures and canonical getter/setter targets. Evaluate receiver, indexes and incoming value once in order. Current wrappers snapshot ByVal/unmodified scalar indexes; complex ByRef locations, Optional indexes and object Set remain explicit deferrals. Keep semantic local storage types shared across ordinary, flat and error-flow emission.

Scalar non-indexed class properties reuse the same procedure binder/emitter as class methods, with property wrappers calling bound Get/Let bodies. Register grouped property symbols with independent CanRead/CanWrite permissions; preserve private accessor visibility and canonical names, including implicit With. Parameterless Let uses the property name for the incoming value. Do not fabricate auto-property storage or close indexed/object Set migration from this subset.

User class procedures use `AstClassMethodSupport`, shared `AstExperimentalCompiler.BindProcedure` control-flow validation and the instance mode of BoundMethodEmitter. Keep lexical class/member/parameter scopes separate, canonicalize declared member casing, omit private members externally and never scan class fields as module globals. Scope Program helper imports to generated user classes. Scalar typed bodies are verified; object/array/List signatures, Optional/Static and derived bodies remain explicit XPS3001 deferrals. Do not close the parent class TODO until accessors, constructors, inheritance and destructor dispatch are migrated.

For Application, use `AstApplicationSupport`, source-derived native namespaces and the shared runtime/state proxies. Bootstrap command-line arguments before the bound Main; preserve Main call targets, Args snapshots and error 9. Use existing property targets/native conversions, reject read-only writes and New Application, and synchronize normal/MCP constructor/read-only diagnostics. Preserve OS credential/host guards, debugger activation and generated executable metadata through the existing packaging path. Include services only on actual use, without addons. Test registry only in isolated storage (Linux supports absolute XDG_CONFIG_HOME); never use real credentials for migration tests.

For HTTP use `AstHttpSupport` and the shared native metadata importer. Compose the real TLS validator and runtime exception from `CoreCompatibilityRuntimeSource`; never introduce an always-successful certificate callback or duplicate transport logic. Normal/AST JSON and authentication facades must delegate to existing helpers. Preserve whole-file legacy routing for async callbacks and UIForm. Test real local HTTP/TLS traffic, Strict/self-signed hostname/expiry rejection, SSRF restrictions, fractional timeouts, mapped diagnostics and absence of HTTP/TLS code when unused. Header dictionaries are dynamic Variant-indexed values, not integer-only arrays.

For native XML, use `AstXmlSupport` with `AstNativeObjectSurface`, shared by CSV. The source-derived surface must retain inherited members and optional native arities. XML element-to-node conversion is a reference upcast, not a Variant coercion; ByRef still requires an exact type. Reuse `NativeXmlRuntimeSource` with its DTD/entity restrictions and resource limits. Test actual normal/AST execution, typed collections, reference semantics, implicit With calls and member names such as `.Parent` that must not be rewritten as class references. Verify unused/escape-only XML does not include the DOM or addon dependencies.

Object detection belongs in the registry. Binding, emission and runtime implementation remain separate layers so adding another XP object does not require expanding `AstExperimentalCompiler` with more object-specific regex logic.

For CSV, use `AstCsvSupport` and the existing `NativeCsvRuntimeSource`; do not introduce another CSV parser. Its conditional runtime selection is tested for actual use, strings/comments and escape-only programs. CSV uses shared managed references (not XPImage's ByVal cloning/disposal). Lower reference-replacing operations such as `FromBytes` through bound statements for both ordinary and `Call` syntax, preserving argument evaluation order. CSV/spreadsheet interop now uses the shared native helper through AST; helper-backed instance calls pass the receiver once via `ReceiverArgument`.

For spreadsheets, use `AstSpreadsheetSupport` and `SpreadsheetRuntimeSource.CoreCode`, adding the existing CSV interoperability source only on actual interop use. Do not add ClosedXML or another XLSX engine: the existing runtime uses framework ZIP/XML. Preserve external-workbook read-only/version guards, ZIP size/count limits, script-relative file paths and private-setter read-only properties. ByVal shares references; ByRef replaces them. Range.Values is native rank-one LSArray and must use shared bounds/index/ForAll handling. Compare existing demos through real normal and AST compilation, and test inferred interop results, malformed input, unused code and mapped MCP diagnostics before closing migration.
