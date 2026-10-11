# AST compiler extension guide

New XP objects must be added through the existing compiler pipeline. Do not
add a second parser or a second machine-interface implementation.

1. Register the object in `AstObjectSupportRegistry`. If the object is not
   ready for AST lowering, return the documented legacy fallback decision.
2. Add or reuse syntax nodes and bind them to typed `BoundNode` values. Keep
   XPscript names, source spans and diagnostics in the bound model.
3. Emit through the shared bound emitters and existing runtime APIs. Generated
   C# is an implementation detail and must not contain XPscript parsing logic.
4. Add a focused executable fixture for successful behavior and a focused
   unsupported-feature fixture when lowering is intentionally deferred.
5. Run the fixture through AST CLI and MCP validation when the feature is
   exposed there. Compare diagnostics by code, source file, span and meaning.
6. Preserve third-party notice generation and existing target/runtime
   packaging; an object migration must not introduce a new NuGet dependency
   unless the dependency is explicitly required and reviewed.

The minimum hand-off is a normal compiler build, the focused AST emission or
execution probe, and the MCP protocol probe when the machine interface is
affected.
