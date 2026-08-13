# XPScript escaped quote regression TODO

(c) xpagedeveloper.com 2026

Discovered by cross-platform runtime verification while compiling documented shell examples unchanged on Windows, Linux and macOS.

## Regression

- [x] support documented backslash-escaped quote form `\"` inside an XPScript string literal in addition to doubled-quote form `""`.
  - Reproducer: `Call Shell("/bin/sh -c \"echo XPScript-Linux\"")`.
  - Previous behavior: the quote after the backslash could terminate the XPScript string and later generated code failed with syntax diagnostics.
  - Expected behavior: `\"` represents a literal `"` character in the XPScript string and does not terminate the source string.
  - Normalize this form before validators and source preprocessors inspect string boundaries.
  - [x] permanent regression verifies the runtime string value contains the quote without the escape backslash; source: `samples/escaped-quotes.xps`.
  - [x] compiler diagnostic redaction treats `\"` as part of the string and does not leak string contents; source: `samples/escaped-quote-diagnostic-error.xps`.
  - Verified by the permanent Escaped Quote Regression compatibility gate, Application Runtime Compatibility, and full .NET 10 Build.
