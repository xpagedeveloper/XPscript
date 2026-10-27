# AST compiler extension guide

`AstApplicationSupport` binds runtime namespaces and shared state proxies from Application source metadata. Namespace handles are not constructible objects; property targets refer to existing static runtime members, while state/registry instances keep their real types. Read-only writes and constructors must retain stable CLI/MCP errors. Native property writes need the same native conversion as method arguments (for example Long to checked ExitCode int). The entry point initializes both shared argument stores before the bound Main, preserving recursion/calls to Main via its generated target name. Use the existing source-marker extractor and generated-project backend for literal executable metadata. Select only required Application subsystems; do not replace OS credential/host guards with test stubs.

`AstHttpSupport` reuses the same source-derived native surface as XML/CSV, and composes the existing HTTP, JSON/authentication helpers and TLS validator conditionally. The AST runtime exception also comes from the shared core contract, so error capture uses the same Number property. Keep transport security in those shared runtimes; do not retain the former certificate callback that always returned true. Async callbacks/UIForm still select whole-file legacy routing rather than a no-op AST stub.

XML uses `AstXmlSupport` and the shared `AstNativeObjectSurface` metadata importer (also used by CSV) to derive all nine object types, inherited members and optional native overloads from `NativeXmlRuntimeSource`. Keep only XML-specific detection, element/node compatibility, constructors and conditional runtime selection in the XML bridge. Use the existing DOM/DTD implementation; do not add another parser or relax its resolver/input limits. `ReferenceUpcast` models an element flowing to a node parameter without permitting the reverse implicit conversion. Explicit `Call .Member(...)` inside With follows the same bound call path as ordinary implicit calls.

Native CSV uses `AstCsvSupport` to derive the six object surfaces from `NativeCsvRuntimeSource`, bind typed indexing/iteration and lower `FromBytes` into reference replacement. The shared CSV engine is emitted only on actual use; escape-only programs retain just the existing escape helpers. File loading is shared with normal CSV/spreadsheet compilation without pulling spreadsheet code into CSV-only output.

`AstSpreadsheetSupport` derives XPSpreadsheet, XPWorksheet, XPCell and XPRange from the existing package-free ZIP/XML runtime. Public properties with private setters remain read-only. Shared helper-backed instance calls use native metadata's `ReceiverArgument`: the emitter evaluates the receiver once, before arguments, and passes it to the existing helper rather than implementing a second conversion. Include CSV interoperability only when needed, including calls whose results are Variant. Spreadsheet relative paths use the shared script-directory bootstrap. Native `Range.Values` uses LSArray; bounds, Variant indexing and ForAll must use the shared array/enumeration runtime, not an empty placeholder or an IEnumerable-only shortcut.

XPImage is an example of a migrated native object: `AstImageSupport` derives public constructors, properties and overloaded methods from `XPImageRuntimeSource`, imports typed binder symbols and emits only native argument adaptations and ownership glue. It includes the existing image/filesystem runtime sources on actual object use. `RuntimeObjectType` supplies the same native type mapping to locals, parameters, conversions, Static storage and protected instruction graphs. The shared addon SDK backend remains responsible for NuGet/native assets and notices; do not add object-specific package copying.

Generated dependency builds are shared by AST and normal compilation. Declare each addon package, version and runtime-use detection in `ApplicationDependencyCatalog`; the generated-project builder consumes that catalog without a per-addon backend branch. Emit runtime code only when the feature is used. Package-free programs use framework references directly. Programs requiring NuGet packages use the SDK backend with the current runtime identifier, which restores the selected direct/transitive graph and copies managed/native assets. `Third-party-license.txt` comes from that exact assets file, including persistent restore-cache builds. Test both addon use and absence, native execution and notices; `tests/GeneratedDependencyProbe` covers Magick.NET and SQLite.

New XP objects must be added through the existing compiler pipeline. Do not
add a second parser or a second machine-interface implementation.

Changes to shared binding, conversions, native type mappings or emission must
also verify other affected object families. Run the focused regression for the
changed object first, then existing regressions covering shared reference identity,
ByRef replacement and relevant ownership differences (including XPImage when
ownership code changes). Check JSON, XML/CSV, HTTP and database behavior where
they consume the changed path. Keep family-specific adaptations scoped to their
semantic type; never infer an object's behavior from a variable name or a shared
CLR representation. Record which families were executed and retain the permanent
FullTest coverage; one object's passing test does not establish cross-object safety.

1. Register the object in `AstObjectSupportRegistry`. If the object is not
   ready for AST lowering, return the documented legacy fallback decision.
2. If the object is allowed as a class-method parameter or result, add its
   capability to `AstObjectSupportRegistry.ClassMethodNativeTypeNames`. Do
   not add a second type list inside `AstClassMethodSupport`; that binder
   should keep only family-specific rules (for example XML upcasts or image
   ownership).
   Native Notes types belong in the registry's `NotesRuntimeTypes` definition:
   its keys also populate class-method capabilities, and the shared emitter
   reads its runtime mappings. Do not duplicate Notes mappings in the emitter.
3. Add or reuse syntax nodes and bind them to typed `BoundNode` values. Keep
   XPscript names, source spans and diagnostics in the bound model.
4. Emit through the shared bound emitters and existing runtime APIs. Generated
   C# is an implementation detail and must not contain XPscript parsing logic.
5. Add a focused executable fixture for successful behavior and a focused
   unsupported-feature fixture when lowering is intentionally deferred.
6. Run the fixture through AST CLI and MCP validation when the feature is
   exposed there. Compare diagnostics by code, source file, span and meaning.
7. Preserve third-party notice generation and existing target/runtime
   packaging; an object migration must not introduce a new NuGet dependency
   unless the dependency is explicitly required and reviewed.

The minimum hand-off is a normal compiler build, the focused AST emission or
execution probe, and the MCP protocol probe when the machine interface is
affected.
