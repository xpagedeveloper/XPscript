# LotusScript language reference completeness

Source inventory is based on the HCL Domino Designer LotusScript Language Reference A-Z pages supplied for Domino Designer 14.0.0.

This file is a temporary tracking document for implementing missing portable language statements, directives, data types and built-in functions in XPScript. Product-specific Notes/Domino classes are out of scope. Platform-specific language features must either have a safe platform implementation or fail with a clear runtime/compiler error on unsupported platforms.

Status legend:

- implemented: supported by XPScript with regression coverage
- partial: supported with documented platform/semantic differences
- missing: not yet implemented
- not-portable: HCL feature is inherently tied to unavailable platform technology such as OLE/LSX and needs explicit compatibility behavior

Implementation groups:

1. Core syntax, declarations, control flow and directives
2. Scalar conversion, inspection, math and date/time
3. String, array and list functions
4. File and directory I/O
5. Error handling and dynamic execution
6. Process, environment and user interaction
7. Platform-specific OLE, locks, LSX and host integration

The repository tests and docs/commands.md are the source of truth for XPScript support. This checklist will be expanded as the HCL A-Z inventory is normalized and compared against the implementation.
