# Multiline string literals

XPScript supports alternate string delimiters with vertical bars and braces. Both forms may span multiple physical source lines.

```xpscript
Dim html As String
html = |<div>
  <strong>hello</strong>
</div>|
```

Brace form:

```xpscript
Dim text As String
text = {first line
second "quoted" line
third line}
```

The opening and closing delimiters are not part of the resulting string. Physical line breaks inside the literal are preserved as characters in the resulting string.

Double quotes do not need escaping inside `|...|` or `{...}` strings.

To include a literal pipe inside a pipe-delimited string, double the closing delimiter:

```xpscript
value = |left || right|
```

This produces `left | right`.

To include a literal closing brace inside a brace-delimited string, double it:

```xpscript
value = {left }} right}
```

This produces `left } right`.

Single-line forms continue to work:

```xpscript
value1 = |text with "quotes"|
value2 = {text with "quotes"}
```

An unterminated alternate string is a compile error. The diagnostic points to the source line containing the opening delimiter.
