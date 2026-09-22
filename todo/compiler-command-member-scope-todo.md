# Compiler command/member scope TODO

Goal: ensure XPScript global commands and runtime functions are resolved independently from class members, properties, methods, parameters, locals, and other scoped identifiers.

Resolution rule: lexical keywords are syntax-level restrictions; compiler-reserved names are restricted only in their applicable declaration scopes; runtime/global functions are resolved only for unqualified calls. Member access after `.` belongs to the receiver's class/type scope and must not be captured by a global runtime rewrite. 

A runtime/global function name must not become globally reserved merely because the compiler knows how to rewrite that function. For example, a class member named `JsonParse` or `StrLeftBack` must remain legal when the grammar permits it, while an unqualified call such as `JsonParse(...)` may still resolve to the runtime function.

## 1. Establish scope and resolution rules

- [x] Document the identifier scopes used by the compiler: types, module/global symbols, class members, procedure parameters, locals, enum members, and compiler-generated symbols.
- [x] Distinguish lexical/syntax keywords from runtime API names and compiler rewrite markers.
- [x] Define when an unqualified function call resolves to a built-in/runtime function.
- [x] Define member-access behavior so `object.Name` / `object.Name(...)` is never treated as an unqualified global command solely because `Name` matches a runtime function.
- [x] Verify case-insensitive resolution consistently. Relevant rewrite paths use `RegexOptions.IgnoreCase` and scoped symbol sets use case-insensitive comparers.

## 2. Audit reserved identifier validation

- [x] Audit `ReservedIdentifierPreprocessor` by declaration kind and scope.
- [x] Keep compiler-generated `__*` protection.
- [x] Keep runtime type-name protection only where a type declaration would actually collide.
- [x] Verify runtime value names such as `Application` and `Body` are restricted only in scopes where the collision is real. Locals, parameters and procedure declarations remain reserved; receiver member access is allowed and covered by regression.
- [x] Ensure ordinary runtime commands/functions are not added to a global reserved-name list.

## 3. Audit all command/function preprocessors

