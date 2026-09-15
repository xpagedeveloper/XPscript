# XPSpreadsheet

`XPSpreadsheet` is XPScript's native API for creating and working with simple `.xlsx` workbooks.

> **Supported model:** XPSpreadsheet supports workbook data, multiple worksheets/tabs, formulas, ranges, AutoFilter, cell/range formatting, row heights, column widths, and AutoFit. Existing external `.xlsx` files can be opened and read, but are read-only by default so XPSpreadsheet does not accidentally remove spreadsheet features it does not understand.

The implementation uses the .NET runtime's built-in ZIP and XML support. It does not require Microsoft Excel, Office, LibreOffice, or an external spreadsheet NuGet package.

## Supported format

Only `.xlsx` is supported. Opening or saving `.xls`, `.xlsm`, `.ods`, `.csv`, or another spreadsheet format returns an explicit error.

## Safe update model

XPSpreadsheet marks every workbook it creates with the custom OOXML document property `XPScriptWorkbookVersion`. The current format writes `XPScriptWorkbookVersion=2`.

Version 2 adds the richer formatting, range, sizing, and filter model. The current runtime can still open and update version 1 XPScript workbooks; when they are saved they are upgraded to version 2. A workbook marked with a newer unsupported version is opened read-only rather than rewritten by an older runtime.

When a workbook is opened, XPSpreadsheet exposes:

- `CreatedByXPScript` - `True` when the XPScript workbook marker is present.
- `XPScriptFormatVersion` - the marker version, or `0` for an external/unmarked workbook.
- `CanUpdate` - `True` when the workbook was created by XPScript and its marker version is supported by the current runtime.

External `.xlsx` files are readable, but `Save()`, `SaveAs()`, and `ToBytes()` refuse to rewrite them. This prevents a supported-data round trip from silently removing charts, images, pivot tables, named ranges, external links, unsupported formatting, or other OOXML parts.

If you intentionally want to convert an external workbook to the XPSpreadsheet model, use `SaveAsSimple()`:

```xpscript
Dim book As New XPSpreadsheet("external.xlsx")

Print CStr(book.CreatedByXPScript)
Print CStr(book.CanUpdate)

book.SaveAsSimple("converted.xlsx")
```

## Create a workbook

```xpscript
Option Declare

Sub Main()
    Dim book As New XPSpreadsheet()
    Dim sheet As XPWorksheet

    Set sheet = book.AddWorksheet("Sales")
    sheet.Cell("A1").Value = "Customer"
    sheet.Cell("B1").Value = "Amount"
    sheet.Cell("A2").Value = "ACME AB"
    sheet.Cell("B2").Value = 12500.5

    book.SaveAs("sales.xlsx")
End Sub
```

If no worksheet has been added when a workbook is saved, XPSpreadsheet creates `Sheet1` automatically.

## Worksheets / tabs

Each worksheet corresponds to a workbook tab.

```xpscript
Dim book As New XPSpreadsheet()
Dim sales As XPWorksheet
Dim notes As XPWorksheet

Set sales = book.AddWorksheet("Sales")
Set notes = book.AddWorksheet("Notes")

Print CStr(book.WorksheetCount)
Print book.Worksheet(1).Name
Print book.Worksheet("Notes").Name

book.RenameWorksheet("Notes", "Comments")
book.RemoveWorksheet("Comments")
```

Worksheet names cannot be empty, cannot exceed 31 characters, cannot contain `: \\ / ? * [ ]`, and must be unique within the workbook.

## Cells

Cells can be addressed by A1 notation or by 1-based row and column numbers.

```xpscript
sheet.Cell("C4").Value = "Hello"
sheet.Cell(4, 3).Value = "Hello"
```

Basic values are strings, numbers, and Boolean values. `Text` returns a string representation of the current cell value.

```xpscript
Dim cell As XPCell
Set cell = sheet.Cell("A1")
Print cell.Address
Print cell.Text
cell.Clear()
```

`UsedRowCount` and `UsedColumnCount` report the largest populated or styled row and column indexes in the supported in-memory model.

## XPRange

Use `XPRange` when an operation should apply to several cells at once:

```xpscript
Dim area As XPRange
Set area = sheet.Range("A1:D20")

area.Bold = True
area.BackgroundColor = "#EAF2F8"
area.NumberFormat = "#,##0.00"
```

