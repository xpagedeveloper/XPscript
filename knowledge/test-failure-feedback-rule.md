# Test Failure Feedback Rule

This rule applies to all automated tests and CI test suites in the repository.

## Rule

When a test fails, the failing behavior must be exercised as early as practical in the next test run.

- If the failing test is small and fast, move or schedule that test earlier in the relevant test sequence for the next run.
- If the failing test is large, slow, or exercises many unrelated behaviors, create a small focused regression test that reproduces the specific failure. Run that focused test early, before the larger suite.
- Keep the original broader test in place. A focused regression test does not replace full-suite verification.
- A fix is not considered fully verified until the focused or reordered test passes and the relevant complete test suite also passes.
- The focused regression test should remain in the repository when it provides useful permanent coverage against recurrence.
- Prefer the smallest deterministic reproduction of the failure. Avoid duplicating unrelated setup or assertions from a large test.
- When multiple failures are being investigated, prioritize early tests so that known failing behavior is reached before expensive unrelated coverage whenever practical.

## Purpose

The purpose of this rule is to shorten the feedback loop after a failure without reducing test coverage. A known failure should not require waiting through a long test suite before discovering whether the next fix addressed it.
