# Native CSV

XPscript provides a native CSV API for parsing, building, iterating, sorting, serializing and saving CSV data without external runtime dependencies.

CSV values are text. Parsing does not infer Integer, Boolean or Date values, so values such as `00123` are preserved exactly.

## Parse CSV

```xpscript
Dim doc As XPCsvDocument
Dim row As XPCsvRow

Set doc = CsvParse(csvText, ";")
Set row = doc.Rows[0]

Print doc.RowCount
Print doc.ColumnCount
Print row.Get("name")
```

`XPCsvDocument.Parse(text)` and `CsvParse(text)` use comma as the default delimiter. Pass `","` or `";"` explicitly when required:

```xpscript
Set doc = XPCsvDocument.Parse(csvText, ";")
```

Only comma and semicolon are accepted as delimiters.

The parser supports quoted fields, doubled quotes, embedded delimiters and embedded CR/LF line breaks. Input line endings may be LF, CRLF or CR. Serialized output uses LF.

## Headers, rows and columns

Headers are exposed as an indexed and iterable collection. Headers can also be added through the collection:

```xpscript
Print doc.Headers.Count
Print doc.Headers[0]
Print doc.Headers.Get(1)

Call doc.Headers.Add("email")

ForAll header In doc.Headers
    Print header
End ForAll
```

`Headers.Add(name)` and `AddHeader(name)` perform the same schema-changing operation. Existing rows are extended with an empty value. Duplicate header names are rejected case-insensitively.

Rows are also indexed and iterable:

```xpscript
Dim row As XPCsvRow

Set row = doc.Rows[0]

ForAll row In doc.Rows
    Print row.Get("name")
End ForAll
```

Each row exposes individual columns as an indexed and iterable collection:

```xpscript
Dim column As XPCsvColumn

ForAll column In row.Columns
    Print CStr(column.Index) & " " & column.Name & " = " & column.Value
End ForAll

Print row.Columns[0].Value
```

`XPCsvColumn` exposes `Index`, `Name` and `Value`. `Name` is empty when `HasHeaders = False`.

The document exposes:

- `RowCount`, number of data rows. The header row is not counted when `HasHeaders = True`.
- `ColumnCount`, number of columns.
- `Headers.Count`.
- `Rows.Count`, equal to `RowCount`.

Indexes are zero-based.

## Row access and strict column names

Rows support index and name access:

```xpscript
Print row[0]
Print row.Get(0)
Print row.Get("name")
```

Name lookup is case-insensitive and requires headers. Missing names are runtime errors that can be trapped with normal XPscript error handling:

```xpscript
On Error Resume Next
Print row.Get("missing")
Print CStr(Err)
Print Error$
On Error GoTo 0
```

The diagnostic is:

```text
CSV column name 'missing' not found.
```

`Set` is intentionally strict as well:

```xpscript
Call row.Set("name", "Anna")
```

`row.Set("missing", value)` does not create a new column. It raises the same missing-column error. Index access outside the row raises `CSV column index out of range.`

## Build CSV

```xpscript
Dim csv As New XPCsvDocument
Dim row As XPCsvRow

csv.Delimiter = ";"

Call csv.Headers.Add("id")
Call csv.Headers.Add("name")
Call csv.Headers.Add("city")

Set row = csv.AddRow()
Call row.Set("id", "00123")
Call row.Set("name", "Åsa")
Call row.Set("city", "Malmö; Sweden")

Print CsvStringify(csv)
```

The builder escapes fields automatically. `CsvEscape(value [, delimiter])` is available when an escaped field is needed independently:

```xpscript
Print CsvEscape("Malmö; Sweden", ";")
```

returns:

```text
"Malmö; Sweden"
```

## Sort rows

`XPCsvDocument.Sort(column)` sorts all data rows in place in ascending alphanumeric order. The header row is never moved.

The column selector can be a header name:

```xpscript
Call csv.Sort("name")
```

or a zero-based numeric column index:

```xpscript
Call csv.Sort(0)
```

Header-name lookup is case-insensitive and requires `HasHeaders = True`. A numeric selector works with or without headers and must be between `0` and `ColumnCount - 1`.

Sorting is case-insensitive and uses natural alphanumeric ordering. Numeric runs are compared numerically, so values such as `item2` sort before `item10`. Rows whose selected values compare equal keep their original relative order.