Ranges can be one-dimensional or multidimensional. All formatting and layout properties can be applied to rectangular multidimensional ranges such as `A1:G17`.

### Range Values

`Values` is intentionally different from the other range properties. It returns a zero-based one-dimensional XPScript array only when the range contains a single row or a single column.

```xpscript
Dim range As XPRange
Dim values As Variant

Set range = sheet.Range("A1:A10")
values = range.Values
Print CStr(LBound(values))
Print CStr(UBound(values))
Print CStr(values(0))

Set range = sheet.Range("A1:H1")
values = range.Values
```

Accessing `Values` on a multidimensional range such as `A1:G17` raises a normal XPScript runtime error. It can therefore be trapped with normal error handling:

```xpscript
Dim area As XPRange
Dim values As Variant

Set area = sheet.Range("A1:G17")

On Error Resume Next
values = area.Values
If Err <> 0 Then
    Print Error$
End If
On Error GoTo 0
```

The multidimensional restriction applies only to `Values`. For example, this is valid:

```xpscript
Dim area As XPRange
Set area = sheet.Range("A1:G17")

area.Bold = True
area.BackgroundColor = "#FFFFCC"
area.BorderBottom = "thin"
area.ColumnWidth = 14
area.RowHeight = 20
```

## Formatting

Formatting properties are available on both `XPCell` and `XPRange`.

### Font and fill

```xpscript
Dim area As XPRange
Set area = sheet.Range("A1:D1")

area.BackgroundColor = "#4472C4"
area.FontColor = "white"
area.FontName = "Arial"
area.FontSize = 12
area.Bold = True
area.Italic = False
```

Color properties accept:

- `#RRGGBB` or `RRGGBB` hex colors.
- 8-digit ARGB values, where the alpha prefix is ignored and the RGB part is used.
- the color names `black`, `white`, `red`, `green`, `blue`, `yellow`, `gray`/`grey`, `orange`, and `purple`.
- an empty string to remove the color.

### Number formats

```xpscript
sheet.Range("B2:D100").NumberFormat = "#,##0.00"
sheet.Cell("E2").NumberFormat = "0.00%"
```

XPSpreadsheet stores the supplied number-format string in the XLSX style table. Excel, LibreOffice, and compatible applications perform the display formatting.

### Alignment and wrapping

```xpscript
Dim header As XPRange
Set header = sheet.Range("A1:D1")

header.HorizontalAlignment = "center"
header.VerticalAlignment = "center"
header.WrapText = True
```

Horizontal alignment supports `general`, `left`, `center`, `right`, `fill`, `justify`, `centerContinuous`, and `distributed`.

Vertical alignment supports `top`, `center`, `bottom`, `justify`, and `distributed`.

### Borders

```xpscript
Dim area As XPRange
Set area = sheet.Range("A1:D20")

area.BorderTop = "thin"
area.BorderBottom = "thin"
area.BorderLeft = "thin"
area.BorderRight = "thin"
area.BorderColor = "#808080"
```

Supported border styles include `thin`, `medium`, `thick`, `dashed`, `dotted`, `double`, `hair`, `dashDot`, `mediumDashed`, `mediumDashDot`, and `slantDashDot`. Use an empty string or `none` to remove a border side.

## Row height, column width, and AutoFit

A range can set the width of every covered column and the height of every covered row:

```xpscript
Dim area As XPRange
Set area = sheet.Range("A1:D20")

area.ColumnWidth = 18
area.RowHeight = 22
```

`AutoFit()` estimates suitable widths and heights from the supported in-memory cell text:

```xpscript
area.AutoFit()
```

AutoFit is intentionally lightweight and does not contain a full font-rendering/layout engine, so its sizing is an approximation rather than pixel-identical Excel AutoFit behavior.

## AutoFilter

A worksheet can persist an OOXML AutoFilter range:

```xpscript
sheet.AutoFilter("A1:F500")
Print sheet.AutoFilterRange
```

Clear it with:

```xpscript
sheet.ClearAutoFilter()
```

`AutoFilterRange` is empty when no filter is active. XPSpreadsheet currently manages the worksheet AutoFilter range; advanced filter criteria are not yet part of the API.

## Formulas

