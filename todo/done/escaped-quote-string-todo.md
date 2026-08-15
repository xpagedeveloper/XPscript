# XPScript escaped quote regression TODO

(c) xpagedeveloper.com 2026

Discovered by cross-platform runtime verification while compiling documented shell examples unchanged on Windows, Linux and macOS.

## Regression

- [x] support documented backslash-escaped quote form `\"` inside an XPScript string literal in addition to doubled-quote form `""`.
  - Reproducer: `Call Shell("/bin/sh -c \"echo XPScript-Linux\"")`.
  - Previous behavior: the quote after the backslash could terminate the XPScript string and later generated code failed with syntax diagnostics.
  - Expected behavior: `\"` represents a literal `"` character in the XPScript string and does not terminate the source string.
  - Normalize this form before validators and source preprocessors inspect string boundaries.
  - [x] permanent regression is embedded in `samples/compatibility.xps`, which compares `A\"B` with the existing doubled-quote form `A""B` and raises an error on mismatch. This sample is executed by the standard `.NET 10 Build` workflow.
  - [x] security review: compiler diagnostic redaction treats `\"` as part of a protected string literal. `samples/escaped-quote-diagnostic-error.xps` places `TOPSECRET\"STILLSECRET` inside the protected literal and the `Escaped Quote Regression` workflow verifies neither secret fragment appears in structured JSON compiler diagnostics while the expected type diagnostic remains present.
  - Language behavior and diagnostic redaction are both protected by permanent regression coverage.
