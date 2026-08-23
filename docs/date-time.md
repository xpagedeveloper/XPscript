# Date and time

This page documents XPScript date/time functions and the Date object extensions. Every example below can be copied into an `.xps` file and run with `xpscriptc run file.xps`. The complete regression is [`samples/date-object-enhancements.xps`](../samples/date-object-enhancements.xps).

## `Date()`

Returns the current local date.

**Parameters:** none.

```xpscript
Sub Main()
    Print CStr(Date())
End Sub
```

## `Now()`

Returns the current local date and time.

**Parameters:** none.

```xpscript
Sub Main()
    Print CStr(Now())
End Sub
```

## `DateNumber(year, month, day)`

Creates a Date value from numeric components.

**Parameters**

- `year`: four-digit year.
- `month`: month number.
- `day`: day of month.

```xpscript
Sub Main()
    Print CStr(DateNumber(2026, 8, 23))
End Sub
```

## `TimeNumber(hour, minute, second)`

Creates a time value from numeric components.

**Parameters:** `hour`, `minute`, `second`.

```xpscript
Sub Main()
    Print CStr(TimeNumber(12, 30, 0))
End Sub
```

## `DateAdd(interval, number, date)`

Adds an interval to a Date value.

**Parameters**

- `interval`: interval code such as `d` for days.
- `number`: signed amount to add.
- `date`: source Date value.

```xpscript
Sub Main()
    Print CStr(DateAdd("d", 5, #2026-08-23#))
End Sub
```

## `DateDiff(interval, date1, date2)`

Returns the difference between two dates using the requested interval.

**Parameters:** `interval`, `date1`, `date2`.

```xpscript
Sub Main()
    Print CStr(DateDiff("d", #2026-08-01#, #2026-08-23#))
End Sub
```

## `Year(date)`, `Month(date)` and `Day(date)`

Return the corresponding calendar component.

**Parameters:** `date`, the Date value to inspect.

```xpscript
Sub Main()
    Dim d As Date
    d = #2026-08-23#
    Print CStr(Year(d))
    Print CStr(Month(d))
    Print CStr(Day(d))
End Sub
```

## `Date.Adjust(years, months, days, hours, minutes, seconds)`

Returns a new Date adjusted by all six signed components. Month/year adjustment follows .NET calendar semantics, including leap years.

**Parameters:** signed integer values for `years`, `months`, `days`, `hours`, `minutes` and `seconds`.

```xpscript
Sub Main()
    Dim d As Date
    d = #2026-08-23 10:00:00#
    d = d.Adjust(0, 1, -2, 3, 0, 0)
    Print CStr(d)
End Sub
```

## `Date.Difference(otherDate)`

Returns signed total seconds as `otherDate - currentDate`.

**Parameters:** `otherDate`, the Date value to compare with.

```xpscript
Sub Main()
    Dim startDate As Date
    Dim endDate As Date
    startDate = #2026-08-23 10:00:00#
    endDate = #2026-08-23 10:01:30#
    Print CStr(startDate.Difference(endDate))
End Sub
```

## `Date.OSDateFormatting`

Returns the current OS/culture short-date formatting mask used by `Format`/`Format$` semantics.

```xpscript
Sub Main()
    Print Date.OSDateFormatting
End Sub
```

## `Date.OSTimeFormatting`

Returns the current OS/culture long-time formatting mask.

```xpscript
Sub Main()
    Print Date.OSTimeFormatting
End Sub
```

## Date comparisons

Date values support the normal comparison operators. Known Date values are compared as complete date/time values.

```xpscript
Sub Main()
    Dim firstDate As Date
    Dim secondDate As Date
    firstDate = #2026-08-23#
    secondDate = #2026-08-24#
    Print CStr(firstDate < secondDate)
End Sub
```
