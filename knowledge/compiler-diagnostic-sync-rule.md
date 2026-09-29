# Compiler diagnostic synchronization rule

The normal compiler interface and the machine compiler/MCP interface are two surfaces over the same compiler behavior. Their diagnostics and error semantics must remain synchronized.

Whenever a compiler error, validation error, diagnostic message, diagnostic code, error category, or failure behavior is added or changed in the normal compiler, review and update the machine compiler/MCP surface in the same change.

The reverse also applies. Whenever error handling, diagnostics, or failure semantics are changed for the machine compiler/MCP interface, review the normal compiler interface and make the corresponding change when applicable.

Do not consider a compiler diagnostic change complete until both interfaces have been checked.

Regression coverage must verify both surfaces when the behavior is exposed through both. MCP tests must preserve structured compiler diagnostics and clearly indicate failed tool calls without losing the diagnostic details.
