# Compiler command/member scope TODO

Goal: ensure XPScript global commands and runtime functions are resolved independently from class members, properties, methods, parameters, locals, and other scoped identifiers.

Resolution rule: lexical keywords are syntax-level restrictions; compiler-reserved names are restricted only in their applicable declaration scopes; runtime/global functions are resolved only for unqualified calls. Member access after `.` belongs to the receiver's class/type scope and must not be captured by a global runtime rewrite.\n\nA runtime/global function name must not become globally reserved merely because the compiler knows how to rewrite that function. For example, a class member named `JsonParse` or `StrLeftBack` must remain legal when the grammar permits it, while an unqualified call such as `JsonParse(...)` may still resolve to the runtime function.

## 1. Establish scope and resolution rules

- [x] Document the identifier scopes used by the compiler: types, module/global symbols, class members, procedure parameters, locals, enum members, and compiler-generated symbols.
- [x] Distinguish lexical/syntax keywords from runtime API names and compiler rewrite markers.
- [x] Define when an unqualified function call resolves to a built-in/runtime function.
- [x] Define member-access behavior so `object.Name` / `object.Name(...)` is never treated as an unqualified global command solely because `Name` matches a runtime function.
- [ ] Verify case-insensitive resolution consistently.

## 2. Audit reserved identifier validation

- [x] Audit `ReservedIdentifierPreprocessor` by declaration kind and scope.
- [x] Keep compiler-generated `__*` protection.
- [x] Keep runtime type-name protection only where a type declaration would actually collide.
- [ ] Verify runtime value names such as `Application` and `Body` are restricted only in scopes where the collision is real.
- [x] Ensure ordinary runtime commands/functions are not added to a global reserved-name list.

## 3. Audit all command/function preprocessors

- [ ] Inventory every preprocessor that rewrites a named command/function using regex or textual replacement.
- [ ] Verify every global-call rewrite rejects member access such as `.Name(...)`.
- [x] Audit `NativeHttpJsonPreprocessor`, especially `JsonParse`, `JsonStringify`, `JsonEncode`, and `JsonDecode`.
- [x] Audit `ReferenceRuntimeExtensionsPreprocessor`; preserve its existing member-access exclusion behavior.
- [ ] Audit HTTP, JSON, XML, CSV, database, Notes, UI, AI, filesystem, string, date, application, and compatibility preprocessors for the same class of bug.
- [ ] Replace fragile regex-only resolution with a shared helper/token-aware mechanism where practical.

## 4. Regression tests

- [x] Add a class property whose name matches a runtime/global function.
- [x] Add a class method whose name matches a runtime/global function.
- [ ] Add a parameter/local whose name matches a runtime/global function where syntactically valid.
- [x] Verify `obj.JsonParse(...)` remains a member call.
- [x] Verify `JsonParse(...)` still resolves to the native JSON runtime function.
- [x] Verify `obj.StrLeftBack(...)` remains a member call.
- [x] Verify `StrLeftBack(...)` still resolves to the reference runtime function.
- [ ] Add representative regressions from every preprocessor family discovered by the audit.
- [ ] Add negative tests for true language keywords and compiler-reserved `__*` names.
- [x] Run regressions through the real XPScript transpiler/compiler, not only string-level unit tests.

## 5. Scope collision matrix

- [ ] Test same spelling across type vs member.
- [ ] Test same spelling across two different classes.
- [ ] Test same spelling across member vs global/runtime function.
- [ ] Test same spelling across method parameter vs class member.
- [ ] Test same spelling across local vs class member.
- [ ] Test overloaded/member-call syntax separately from property access.
- [ ] Verify generated OpenAPI code follows the same scope rules instead of maintaining a broader pseudo-reserved list.

## 6. Completion gate

- [ ] All affected compiler tests pass on Windows, Linux, and macOS.
- [ ] Existing runtime function calls remain backward compatible.
- [ ] Existing valid member/property names are not renamed or rejected merely because they match an XPScript command/runtime API name.
- [ ] Add documentation describing the difference between XPScript keywords, compiler-reserved identifiers, runtime functions, and scoped user symbols.
