# XPScript Date and Time

This page documents Date functionality demonstrated by `samples/date-object-enhancements.xps`, `samples/date-comparisons-valid.xps`, `samples/date-comparisons-invalid.xps`, `samples/reference-runtime-batch1.xps` and Evaluate date samples.

## Date values

```xpscript
Dim value As Date
```

Dates are represented as full date/time values.

## CDate / CDat / CVDate

Convert compatible input to Date.

```xpscript
Dim d As Date
d = CDate("2026-08-12")
```

`CDat` and `CVDate` are supported conversion aliases in the runtime surface where documented by the samples.

## DateNumber

Creates a Date from year, month and day.

```xpscript
startDate = DateNumber(2026, 1, 31)
```

## TimeNumber

Creates a time value from hour, minute and second.

```xpscript
t = TimeNumber(14, 30, 0)
```

## DateValue and TimeValue

`DateValue` extracts/converts the date portion of a value. `TimeValue` extracts/converts the time portion.

## Year, Month, Day

```xpscript
Print CStr(Year(d))
Print CStr(Month(d))
Print CStr(Day(d))
```

## Hour, Minute, Second

```xpscript
Print CStr(Hour(d))
Print CStr(Minute(d))
Print CStr(Second(d))
```

## Date.Adjust

Returns a new Date adjusted by the supplied components.

```xpscript
adjusted = original.Adjust(years, months, days, hours, minutes, seconds)
```

Example:

```xpscript
adjustedDate = startDate.Adjust(0, 1, 0, 0, 0, 0)
endDate = adjustedDate.Adjust(0, 0, 1, 2, 30, 15)
```

The operation is non-mutating: the original Date value is not modified. Month and year changes use calendar-aware DateTime semantics.

## Date.Difference

Returns signed total seconds between two dates using `otherDate - currentDate` semantics.

```xpscript
seconds = adjustedDate.Difference(endDate)
```

## DateAdd

Adds a Date interval.

```xpscript
result = DateAdd(interval, amount, dateValue)
```

## DateDiff

Returns the difference between two dates for the requested interval.

```xpscript
result = DateDiff(interval, firstDate, secondDate)
```

## DatePart

Returns a requested date/time component using an interval identifier.

```xpscript
result = DatePart(interval, dateValue)
```

## Date comparisons

Date values support:

- `=`
- `<>`
- `<`
- `<=`
- `>`
- `>=`

```xpscript
If endDate > startDate Then
    Print "later"
End If
```

Statically known nonsensical comparisons such as Date against Boolean, Object, arrays, custom Class instances or custom Type values are rejected by compiler diagnostics. Date/String, Date/numeric and Date/Variant paths remain available where conversion is meaningful.

## IsDate

Returns True when the supplied value can be treated as a Date.

## Samples

- `samples/date-object-enhancements.xps`
- `samples/date-comparisons-valid.xps`
- `samples/date-comparisons-invalid.xps`
- `samples/reference-runtime-batch1.xps`
- `samples/evaluate-standard-functions.xps`
- `samples/evaluate-coercion-diagnostics.xps`
