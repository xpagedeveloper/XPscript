# Domino native API knowledge

This file records authoritative and implementation-reference sources for XPscript Notes/Domino compatibility work.

## HCL Domino C API documentation

Primary source for native Domino API signatures, constants, structures, flags, ownership/lifecycle rules, and documented behavior:

- [HCL Domino C API Documentation](https://opensource.hcltechsw.com/domino-c-api-docs/)

Use the C API documentation as the primary authority before adding or changing native Notes/Domino interop. Do not infer signatures, flags, structure layouts, ownership semantics, or behavior when the C API documentation can verify them.

## HCL Domino JNX

HCL's Java/JNI implementation over the Domino native API, useful as an implementation reference for how HCL maps higher-level Notes/Domino behavior onto the native C API:

- [HCL Domino JNX](https://github.com/HCL-TECH-SOFTWARE/domino-jnx)

JNX is a reference implementation, not a substitute for the C API contract. When implementing XPscript Notes classes, use it to cross-check native calls, flag combinations, memory/resource handling, data conversion, and higher-level behavior. If JNX and assumptions in XPscript differ, verify the behavior against the C API documentation and the relevant HCL LotusScript/Designer documentation before implementing.

## NotesDocument.ComputeWithForm

XPscript implements the normal LotusScript-compatible two-argument form and adds an XPscript-specific third ByRef Variant parameter:

```vb
Dim failedFields As Variant
ok = doc.ComputeWithForm(False, True, failedFields)
```

The third parameter is not part of the standard LotusScript `NotesDocument.ComputeWithForm` signature. It is an XPscript extension for retrieving field names reported by the native validation callback.

Semantics:

- If the third argument is omitted, XPscript creates no output slot and performs no output write.
- If `raiseError` is `False`, a supplied third argument is set to `Nothing` even if native validation reports errors.
- If `raiseError` is `True` and no field errors are reported, the third argument is `Nothing`.
- If `raiseError` is `True` and field errors are reported, the third argument is a Variant array containing the unique failing field names.
- The array is assigned before the validation error is raised so LotusScript-style error handling can inspect it.
- Validation failures raised by `ComputeWithForm` use XPscript runtime error 5. Native callback statuses such as `ERR_VALIDATION` are not exposed as the XPscript `Err` value.

The ordinary two-argument overload calls `NSFNoteComputeWithForm` without a callback. The XPscript-specific third-argument overload installs a `CWF_ERROR_PROC` callback to collect failed field names. Do not pass `CWF_CONTINUE_ON_ERROR` when field-name collection is required because HCL documents that this flag suppresses callback processing. The callback returns `CWF_NEXT_FIELD` after recording an error so validation continues and all failing field names can be collected.

### CDFIELD field-name layout verification

The callback receives `pCDField`, documented by HCL as a pointer to the form field's `CDFIELD` record. HCL's current C API 14.5 reference defines the fixed members in this order:

`WSIG`, `Flags`, `DataType`, `ListDelim`, `NFMT`, `TFMT`, `FONTID`, `DVLength`, `ITLength`, `TabOrder`, `IVLength`, `NameLength`, `DescLength`, `TextValueLength`.

For the documented ODS layout this places:

- `DVLength` at byte offset 22
- `ITLength` at byte offset 24
- `IVLength` at byte offset 28
- `NameLength` at byte offset 30
- the variable portion after a 36-byte fixed `CDFIELD` record

The XPscript constants in `NotesNativeApiComputeWithFormSource` match those offsets. The field-name pointer is therefore:

`CDFIELD fixed length + DVLength + ITLength + IVLength`

This is also the exact algorithm used in HCL's documented READFORM sample, which calculates the name using `ODSLength(_CDFIELD) + DVLength + ITLength + IVLength` and copies `NameLength` bytes.

These offsets are verified against the current HCL C API 14.5 contract and the longstanding HCL READFORM ODS example. XPscript currently does not publish a Domino release support matrix, so this verification must not be interpreted as an unsupported claim that every historical Domino release is covered. If a release-specific support matrix is added later, validate `_CDFIELD`/`ODSLength(_CDFIELD)` against the toolkit headers for every listed release.

Authoritative references:

- [CDFIELD](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Data/CDFIELD/)
- [CWF_ERROR_PROC](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Data/CWF_ERROR_PROC/)
- [NSFNoteComputeWithForm](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Func/NSFNoteComputeWithForm/)
- [Forms and Frames / READFORM](https://opensource.hcltechsw.com/domino-c-api-docs/howto/user_guide/Forms_and_Frames/)

## Implementation rule

For Notes/Domino compatibility work:

1. Verify that the requested LotusScript member exists in HCL documentation.
2. Verify that its behavior can be mapped completely to documented Domino C API functionality.
3. Use Domino JNX as a secondary implementation reference where useful.
4. Implement the member only when XPscript can provide the documented behavior without silently dropping arguments, changing semantics, or emulating unsupported behavior.
5. If full compatibility cannot be verified, leave the member unimplemented rather than exposing a partial implementation.
