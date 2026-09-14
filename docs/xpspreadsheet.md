# XPSpreadsheet

`XPSpreadsheet` is XPScript's basic native spreadsheet API for simple `.xlsx` workbooks.

> **Basic implementation:** this version supports simple workbook data, multiple worksheets/tabs, basic formulas, and basic cell styling. Existing external `.xlsx` files can be opened and read, but are read-only by default so XPSpreadsheet does not accidentally remove spreadsheet features it does not understand.

The implementation uses the .NET runtime's built-in ZIP and XML support. It does not require Microsoft Excel, Office, LibreOffice, or an external spreadsheet NuGet package.

## Supported format

Only `.xlsx` is supported. Opening or saving `.xls`, `.xlsm`, `.ods`, `.csv`, or another spreadsheet format returns an explicit error.

## Safe update model

XPSpreadsheet marks every workbook it creates with the custom OOXML document property `XPScriptWorkbookVersion`. Version 1 writes `XPScriptWorkbookVersion=1`.

When a workbook is opened, XPSpreadsheet exposes:

- `CreatedByXPScript` - `True` when the XPScript workbook marker is present.
- `XPScriptFormatVersion` - the marker version, or `0` for an external/unmarked workbook.
- `CanUpdate` - `True` only when the workbook was created by XPScript and the marker version is supported by the current runtime.

External `.xlsx` files are readable, but `Save()`, `SaveAs()`, and `ToBytes()` refuse to rewrite them. This prevents a basic XPSpreadsheet round trip from silently removing unsupported charts, styles, images, pivot tables, named ranges, external links, or other OOXML parts.

If you intentionally want to convert an external workbook to the simple XPSpreadsheet model, use `SaveAsSimple()`:

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

## Cell styling

XPSpreadsheet supports basic cell fill and font emphasis using normal OOXML styles.

```xpscript
sheet.Cell("A1").BackgroundColor = "#FFFF00"
sheet.Cell("A1").Bold = True
sheet.Cell("A1").Italic = True
```

`BackgroundColor` accepts:

- `#RRGGBB` or `RRGGBB` hex colors.
- 8-digit ARGB values, where the alpha prefix is ignored and the RGB part is used.
- the color names `black`, `white`, `red`, `green`, `blue`, `yellow`, `gray`/`grey`, `orange`, and `purple`.
- an empty string to remove the background fill.

`Bold` and `Italic` are independent Boolean properties and can be combined. A normal font is represented by both properties being `False`.

```xpscript
Dim cell As XPCell
Set cell = sheet.Cell("A1")
cell.Bold = False
cell.Italic = False
```

These supported styles are written to `xl/styles.xml` and are restored when an XPScript workbook is reopened or loaded through `FromBytes()`.

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

## Update an XPScript workbook

```xpscript
Dim book As New XPSpreadsheet("sales.xlsx")
Dim sheet As XPWorksheet

If Not book.CanUpdate Then
    Error 1000, "Workbook is read-only in XPSpreadsheet"
End If

Set sheet = book.Worksheet("Sales")
sheet.Cell("B2").Value = 15000
sheet.Cell("A3").Value = "Example Ltd"
sheet.Cell("B3").Value = 9000

book.Save()
```

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

Methods:

- `Cell(address)`
- `Cell(row, column)`
- `Clear()`

### XPCell

Properties:

- `Value`
- `Text`
- `Formula`
- `BackgroundColor`
- `Bold`
- `Italic`
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
- `xpspreadsheet-invalid-format.xps`

## Current limitations

XPSpreadsheet still does not provide a full spreadsheet application or calculation engine. It does not preserve or edit advanced formatting, charts, images, macros/VBA, pivot tables, conditional formatting, named ranges, external data connections, embedded objects, or other advanced XLSX parts.

Because of that limitation, XPSpreadsheet deliberately refuses to overwrite external/unmarked XLSX workbooks. `SaveAsSimple()` is the explicit opt-in operation for creating a simplified XPScript workbook from data that XPSpreadsheet can read.

The file-based API is not available for `browser-wasm` targets in this version.
