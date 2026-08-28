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

## Implementation rule

For Notes/Domino compatibility work:

1. Verify that the requested LotusScript member exists in HCL documentation.
2. Verify that its behavior can be mapped completely to documented Domino C API functionality.
3. Use Domino JNX as a secondary implementation reference where useful.
4. Implement the member only when XPscript can provide the documented behavior without silently dropping arguments, changing semantics, or emulating unsupported behavior.
5. If full compatibility cannot be verified, leave the member unimplemented rather than exposing a partial implementation.
