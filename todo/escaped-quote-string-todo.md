# XPScript escaped quote regression TODO

(c) xpagedeveloper.com 2026

Discovered by cross-platform runtime verification while compiling documented shell examples unchanged on Windows, Linux and macOS.

## Regression

- [x] support documented backslash-escaped quote form `\"` inside an XPScript string literal in addition to doubled-quote form `""`.
  - Reproducer: `Call Shell("/bin/sh -c \"echo XPScript-Linux\"")`.
  - Previous behavior: the quote after the backslash could terminate the XPScript string and later generated code failed with syntax diagnostics.
  - Expected behavior: `\"` represents a literal `"` character in the XPScript string and does not terminate the source string.
  - Normalize this form before validators and source preprocessors inspect string boundaries.
  - [>] permanent regression is embedded in `samples/compatibility.xps`, which compares `A\"B` with the existing doubled-quote form `A""B` and raises an error on mismatch. This sample is executed by the standard `.NET 10 Build` workflow.
  - [ ] security review: ensure compiler diagnostic redaction also treats `\"` as part of a protected string literal; keep this under the broader compiler/security review rather than marking it verified without a permanent security gate.
  - Mark the language behavior `[x]` only after the standard `.NET 10 Build` passes on the final PR head.