- [x] Inventory the primary global function rewrite paths in `AdvancedXPScriptTranspiler`, `CoreCompatibilityTranspiler`, `CrossPlatformPreprocessor`, `HashFunctionsPreprocessor`, `ReferenceRuntimeExtensionsPreprocessor`, and the native JSON/XML/CSV preprocessors. Continue auditing feature-specific preprocessors as regressions identify additional global rewrites.
- [>] Verify every global-call rewrite rejects member access such as `.Name(...)`. Core/runtime, cross-platform, hash, reference-runtime, JSON, XML, CSV, file-I/O, text-I/O, operator/array and shared `PreprocessorFeatureGate.ContainsCall` paths have been checked/fixed.
- [x] Audit `NativeHttpJsonPreprocessor`, especially `JsonParse`, `JsonStringify`, `JsonEncode`, and `JsonDecode`.
- [x] Audit `ReferenceRuntimeExtensionsPreprocessor`; preserve its existing member-access exclusion behavior.
- [>] Audit HTTP, JSON, XML, CSV, database, Notes, UI, AI, filesystem, string, date, application, and compatibility preprocessors for the same class of bug. JSON/XML/CSV, reference runtime, hash, cross-platform, HCL selected compatibility, core runtime, Notes runtime, Archive, Spreadsheet, UI extension, NetworkTools, SystemInventory, file-I/O, text-I/O, operator/array, date-object and type-coercion paths checked so far. The latter object preprocessors operate on explicit receiver/type syntax rather than unqualified global function names.
- [>] Replace fragile regex-only resolution with a shared helper/token-aware mechanism where practical. Shared `PreprocessorFeatureGate.ContainsCall` now uses code-only input and excludes receiver/member access; Text I/O, hash, reference-runtime, type-coercion, operator/array, HCL selected compatibility, cross-platform, JSON, XML, and CSV global calls now use the shared scoped rewriter; remaining direct rewrite sites are being audited before broader consolidation.
  - Audited Archive, Spreadsheet, and native-library platform preprocessors: their remaining rewrites are receiver/type/declaration-specific rather than unqualified global-call resolution, so they should stay specialized.
  - Audited LanguageExtensions, ApplicationObject, and GeneralSyntax preprocessors: optional-call and enum rewrites already exclude receiver scope; Application rewrites target the reserved runtime root explicitly; general syntax rewrites language constructs rather than global command names.
  - Audited AdvancedXPScriptTranspiler runtime-function lowering and DateObjectPreprocessor: parenthesized runtime functions and Date receiver rewrites already use member-aware `(?<![\w.])` boundaries. CoreCompatibilityTranspiler delegates final expression lowering to AdvancedXPScriptTranspiler, so these paths preserve receiver/member scope.
  - Audited NetworkTools, SystemInventory, UIExtension, and AttachmentCollection preprocessors: remaining transformations are explicit type construction or tracked receiver-instance rewrites, not unqualified global command resolution.
  - Audited NotesRuntime, NotesSessionAutoDetect, FileIoExtensions, and PropertyLetCompatibility preprocessors: Notes rewrites are explicit Notes types/tracked receivers, `Input# Compiler command/member scope TODO

Goal: ensure XPScript global commands and runtime functions are resolved independently from class members, properties, methods, parameters, locals, and other scoped identifiers.

Resolution rule: lexical keywords are syntax-level restrictions; compiler-reserved names are restricted only in their applicable declaration scopes; runtime/global functions are resolved only for unqualified calls. Member access after `.` belongs to the receiver's class/type scope and must not be captured by a global runtime rewrite. 

A runtime/global function name must not become globally reserved merely because the compiler knows how to rewrite that function. For example, a class member named `JsonParse` or `StrLeftBack` must remain legal when the grammar permits it, while an unqualified call such as `JsonParse(...)` may still resolve to the runtime function.

## 1. Establish scope and resolution rules

- [x] Document the identifier scopes used by the compiler: types, module/global symbols, class members, procedure parameters, locals, enum members, and compiler-generated symbols.
- [x] Distinguish lexical/syntax keywords from runtime API names and compiler rewrite markers.
- [x] Define when an unqualified function call resolves to a built-in/runtime function.
- [x] Define member-access behavior so `object.Name` / `object.Name(...)` is never treated as an unqualified global command solely because `Name` matches a runtime function.
- [x] Verify case-insensitive resolution consistently. Relevant rewrite paths use `RegexOptions.IgnoreCase` and scoped symbol sets use case-insensitive comparers.

## 2. Audit reserved identifier validation

- [x] Audit `ReservedIdentifierPreprocessor` by declaration kind and scope.
- [x] Keep compiler-generated `__*` protection.
- [x] Keep runtime type-name protection only where a type declaration would actually collide.
- [x] Verify runtime value names such as `Application` and `Body` are restricted only in scopes where the collision is real. Locals, parameters and procedure declarations remain reserved; receiver member access is allowed and covered by regression.
- [x] Ensure ordinary runtime commands/functions are not added to a global reserved-name list.

## 3. Audit all command/function preprocessors

- [x] Inventory the primary global function rewrite paths in `AdvancedXPScriptTranspiler`, `CoreCompatibilityTranspiler`, `CrossPlatformPreprocessor`, `HashFunctionsPreprocessor`, `ReferenceRuntimeExtensionsPreprocessor`, and the native JSON/XML/CSV preprocessors. Continue auditing feature-specific preprocessors as regressions identify additional global rewrites.
- [>] Verify every global-call rewrite rejects member access such as `.Name(...)`. Core/runtime, cross-platform, hash, reference-runtime, JSON, XML, CSV, file-I/O, text-I/O, operator/array and shared `PreprocessorFeatureGate.ContainsCall` paths have been checked/fixed.
- [x] Audit `NativeHttpJsonPreprocessor`, especially `JsonParse`, `JsonStringify`, `JsonEncode`, and `JsonDecode`.
- [x] Audit `ReferenceRuntimeExtensionsPreprocessor`; preserve its existing member-access exclusion behavior.
- [>] Audit HTTP, JSON, XML, CSV, database, Notes, UI, AI, filesystem, string, date, application, and compatibility preprocessors for the same class of bug. JSON/XML/CSV, reference runtime, hash, cross-platform, HCL selected compatibility, core runtime, Notes runtime, Archive, Spreadsheet, UI extension, NetworkTools, SystemInventory, file-I/O, text-I/O, operator/array, date-object and type-coercion paths checked so far. The latter object preprocessors operate on explicit receiver/type syntax rather than unqualified global function names.
- [>] Replace fragile regex-only resolution with a shared helper/token-aware mechanism where practical. Shared `PreprocessorFeatureGate.ContainsCall` now uses code-only input and excludes receiver/member access; Text I/O, hash, reference-runtime, type-coercion, operator/array, HCL selected compatibility, cross-platform, JSON, XML, and CSV global calls now use the shared scoped rewriter; remaining direct rewrite sites are being audited before broader consolidation.
  - Audited Archive, Spreadsheet, and native-library platform preprocessors: their remaining rewrites are receiver/type/declaration-specific rather than unqualified global-call resolution, so they should stay specialized.
  - Audited LanguageExtensions, ApplicationObject, and GeneralSyntax preprocessors: optional-call and enum rewrites already exclude receiver scope; Application rewrites target the reserved runtime root explicitly; general syntax rewrites language constructs rather than global command names.
  - Audited AdvancedXPScriptTranspiler runtime-function lowering and DateObjectPreprocessor: parenthesized runtime functions and Date receiver rewrites already use member-aware `(?<![\w.])` boundaries. CoreCompatibilityTranspiler delegates final expression lowering to AdvancedXPScriptTranspiler, so these paths preserve receiver/member scope.
 retains its specialized `#fileNo` syntax with member exclusion, and Property Let is a declaration-only compatibility rewrite.
  - Audited TypeDeclarationPreprocessor, XPScriptTranspiler helper rewrites, and UIForm generated-code postprocessors: these operate on declarations, tracked list variables, compiler-generated labels, or generated C# runtime structure rather than user-level unqualified command/function resolution.
  - Audited ModuleGlobals, ModuleObjectGlobals, and IndexedProperty preprocessors: module array/object rewrites are symbol-tracked and member-aware, while indexed-property lowering intentionally preserves an optional receiver and rewrites it to generated getter/setter members.
  - Audited JsonHttpCompatibility, NotesMimeType, NotesDocumentSendWarning, and ServerSideMetadata preprocessors: these are tracked-type/receiver compatibility handling or metadata stripping and do not rewrite unqualified global command/function names.
  - Audited AI session/prompt runtime postprocessors, AI tool callback validation, and the configurable source-preprocessor pipeline: AI postprocessors patch generated C# runtime code, callback validation is declaration/registration-aware, and configurable source preprocessing is intentionally lexical user-configured replacement rather than built-in command resolution.
  - Audited IncludeSource, SourceLineMarker, and NotesSessionAutoDetect preprocessors: include/line-marker handling is directive/instrumentation-specific and NotesSession rewriting targets explicit construction syntax, not unqualified runtime calls.
