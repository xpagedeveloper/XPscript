# XPScript Strings, Conversion, Inspection and Base64

This page documents functions demonstrated by `samples/reference-runtime-batch1.xps`, `samples/coercion.xps`, `samples/compatibility.xps`, `samples/base64-binary.xps`, `samples/language-extensions.xps` and the Evaluate function samples.

## Conversion functions

### CStr

Converts a value to String using XPScript runtime conversion semantics.

```xpscript
text = CStr(value)
```

### CByte, CInt, CLng, CSng, CDbl, CCur, CBool

Convert values to the corresponding scalar type.

```xpscript
number = CInt("42")
```

### CDate / CDat / CVDate

Convert compatible values to Date.

### CVar

Returns the value as Variant.

### CType

Converts using a runtime type name.

```xpscript
value = CType("42", "Integer")
```

## Inspection functions

### TypeName

Returns an XPScript type name.

### DataType

Returns the runtime data-type code.

### IsArray

Returns True for array values.

### IsDate

Checks Date compatibility.

### IsNull / IsEmpty

Check Null/Empty state according to the current XPScript runtime model.

### IsNumeric

Checks numeric convertibility.

### IsObject

Checks object/reference-style values.

### IsScalar

Checks scalar values.

### IsList

Checks XPScript List values.

### IsUnknown

Checks unknown/empty Variant state as demonstrated by the reference-runtime sample.

## Core String functions

### Len / LenB

Returns character or byte-oriented length.

### Left / Right / Mid

Extract String ranges.

```xpscript
Left("abcdef", 3)
Right("abcdef", 3)
Mid("abcdef", 2, 3)
```

### LeftB / RightB / MidB / InstrB

Byte-oriented compatibility functions demonstrated by `reference-runtime-batch1.xps`.

### Instr

Finds a substring.

### Replace

Replaces matching text with optional start/count/comparison arguments.

### LCase / UCase

Change case using current-culture semantics.

### Trim / LTrim / RTrim / FullTrim

Whitespace helpers.

### StrReverse

Reverses a String.

### Space

Returns a String containing a requested number of spaces.

### String

Repeats a character.

### Chr / UChr

Convert numeric character codes to characters. `UChr` is the Unicode-oriented form demonstrated by the reference sample.

### Asc / Uni

Return character code values. `Uni` is demonstrated for Unicode character inspection.

## Token/delimiter helpers

### StrLeft

Returns text to the left of the first delimiter.

```xpscript
StrLeft("one/two/three", "/")
```

### StrLeftBack

Returns text to the left of the last delimiter.

### StrRight

Returns text to the right of the first delimiter.

### StrRightBack

Returns text to the right of the last delimiter.

### StrToken

Returns a token by delimiter and index.

```xpscript
StrToken("one/two/three", "/", 2)
```

## LSet / RSet

Pads or fits text to a requested width using left/right alignment.

```xpscript
LSet("abc", 5)
RSet("abc", 5)
```

## StrConv

Performs supported String conversions such as case transformations.

```xpscript
StrConv("hello world", 1)
```

## Numeric String helpers

### Val

Parses the leading numeric portion of a String using invariant-style numeric syntax.

### Str

Formats a number using compatibility String semantics.

### Bin / Hex / Oct

Convert integers to binary, hexadecimal or octal String representations.

## Base64

### Base64Encode

Encodes text using an explicit charset.

```xpscript
encoded = Base64Encode("Fredrik", "utf-8")
```

### Base64Decode

Decodes Base64 to text using an explicit charset.

```xpscript
decoded = Base64Decode("RnJlZHJpaw==", "utf-8")
```

### Base64DecodeBinary

Returns decoded bytes as an XPScript Byte array rather than converting them to text.

```xpscript
bytes = Base64DecodeBinary(encoded)
```

### ToBase64 / FromBase64

Compatibility helpers for Base64 text conversion.

## URL encoding

### UrlEncode / UrlDecode

Encode/decode URL-safe text.

## Coercion and `+`

XPScript's forgiving dynamic `+` can perform numeric addition or String concatenation depending on operand types and numeric String conversion. Use `&` when explicit concatenation is intended.

## Samples

- `samples/reference-runtime-batch1.xps`
- `samples/coercion.xps`
- `samples/compatibility.xps`
- `samples/base64-binary.xps`
- `samples/language-extensions.xps`
- `samples/evaluate-standard-functions.xps`
- `samples/evaluate-coercion-diagnostics.xps`
