# XPSpreadsheet

`XPSpreadsheet` is XPScript's basic native spreadsheet API for simple `.xlsx` workbooks.

> **Basic implementation:** this first version is intentionally limited to simple workbook data. It can create, read, and update `.xlsx` files, including multiple worksheets/tabs and basic cell values. It is not intended to preserve or edit every advanced spreadsheet feature.

The implementation uses the .NET runtime's built-in ZIP and XML support. It does not require Microsoft Excel, Office, LibreOffice, or an external spreadsheet NuGet package.

## Supported format

Only `.xlsx` is supported. Opening or saving `.xls`, `.xlsm`, `.ods`, `.csv`, or another spreadsheet format returns an explicit error.

When an existing `.xlsx` workbook is opened and saved, XPSpreadsheet rewrites the workbook from the supported model. Advanced features that this basic implementation does not understand may therefore be removed. Do not use this version to round-trip complex workbooks that must retain charts, macros, styling, pivot tables, external links, or similar features.

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

Worksheet names must follow normal XLSX naming rules: they cannot be empty, cannot exceed 31 characters, cannot contain `: \\ / ? * [ ]`, and must be unique within the workbook.

## Cells

Cells can be addressed by A1 notation or by 1-based row and column numbers.

```xpscript
sheet.Cell("C4").Value = "Hello"
sheet.Cell(4, 3).Value = "Hello"
```

Basic values supported by this version are strings, numbers, and Boolean values. Empty cells are represented by an empty value. `Text` returns a string representation of the current cell value.

```xpscript
Dim cell As XPCell
Set cell = sheet.Cell("A1")
Print cell.Address
Print cell.Text
cell.Clear()
```

`UsedRowCount` and `UsedColumnCount` report the largest populated row and column indexes in the supported in-memory model.

## Read an XLSX file

```xpscript
Dim book As New XPSpreadsheet("sales.xlsx")
Dim sheet As XPWorksheet

Set sheet = book.Worksheet("Sales")
Print sheet.Cell("A2").Text
Print CStr(sheet.Cell("B2").Value)
```

The reader accepts normal XLSX string storage forms, including shared strings and inline strings.

## Update an XLSX file

```xpscript
Dim book As New XPSpreadsheet("sales.xlsx")
Dim sheet As XPWorksheet

Set sheet = book.Worksheet("Sales")
sheet.Cell("B2").Value = 15000
sheet.Cell("A3").Value = "Example Ltd"
sheet.Cell("B3").Value = 9000

book.Save()
```

`Save()` updates the file that was opened. `SaveAs()` writes to another `.xlsx` file and makes that file the workbook's current path.

## Main objects

### XPSpreadsheet

Properties:

- `Path`
- `WorksheetCount`

Methods:

- `AddWorksheet(name)`
- `Worksheet(nameOrIndex)`
- `RemoveWorksheet(nameOrIndex)`
- `RenameWorksheet(nameOrIndex, newName)`
- `Open(filename)`
- `Save()`
- `SaveAs(filename)`
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
- `Address`
- `Row`
- `Column`

Methods:

- `Clear()`

## Current limitations

This is a simple basic implementation. The first version does not provide a full spreadsheet application or calculation engine. In particular, do not rely on it to preserve or edit advanced formatting, charts, images, macros/VBA, pivot tables, conditional formatting, named ranges, external data connections, embedded objects, or other advanced XLSX parts.

The file-based API is not available for `browser-wasm` targets in this version.
