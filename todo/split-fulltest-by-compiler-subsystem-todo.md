# Split FullTest by compiler subsystem

## Goal

Split the monolithic language FullTest into smaller, focused test suites based on the compiler/runtime subsystem they exercise. The suites should run independently and, where possible, in parallel in GitHub Actions.

## Test rule

- [ ] Preserve the repository rule that a newly discovered failure gets a small focused regression test.
- [ ] Put that focused regression test first in the relevant subsystem suite.
- [ ] If an existing failing test is already small and focused, move it first instead of duplicating it.
- [ ] Do not let a failure in one subsystem prevent unrelated subsystem suites from running.

## Proposed suites

### Parser / Syntax

- [ ] Create a Parser/Syntax suite.
- [ ] Move tests for parsing, declarations, statements, If/Else, loops, Select and general syntax into it.
- [ ] Include parser-specific diagnostics where appropriate.

### Functions / Parameter Passing

- [ ] Create a Functions/Parameter Passing suite.
- [ ] Move Function/Sub tests into it.
- [ ] Move ByRef/ByVal tests into it.
- [ ] Move return-value and nested-call tests into it.
- [ ] Move the focused `Not Probe(..., Array(...))` default-ByRef regression into this suite and keep it first while it represents the latest failure.

### Operators / Expressions

- [ ] Create an Operators/Expressions suite.
- [ ] Move Not, And, Or, Xor and related logical-operator tests into it.
- [ ] Move comparison and arithmetic expression tests into it.
- [ ] Move operator/array compatibility tests into it.

### Types / Variables

- [ ] Create a Types/Variables suite.
- [ ] Move Variant tests into it.
- [ ] Move array, object, conversion and scope tests into it.

### Compiler Diagnostics

- [ ] Create a Compiler Diagnostics suite.
- [ ] Move expected compiler-error tests into it.
- [ ] Verify diagnostic codes, source locations and messages.
- [ ] Include focused coverage for diagnostics such as XPS2014.
- [ ] Keep machine compiler/MCP diagnostic behavior synchronized with normal compiler diagnostics.

### Runtime

- [ ] Create a Runtime suite.
- [ ] Move runtime-error and runtime-helper tests into it.
- [ ] Test normalized runtime errors without exposing managed C# stack traces by default.
- [ ] Test full diagnostic/stack information when debug mode is enabled.

### Standard Library

- [ ] Create a Standard Library suite.
- [ ] Move string, date, math, file and other standard-library language tests into it.
- [ ] Split this suite further later if it becomes too large.

## GitHub Actions

- [ ] Update FullTest workflow configuration so subsystem suites run as separate jobs.
- [ ] Run independent suites in parallel where possible.
- [ ] Give each job a clear subsystem-specific name.
- [ ] Preserve useful logs and failure output for each suite.
- [ ] Ensure one failed suite does not cancel unrelated suites.
- [ ] Keep platform-specific tests separate from language/compiler subsystem tests where appropriate.

## Test runner cleanup

- [ ] Refactor `.github/scripts/run-fulltests.ps1` so each subsystem can be selected directly.
- [ ] Remove duplicated setup logic between suites.
- [ ] Keep common compile/run helpers shared.
- [ ] Rename existing regression variables/messages whose names no longer match what they test (for example the current default-ByRef Not regression still using `byval` naming).
- [ ] Document where a new regression test should be placed.

## Verification

- [ ] Verify every existing language FullTest test is assigned to exactly one appropriate suite, unless intentional cross-suite coverage is documented.
- [ ] Verify all new suites pass independently.
- [ ] Verify all suites can run together in CI.
- [ ] Compare test coverage/count before and after the split so no existing tests disappear accidentally.
- [ ] Verify a deliberately failing focused test fails only its relevant suite while unrelated suites continue.
- [ ] Update repository testing documentation/knowledge with the final suite structure.
