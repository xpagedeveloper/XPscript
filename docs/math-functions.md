# XPScript Math Functions

> For compact command syntax, parameters and examples, see the [Command Reference](command-reference.md).


## Abs

Returns the absolute value.

```xpscript
Print CStr(Abs(-4))
```

## Int

Returns the floor of a number.

```xpscript
Print CStr(Int(3.9))
```

## Fix

Truncates the fractional portion toward zero.

```xpscript
Print CStr(Fix(-3.9))
```

## Round

Rounds using the runtime's midpoint-to-even semantics.

```xpscript
Print CStr(Round(12.345, 2))
```

The digits argument is optional.

## Sqr

Returns the square root.

```xpscript
Print CStr(Sqr(81))
```

## Sgn

Returns the sign of a number:

- negative → `-1`
- zero → `0`
- positive → `1`

```xpscript
Print CStr(Sgn(-4))
```

## Sin

Returns the sine of a value in radians.

## Cos

Returns the cosine of a value in radians.

## Tan

Returns the tangent of a value in radians.

## ATn

Returns the arctangent in radians.

## ATn2

Returns the angle derived from Y and X coordinates.

```xpscript
angle = ATn2(y, x)
```

## ASin

Returns the arcsine.

## ACos

Returns the arccosine.

## Exp

Returns `e` raised to the supplied power.

## Log

Returns the natural logarithm.

## Fraction

Returns the fractional part after truncation.

```xpscript
Print CStr(Fraction(12.75))
```

## Rnd

Returns a pseudo-random Double.

```xpscript
value = Rnd()
```

An overload accepting a numeric argument is available in the runtime compatibility surface.

## Randomize

Resets the pseudo-random generator.

```xpscript
Randomize
```

A seed may be supplied where deterministic initialization is required.

```xpscript
Randomize 1234
```

## Numeric operators

The samples also demonstrate:

- `+`
- `-`
- `*`
- `/`
- `\` integer division
- `Mod`
- `^` exponentiation

For logical/bitwise operators see [docs/arrays-lists-operators.md](arrays-lists-operators.md).

## Conversion errors

Math functions use normal XPScript numeric coercion. Values that cannot be converted should be handled with normal `On Error` logic; Evaluate additionally sanitizes diagnostics before returning them to the caller.

## Samples

- [samples/compatibility.xps](../samples/compatibility.xps)
- [samples/evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps)
- [samples/evaluate-coercion-diagnostics.xps](../samples/evaluate-coercion-diagnostics.xps)
- arithmetic sections in [samples/operators-arrays.xps](../samples/operators-arrays.xps)
