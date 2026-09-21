# Compiler command/member scope TODO

Goal: ensure XPScript global commands and runtime functions are resolved independently from class members, properties, methods, parameters, locals, and other scoped identifiers.

Resolution rule: lexical keywords are syntax-level restrictions; compiler-reserved names are restricted only in their applicable declaration scopes; runtime/global functions are resolved only for unqualified calls. Member access after `.` belongs to the receiver's class/type scope and must not be captured by a global runtime rewrite.\n\nA runtime/global function name must not become globally reserved merely because the compiler knows how to rewrite that function. For example, a class member named `JsonParse` or `StrLeftBack` must remain legal when the grammar permits it, while an unqualified call such as `JsonParse(...)` may still resolve to the runtime function.

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
- [>] Verify every global-call rewrite rejects member access such as `.Name(...)`. Core/runtime, cross-platform, hash, reference-runtime, JSON, XML, and CSV paths have been checked/fixed.
- [x] Audit `NativeHttpJsonPreprocessor`, especially `JsonParse`, `JsonStringify`, `JsonEncode`, and `JsonDecode`.
- [x] Audit `ReferenceRuntimeExtensionsPreprocessor`; preserve its existing member-access exclusion behavior.
- [>] Audit HTTP, JSON, XML, CSV, database, Notes, UI, AI, filesystem, string, date, application, and compatibility preprocessors for the same class of bug. JSON/XML/CSV, reference runtime, hash, cross-platform, HCL selected compatibility, core runtime, Notes runtime, Archive, Spreadsheet, UI extension, NetworkTools, and SystemInventory paths checked so far. The latter object preprocessors operate on explicit receiver/type syntax rather than unqualified global function names.
- [ ] Replace fragile regex-only resolution with a shared helper/token-aware mechanism where practical.

## 4. Regression tests

- [x] Add a class property whose name matches a runtime/global function.
- [x] Add a class method whose name matches a runtime/global function.
- [x] Add a parameter/local whose name matches a runtime/global function where syntactically valid.
- [x] Verify `obj.JsonParse(...)` remains a member call.
- [x] Verify `JsonParse(...)` still resolves to the native JSON runtime function.
- [x] Verify `obj.StrLeftBack(...)` remains a member call.
- [x] Verify `StrLeftBack(...)` still resolves to the reference runtime function.
- [>] Add representative regressions from every preprocessor family discovered by the audit. JSON/XML/CSV/reference-runtime coverage added; more families remain.
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

- [x] All affected compiler tests pass on Windows, Linux, and macOS. FullTest run 399 passed on exact branch HEAD.
- [x] Existing runtime function calls remain backward compatible. FullTest and runtime regression suites passed.
- [x] Existing valid member/property names are not renamed or rejected merely because they match an XPScript command/runtime API name. Scope regressions pass through the real transpiler/compiler.
- [x] Add documentation describing the difference between XPScript keywords, compiler-reserved identifiers, runtime functions, and scoped user symbols.
