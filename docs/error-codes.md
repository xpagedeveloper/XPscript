# XPScript Error Codes

(c) xpagedeveloper.com 2026

This page is the authoritative reference for numeric XPScript runtime error codes exposed through `Err`, `Error`, `Error$` and `On Error` handling. Compiler diagnostics are separate: compiler failures are reported with source file, line, position, description and marked source rather than a numeric runtime `Err` code.

## Runtime error codes

| Code | Name | Meaning |
|---:|---|---|
| 5 | Invalid procedure call | A call, argument combination, operation or runtime request is invalid for the current context. `Evaluate` also uses error 5 for safe generic parser/API failures and resource-budget violations that do not map to a more specific public error. |
| 6 | Overflow | A numeric conversion or arithmetic result cannot be represented by the target XPScript type. Example: converting `300` with `CByte`. |
| 9 | Subscript out of range | An array, List, collection or indexed value is accessed with an invalid index, dimension, key or range. |
| 11 | Division by zero | Integer or numeric division is attempted with a zero divisor. |
| 13 | Type mismatch | A value cannot be converted, assigned or used as the required type. `Evaluate` conversion failures use this code as well. |
| 53 | File not found | A requested file or filesystem source does not exist where the operation requires an existing file. |
| 55 | File already open | A file number or file state conflicts with an already-open XPScript file handle. |
| 62 | Input past end of file | A file input operation requests data after the readable end of the file. |
| 70 | Permission/access denied | An operation is blocked by permissions, access policy, file-sharing/locking rules or another protected-resource boundary. Cross-process `Lock`/`Unlock` conflicts use this code. |

These names follow the runtime's public compatibility semantics. Some operations throw a more specific description while retaining the same numeric code, especially error 5 and error 70.

## Error handling

`Err` contains the numeric runtime error code captured by the active XPScript error context. `Error$` returns the current description, while `Error(number)` returns the standard description where the runtime defines one. User code may also raise an error explicitly with `Error number, description`.

Typical handling:

```xpscript
On Error GoTo Handler

Dim value As Byte
value = CByte(300)
Exit Sub

Handler:
Print "ERR=" & CStr(Err)
Print "DESCRIPTION=" & Error$
```

The overflow example above produces error `6`.

## Evaluate mappings

The isolated `Evaluate` runtime normalizes important failures to normal XPScript error semantics rather than exposing raw CLR exceptions. Existing permanent regression coverage includes:

- error 13 for type/conversion mismatch in [evaluate-coercion-diagnostics.xps](../samples/evaluate-coercion-diagnostics.xps)
- error 11 for division by zero in [evaluate-coercion-diagnostics.xps](../samples/evaluate-coercion-diagnostics.xps)
- error 6 for overflow in [evaluate-coercion-diagnostics.xps](../samples/evaluate-coercion-diagnostics.xps)
- sanitized Evaluate diagnostics in [evaluate-diagnostic-sanitization.xps](../samples/evaluate-diagnostic-sanitization.xps)

Parser/API failures that have no more specific public mapping use controlled error 5 diagnostics. Permission/access failures are normalized to error 70 where applicable.

## File and locking mappings

File APIs use the public file-oriented codes above where the runtime has a defined compatibility mapping. Cross-platform file-lock contention is represented as error 70 rather than leaking an operating-system-specific errno/HResult. See [File I/O and filesystem](file-io-filesystem.md) for platform behavior.

## Compiler diagnostics are not runtime error codes

Compile-time failures are not assigned one of the runtime `Err` numbers merely to make them numeric. The compiler instead returns structured diagnostics containing the source file, physical line, position, description and marked code. This keeps syntax/type/parameter diagnostics precise and avoids inventing runtime error numbers for compile-time conditions.

When compiler output is requested as JSON or XML, those same structured diagnostics are serialized in the requested result format.

## Source of truth

The public runtime mapping is defined by `XPScriptErrorRuntime` in `src/XPScript.Compiler/CoreCompatibilityRuntimeSource.cs`. Additional runtime components may raise one of the public codes with a context-specific description; notably file/runtime permission paths use error 70. This document should be updated whenever a new numeric XPScript runtime error becomes user-visible.
