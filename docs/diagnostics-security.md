# Diagnostic security policy

XPScript diagnostics must remain useful without becoming a secret or source-payload disclosure channel.

## Allowed diagnostic metadata

Compiler diagnostics may include semantic identifiers when they are required to locate or understand an error. Examples include variable names, function names, parameter names, member names and declared type names. These identifiers are source metadata and are intentionally retained where they materially improve troubleshooting.

## Values that must be redacted

Diagnostics must not expose source-controlled string literal contents, complete filesystem paths when a file name is sufficient, complete request URLs, HTTP header values, JSON payloads, compiler workspace paths, raw generated C# source context, raw COM/native loader exception details, or generic unexpected exception messages.

Source-code lines attached to structured diagnostics preserve their layout but mask characters inside string literals through `CompilerDiagnosticRedaction.MaskStringLiterals` or an equivalent bounded redaction step.

Generated-build diagnostics replace invocation-local workspace paths with `<compiler-workspace>` and reduce recognized source paths to their file name. Generic unexpected compiler exceptions are exposed as `Compilation failed.` rather than the original exception text.

## Runtime APIs

Runtime APIs use API-specific bounded messages when the sensitive data shape differs by subsystem. Evaluate, HTTP, JSON, Shell, COM, File I/O and native interop already apply subsystem-specific redaction. A shared runtime redaction helper should only be introduced when multiple runtime APIs require the same transformation. Do not force unlike data types through a generic string scrubber because that can leave partial secrets or destroy useful diagnostics.

## Permanent verification

`Diagnostics Security Closeout` audits the common compiler redaction helper, compiler exception construction, generated-build sanitization policy and representative negative compiler samples on Windows, Ubuntu and macOS. Subsystem closeout workflows continue to verify Evaluate, HTTP, JSON, File I/O, Shell, COM and native interop runtime diagnostics.