Formula text is stored in the workbook and calculated by Excel, LibreOffice, or another compatible spreadsheet application.

```xpscript
sheet.Cell("A3").Formula = "=A1+A2"
sheet.Cell("A8").Formula = "=SUM(A1:A7)"
```

XPSpreadsheet does not include its own formula calculation engine.

## Read an XLSX file

```xpscript
Dim book As New XPSpreadsheet("sales.xlsx")
Dim sheet As XPWorksheet

Set sheet = book.Worksheet("Sales")
Print sheet.Cell("A2").Text
Print CStr(sheet.Cell("B2").Value)
```

The reader accepts normal XLSX string storage forms, including shared strings and inline strings. Reading does not require the workbook to have been created by XPScript.

## Read and write XLSX bytes

`ToBytes()` serializes an XPScript-created workbook to an in-memory byte array. `FromBytes()` loads XLSX data directly from a byte array or enumerable byte values without creating a temporary file.

```xpscript
Dim source As New XPSpreadsheet("sales.xlsx")
Dim data As Variant

data = source.ToBytes()

Dim copy As New XPSpreadsheet()
copy.FromBytes(data)

Print copy.Worksheet("Sales").Cell("A2").Text
```

`FromBytes()` uses the same validation and XPScript workbook marker rules as `Open()`.

## Main objects

### XPSpreadsheet

Properties:

- `Path`
- `WorksheetCount`
- `CreatedByXPScript`
- `XPScriptFormatVersion`
- `CanUpdate`

Methods:

- `AddWorksheet(name)`
- `Worksheet(nameOrIndex)`
- `RemoveWorksheet(nameOrIndex)`
- `RenameWorksheet(nameOrIndex, newName)`
- `Open(filename)`
- `FromBytes(bytes)`
- `Save()`
- `SaveAs(filename)`
- `SaveAsSimple(filename)`
- `ToBytes()`
- `Close()`

### XPWorksheet

Properties:

- `Name`
- `Index`
- `UsedRowCount`
- `UsedColumnCount`
- `AutoFilterRange`

Methods:

- `Cell(address)`
- `Cell(row, column)`
- `Range(address)`
- `AutoFilter(address)`
- `ClearAutoFilter()`
- `Clear()`

### XPRange

Properties:

- `Address`
- `RowCount`
- `ColumnCount`
- `Values` — only for one-dimensional ranges
- `BackgroundColor`
- `Bold`
- `Italic`
- `FontColor`
- `FontSize`
- `FontName`
- `NumberFormat`
- `HorizontalAlignment`
- `VerticalAlignment`
- `WrapText`
- `BorderTop`
- `BorderBottom`
- `BorderLeft`
- `BorderRight`
- `BorderColor`
- `ColumnWidth`
- `RowHeight`

Methods:

- `AutoFit()`
- `Clear()`

### XPCell

Properties:

- `Value`
- `Text`
- `Formula`
- `BackgroundColor`
- `Bold`
- `Italic`
- `FontColor`
- `FontSize`
- `FontName`
- `NumberFormat`
- `HorizontalAlignment`
- `VerticalAlignment`
- `WrapText`
- `BorderTop`
- `BorderBottom`
- `BorderLeft`
- `BorderRight`
- `BorderColor`
- `Address`
- `Row`
- `Column`

Methods:

- `Clear()`

## Demos

Spreadsheet demos are under `demo/spreadsheet/`:

- `xpspreadsheet-basic.xps`
- `xpspreadsheet-worksheets.xps`
- `xpspreadsheet-styles.xps`
- `xpspreadsheet-ranges.xps`
- `xpspreadsheet-formatting.xps`
- `xpspreadsheet-autofilter.xps`
- `xpspreadsheet-invalid-format.xps`

## Current limitations

XPSpreadsheet still does not provide a full spreadsheet application or calculation engine. It does not preserve or edit charts, images, macros/VBA, pivot tables, conditional formatting, named ranges, external data connections, embedded objects, or other unsupported XLSX parts.

Because of that limitation, XPSpreadsheet deliberately refuses to overwrite external/unmarked XLSX workbooks. `SaveAsSimple()` is the explicit opt-in operation for creating a simplified XPScript workbook from data that XPSpreadsheet can read.

The file-based API is not available for `browser-wasm` targets in this version.
