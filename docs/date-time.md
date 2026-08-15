# XPScript Date and Time

This page documents Date functionality demonstrated by the runtime regression samples and by reusable programs in `examples/`.

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

## OS date and time format properties

Every typed Date value exposes two read-only formatting properties:

- `OSDateFormatting` returns the current operating-system/user culture short-date format mask.
- `OSTimeFormatting` returns the current operating-system/user culture long-time format mask.

The returned strings use the same custom date/time formatting syntax accepted by XPScript `Format` and `Format$`, so they can be passed directly to those functions.

```xpscript
Dim value As Date
value = Now

Print value.OSDateFormatting
Print value.OSTimeFormatting
Print Format$(value, value.OSDateFormatting)
Print Format$(value, value.OSTimeFormatting)
```

For example, a Swedish environment may return a date mask similar to `yyyy-MM-dd`, while another locale can return a different mask. Programs should use the returned value rather than assuming a specific separator, field order, 12/24-hour clock or seconds layout.

These properties describe the culture visible to the running XPScript process. They are read-only and do not change the operating-system settings.

Reusable example: `examples/date-os-formatting.xps`.

## Date.Adjust

Returns a new Date adjusted by the supplied components.

```xpscript
adjusted = original.Adjust(years, months, days, hours, minutes, seconds)
```

All six arguments are integer component adjustments and may be positive, zero or negative. The operation is non-mutating: the original Date value is not modified. Year and month adjustments use calendar-aware semantics, so leap days and month lengths are handled by the runtime rather than by fixed-day arithmetic. Date-only adjustments preserve the time component unless a time component is explicitly adjusted.

Example:

```xpscript
Dim original As Date
Dim adjusted As Date

original = DateNumber(2024, 2, 29)
adjusted = original.Adjust(1, 1, 2, 3, 4, 5)
```

Reusable example: `examples/date-adjust.xps`.

## Date.Difference

Returns signed total seconds between two dates using `otherDate - currentDate` semantics.

```xpscript
seconds = startDate.Difference(endDate)
```

The sign therefore depends on call direction. If `endDate` is later than `startDate`, `startDate.Difference(endDate)` is positive and `endDate.Difference(startDate)` is negative.

Reusable example: `examples/date-difference.xps`.

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

Date values support `=`, `<>`, `<`, `<=`, `>` and `>=`.

```xpscript
If endDate > startDate Then
    Print "later"
End If
```

Comparisons use the full Date value, including time-of-day. Statically known nonsensical comparisons such as Date against Boolean, Object, arrays, custom Class instances or custom Type values are rejected by compiler diagnostics. Date/String, Date/numeric and Date/Variant paths remain available where conversion is meaningful.

Reusable example: `examples/date-comparisons.xps`.

## IsDate

Returns True when the supplied value can be treated as a Date.

## Reusable examples

The `examples/` programs are intended to be copied or adapted directly:

- `examples/date-adjust.xps` — calendar-aware Date adjustment without mutating the original value.
- `examples/date-difference.xps` — signed total-second differences in both directions.
- `examples/date-comparisons.xps` — all six Date comparison operators.
- `examples/date-os-formatting.xps` — OS/culture date and time masks used directly with `Format$`.

## Regression samples

The compiler/runtime regression suite also covers:

- [samples/date-object-enhancements.xps](../samples/date-object-enhancements.xps)
- [samples/date-comparisons-valid.xps](../samples/date-comparisons-valid.xps)
- [samples/date-comparisons-invalid.xps](../samples/date-comparisons-invalid.xps)
- [samples/reference-runtime-batch1.xps](../samples/reference-runtime-batch1.xps)
- [samples/evaluate-standard-functions.xps](../samples/evaluate-standard-functions.xps)
- [samples/evaluate-coercion-diagnostics.xps](../samples/evaluate-coercion-diagnostics.xps)
