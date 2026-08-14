# Include source-map regression fixture

`root.xps` deliberately contains padding before including `lib/compile-error.xps`.

The included file contains a deterministic type error on its own physical line 5. The regression requires the compiler to report `compile-error.xps` line 5 rather than the later line occupied by that statement in the flattened compilation unit.
