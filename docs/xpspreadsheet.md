# XPSpreadsheet

`XPSpreadsheet` is XPScript's basic native spreadsheet API for simple `.xlsx` workbooks.

> **Basic implementation:** this first version is intentionally limited to simple workbook data. It can create, read, and update `.xlsx` files created by XPScript, including multiple worksheets/tabs and basic cell values. Existing external `.xlsx` files can be opened and read, but are read-only by default so XPSpreadsheet does not accidentally remove spreadsheet features it does not understand.

The implementation uses the .NET runtime's built-in ZIP and XML support. It does not require Microsoft Excel, Office, LibreOffice, or an external spreadsheet NuGet package.

## Supported format

Only `.xlsx` is supported. Opening or saving `.xls`, `.xlsm`, `.ods`, `.csv`, or another spreadsheet format returns an explicit error.

## Safe update model

XPSpreadsheet marks every workbook it creates with the custom OOXML document property `XPScriptWorkbookVersion`. Version 1 of XPSpreadsheet writes `XPScriptWorkbookVersion=1`.

When a workbook is opened, XPSpreadsheet exposes:

- `CreatedByXPScript` - `True` when the XPScript workbook marker is present.
- `XPScriptFormatVersion` - the marker version, or `0` for an external/unmarked workbook.
- `CanUpdate` - `True` only when the workbook was created by XPScript and the marker version is supported by the current runtime.

External `.xlsx` files are readable, but `Save()`, `SaveAs()`, and `ToBytes()` refuse to rewrite them. This prevents a basic XPSpreadsheet round trip from silently removing unsupported charts, styles, images, pivot tables, named ranges, external links, or other OOXML parts.

If you intentionally want to convert an external workbook to the simple XPSpreadsheet model, use `SaveAsSimple()`:

```xpscript
Dim book As New XPSpreadsheet("external.xlsx")

Print CStr(book.CreatedByXPScript)     ' False
Print CStr(book.CanUpdate)             ' False

' Explicitly creates a new simplified workbook containing supported data only.
book.SaveAsSimple("converted.xlsx")
```

`SaveAsSimple()` is intentionally explicit because unsupported workbook content may be removed. The resulting workbook is marked as an XPScript workbook and can subsequently be updated with normal `Save()` and `SaveAs()` calls.

A workbook marked with a newer `XPScriptWorkbookVersion` than the current runtime supports is also opened read-only rather than being rewritten by an older runtime.

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

Reading does not require the workbook to have been created by XPScript.

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

`Save()` updates the XPScript-created file that was opened. `SaveAs()` writes an XPScript-created workbook to another `.xlsx` file and makes that file the workbook's current path.

For external/unmarked workbooks, use `SaveAsSimple()` only when intentional simplification is acceptable.

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
- `Address`
- `Row`
- `Column`

Methods:

- `Clear()`

## Current limitations

This is a simple basic implementation. The first version does not provide a full spreadsheet application or calculation engine. In particular, it does not preserve or edit advanced formatting, charts, images, macros/VBA, pivot tables, conditional formatting, named ranges, external data connections, embedded objects, or other advanced XLSX parts.

Because of that limitation, XPSpreadsheet deliberately refuses to overwrite external/unmarked XLSX workbooks. `SaveAsSimple()` is the explicit opt-in operation for creating a simplified XPScript workbook from data that XPSpreadsheet can read.

The file-based API is not available for `browser-wasm` targets in this version.