Example:

```xpscript
Dim csv As XPCsvDocument
Set csv = CsvParse("id;name" & Chr(10) & _
                   "2;item10" & Chr(10) & _
                   "1;item2" & Chr(10) & _
                   "3;Item1", ";")

Call csv.Sort("name")
' Item1, item2, item10

Call csv.Sort(0)
' 1, 2, 3
```

## Save CSV files

File output belongs to the `XPCsvDocument` that contains the CSV data.

```xpscript
Dim csv As New XPCsvDocument

Call csv.Headers.Add("name")
Call csv.Headers.Add("city")
Call csv.Save("output.csv")
```

`Save(path [, encoding])` serializes the current contents of that `XPCsvDocument` and replaces the target file:

```xpscript
Call csv.Save("customers.csv")
Call csv.Save("customers-1252.csv", "windows-1252")
```

`SaveFile(path [, encoding])` is the explicit file-named alias and has identical behavior:

```xpscript
Call csv.SaveFile("customers.csv")
Call csv.SaveFile("customers-utf16.csv", "utf-16")
```

There are no global `CsvSave` or `CsvWriteFile` functions. `XPCsvDocument.WriteFile` is also not part of the API. Use `Save` or `SaveFile` on the document instance.

The document encoding is used by default. `FileEncoding` is an alias for `Encoding` when working with file output:

```xpscript
csv.FileEncoding = "utf-8-bom"
Call csv.Save("customers.csv")
```

Passing an encoding to `Save` or `SaveFile` overrides the encoding for that write without changing the document setting.

Relative paths follow XPscript file-system path handling.

## Header mode

`HasHeaders` defaults to `True`.

```xpscript
Set doc = CsvParse(text, ";", False)
```

When `HasHeaders = False`, every record is a data row and `RowCount` includes the first record. Name-based `Get` and `Set` require headers.

Changing `HasHeaders` on an existing document promotes the first row to headers or demotes the current headers to the first data row.

## Encoding and bytes

XPscript `String` values are Unicode, so encoding is only relevant when CSV crosses a byte boundary.

Parse byte data with an explicit encoding:

```xpscript
Set doc = CsvParseBytes(data, "windows-1252", ";")
```

or:

```xpscript
Set doc = XPCsvDocument.ParseBytes(data, "utf-8", ",")
```

Supported encoding names are:

- `utf-8`
- `utf-8-bom`
- `windows-1252` (`cp1252` and `1252` aliases)
- `iso-8859-1` (`latin1` and `latin-1` aliases)
- `utf-16` / `utf-16le`
- `utf-16be`

Serialize to bytes using the document encoding:

```xpscript
csv.Encoding = "windows-1252"
data = csv.ToBytes()
```

or choose an encoding for one call:

```xpscript
data = csv.ToBytes("utf-8")
```

`FileEncoding` and `Encoding` refer to the same document encoding for file output.

Characters that cannot be represented in Windows-1252 cause a trap-able runtime error instead of silent replacement.

## API summary

`XPCsvDocument`:

- `Headers`
- `Rows`
- `RowCount`
- `ColumnCount`
- `HasHeaders`
- `Delimiter`
- `Encoding`
- `FileEncoding`
- `AddHeader(name)`
- `AddRow()`
- `Sort(column)`
- `Stringify()`
- `ToBytes([encoding])`
- `Save(path [, encoding])`
- `SaveFile(path [, encoding])`

Functions:

- `CsvParse(text [, delimiter [, hasHeaders]])`
- `CsvParseBytes(bytes, encoding [, delimiter [, hasHeaders]])`
- `CsvStringify(document)`
- `CsvEscape(value [, delimiter])`

Collections:

- `XPCsvHeaderCollection`: `Count`, `Get(index)`, `Add(name)`, `ForAll`
- `XPCsvRowCollection`: `Count`, `Get(index)`, `ForAll`
- `XPCsvColumnCollection`: `Count`, `Get(index)`, `ForAll`

`XPCsvRow`:

- `Count`
- `Columns`
- `Get(indexOrName)`
- `Set(indexOrName, value)`

## Resource limit

Native CSV parsing accepts at most 32 MiB of input. The byte limit is checked directly for byte input and as UTF-8 size for String input.
