# Include diagnostic source mapping

When XPScript expands `Include` files, diagnostics are mapped back to the physical source file that contributed each expanded line.

A diagnostic inside an included file therefore uses:

- the included `.xps` file name,
- a line number counted from line 1 of that included file,
- the position reported for that physical source line.

The flattened compilation-unit line is not exposed as the source line for these diagnostics.

This behavior applies to normal compilation and direct `xpscriptc run` compilation, including text, JSON and XML result formats.
