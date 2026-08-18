# UIForm layout and reactive refresh

(c) xpagedeveloper.com 2026

## Goal

Provide one layout and reactive-update API for XPscript UIForm on desktop and web.

The same XPscript source should be able to run through Avalonia desktop, Kestrel, CGI and FastCGI wherever the underlying feature is supported.

## Layout API

Planned public API:

```xps
Call form.SetGridColumns(12)
Call form.SetFieldPosition("country", 1, 1, 6, 1)
Call form.SetFieldPosition("city", 1, 7, 6, 1)
Call form.SetFieldRegion("city", "cityRegion")
```

`SetGridColumns(columns)` configures a logical form grid. Default is 1 column for backwards compatibility.

`SetFieldPosition(name, row, column [, columnSpan [, rowSpan]])` positions a field in the logical grid.

Rules:

- rows and columns are 1-based
- column and row spans must be >= 1
- column + columnSpan - 1 may not exceed the configured grid column count
- omitted positioning keeps the current automatic vertical layout
- positioning is declarative, not pixel based
- desktop and web must use the same metadata

Web implementation should use CSS Grid.

Desktop implementation should use Avalonia Grid.

## Regions

A region is a named render target.

```xps
Call form.SetFieldRegion("city", "cityRegion")
Call form.SetFieldRegion("postalCode", "addressRegion")
```

A region may contain one or more fields.

Regions are used for partial refresh and may also be used for grouping/styling later.

Region IDs must be safe identifiers and unique inside one UIForm.

## Reactive updates

Planned API:

```xps
Call form.SetRefreshOnChange("country", "cityRegion", "RefreshCities")
```

Arguments:

1. source field name
2. target region ID
3. XPscript Sub/Function handler name

The default trigger is `change`.

Possible later triggers:

- input
- click
- blur
- submit

## Handler model

Example:

```xps
Sub RefreshCities()
    Dim country As String
    country = form.GetFieldValueString("country")

    Call form.ClearOptions("city")

    If country = "SE" Then
        Call form.AddOption("city", "Stockholm")
        Call form.AddOption("city", "Göteborg")
    ElseIf country = "NO" Then
        Call form.AddOption("city", "Oslo")
        Call form.AddOption("city", "Bergen")
    End If
End Sub
```

The handler must run after submitted/current values have been written to the bound JSON object.

The handler may modify:

- field values
- Select/Radio options
- visibility state when implemented
- enabled/read-only state when implemented
- other form metadata intended for rerender

## Web behaviour

For Kestrel, CGI and FastCGI:

1. rendered HTML gets stable region IDs
2. source fields with refresh rules get a small generated JavaScript listener
3. on change, current form values are POSTed to the same XPscript endpoint
4. request includes `__xps_uiform_partial=<regionId>` and source field metadata
5. XPscript applies posted values to the bound JSON object
6. configured XPscript handler runs
7. only target region HTML is returned
8. browser replaces only that region

The normal full form POST continues to work unchanged.

No external JavaScript framework should be required.

Security requirements:

- target region must be validated against regions registered by the form
- handler name must come from server-side form configuration, never directly from arbitrary client POST data
- normal CSRF/session protections used by the web host must continue to apply
- all returned labels, values and attributes remain encoded
- submitted values still pass server-side field validation

## Desktop behaviour

For Avalonia:

1. source control change event fires
2. current editor values are applied to the same bound JSON data
3. configured XPscript handler runs
4. target region is rebuilt from current form metadata
5. only the target Avalonia container is replaced/refreshed

The complete window must not be recreated for a normal reactive field update.

## Data refresh use cases

This must support:

- country -> city dependent select
- customer -> contact dependent select
- product -> current price/inventory display
- periodic/manual status refresh of one region
- fetching database/API information after a field changes
- refreshing one result area without rebuilding the full form

## Additional required API

Add:

```xps
Call form.ClearOptions("city")
Call form.RefreshRegion("cityRegion")
```

`ClearOptions` enables dependent lists to be rebuilt safely.

`RefreshRegion` allows explicit/manual refresh in addition to automatic change rules.

## Implementation order

1. shared layout metadata
2. web CSS Grid rendering
3. Avalonia Grid rendering
4. region metadata and stable IDs
5. ClearOptions
6. refresh rule metadata
7. safe XPscript handler invocation
8. web partial POST/HTML replacement
9. desktop change-event/region rebuild
10. tests and showcase scripts

## Tests

Add tests for:

- default one-column backwards-compatible layout
- grid bounds validation
- row/column/span metadata
- identical region metadata through desktop bridge JSON
- safe region identifiers
- unknown source fields rejected
- unknown target regions rejected
- unknown handlers rejected at compile/runtime boundary
- dependent Select options update
- only requested web region returned on partial request
- full web POST remains unchanged
- desktop region rebuild preserves unrelated control values