## 4. Regression tests

- [x] Add a class property whose name matches a runtime/global function.
- [x] Add a class method whose name matches a runtime/global function.
- [x] Add a parameter/local whose name matches a runtime/global function where syntactically valid.
- [x] Verify `obj.JsonParse(...)` remains a member call.
- [x] Verify `JsonParse(...)` still resolves to the native JSON runtime function.
- [x] Verify `obj.StrLeftBack(...)` remains a member call.
- [x] Verify `StrLeftBack(...)` still resolves to the reference runtime function.
- [>] Add representative regressions from every preprocessor family discovered by the audit. JSON/XML/CSV/reference-runtime and operator/array coverage added. Cross-platform, HCL, JSON, XML, and CSV member-vs-global resolution now also has an explicit migrated-family regression, verified by FullTest run 465; more targeted family regressions remain.
- [x] Add negative tests for true language keywords and compiler-reserved `__*` names.
- [x] Run regressions through the real XPScript transpiler/compiler, not only string-level unit tests.

## 5. Scope collision matrix

- [x] Test same spelling across type vs member.
- [x] Test same spelling across two different classes.
- [x] Test same spelling across member vs global/runtime function.
- [x] Test same spelling across method parameter vs class member.
- [x] Test same spelling across local vs class member.
- [x] Test overloaded/member-call syntax separately from property access.
- [ ] Verify generated OpenAPI code follows the same scope rules instead of maintaining a broader pseudo-reserved list.

## 6. Completion gate

- [x] All affected compiler tests pass on Windows, Linux, and macOS. FullTest run 465 passed on exact branch HEAD `3b5196fe` across Windows, Ubuntu, and macOS with the explicit cross-platform/HCL/JSON/XML/CSV member-vs-global scope regression.
- [x] Existing runtime function calls remain backward compatible. FullTest and runtime regression suites passed.
- [x] Existing valid member/property names are not renamed or rejected merely because they match an XPScript command/runtime API name. Scope regressions pass through the real transpiler/compiler.
- [x] Add documentation describing the difference between XPScript keywords, compiler-reserved identifiers, runtime functions, and scoped user symbols.
