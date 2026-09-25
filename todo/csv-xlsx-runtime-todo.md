# CSV and XLSX runtime TODO

(c) xpagedeveloper.com 2026

Implement after `todo/image-runtime-todo.md` is complete and merged.

## Architecture and reuse rules

- [ ] Reuse existing XPScript file I/O, path security, Byte-array, JSON, web upload/download and resource-limit infrastructure.
- [ ] Use mature maintained .NET/NuGet libraries for CSV and XLSX parsing/writing where they meet licensing, .NET 10 and Windows/Linux/macOS requirements.
- [ ] Do not implement the XLSX Open Packaging Convention, ZIP/XML workbook format, formula parser or CSV edge-case parser from scratch when an appropriate maintained library exists.
- [ ] Keep third-party libraries behind XPScript-owned interfaces so package choices can be changed without breaking scripts.
- [ ] Candidate libraries must be evaluated for maintenance activity, CVEs, license, dependency footprint and cross-platform support before adoption.

## Shared tabular model

- [ ] Define a provider-neutral `TableData` / `DataTable` style XPScript abstraction for rows, columns and typed cell values without conflicting with legacy .NET `DataTable` terminology used elsewhere in compiler internals.
- [ ] Support column names and optional inferred/declared column types.
- [x] Support indexed row access and named-column access.
- [ ] Support String, Boolean, Integer, Long, Double, Currency, Date, Empty and Null values where representable.
- [ ] Support conversion to and from `XPJsonArray` / `XPJsonObject` for common row-object shapes.
- [ ] Support streaming/enumeration APIs for large files so the whole dataset does not need to be materialized.

## CSV read support

- [x] Add a `CSVDocument` / `CSVReader` API or equivalent stable XPScript surface.
- [x] Read CSV from file path.
- [x] Read CSV from Byte array / stream-compatible input where practical.
- [ ] Configurable delimiter, including comma, semicolon and tab.
- [ ] Configurable quote character and escaping behavior.
- [x] Support RFC 4180-compatible quoted fields, embedded delimiters, embedded newlines and escaped quotes.
- [x] Support files with or without header row.
- [x] Support configurable encoding with UTF-8 as the default.
- [x] Detect/handle UTF-8 BOM safely.
- [ ] Support bounded row, column, field-length and total-input limits.
- [ ] Optional type inference, disabled or conservative by default to avoid surprising conversions.
- [x] Preserve empty field versus missing column semantics explicitly.

## CSV creation and modification

- [x] Create new CSV data in memory.
- [ ] Add/remove rows and columns.
- [x] Set/get individual cell values.
- [x] Write to file path.
- [x] Export as Byte array/string for web responses or further processing.
- [ ] Configurable delimiter, newline convention, quote policy and encoding.
- [x] Quote/escape fields correctly through the selected library.
- [ ] Support append/streaming writer mode for large exports where the library supports it safely.

## XLSX workbook read support

- [ ] Add top-level `ExcelWorkbook` / `XLSXWorkbook` class.
- [x] Load `.xlsx` from file path.
- [x] Load from Byte array / stream-compatible input where practical.
- [x] Enumerate worksheets.
- [x] Read worksheet names, used ranges, rows and cells.
- [ ] Read typed cell values including String, Boolean, numeric and Date/DateTime.
- [ ] Read formulas and cached/display values distinctly where the selected library exposes both.
- [ ] Read merged-cell metadata.
- [ ] Read basic styles/number formats where practical.
- [x] Do not execute macros, external links or embedded active content.
- [ ] `.xls` binary format is out of scope initially unless a mature safe library provides it without complicating the base runtime.

## XLSX creation and modification

- [x] Create a new workbook.
- [ ] Add, rename, reorder and remove worksheets.
- [x] Set/get cells by row/column and A1-style address where practical.
- [ ] Add/remove rows and columns.
- [x] Set cell values and formulas.
- [x] Support basic number/date/currency formats.
- [x] Support fonts, bold/italic, alignment, fill, borders and cell styles through a compact stable API.
- [x] Support column widths and row heights.
- [ ] Support merged cells.
- [ ] Support freeze panes and autofilter where practical.
- [ ] Support basic tables/ranges where the selected library provides reliable support.
- [ ] Support workbook properties/metadata.
- [x] Save as `.xlsx` file.
- [x] Export finished workbook as Byte array for web responses.

## Formula boundary

- [x] Store formulas in XLSX when requested.
- [x] Do not build a custom Excel formula calculation engine.
- [ ] If the selected library has a maintained formula evaluator, evaluate its coverage and security separately before exposing calculation.
- [x] Otherwise expose formula text and cached values and document that recalculation is performed by Excel/LibreOffice or another spreadsheet engine.

## CSV/XLSX conversion

- [x] Convert CSV to a workbook/worksheet.
- [x] Export a worksheet/range to CSV.
- [x] Allow selection of worksheet and delimiter/encoding options.
- [ ] Preserve typed values where possible when moving into XLSX.
- [ ] Define explicit text formatting rules when exporting typed XLSX cells to CSV.

## JSON integration

- [ ] Convert CSV rows to `XPJsonArray` of row objects using headers as property names.
- [ ] Create CSV from `XPJsonArray` / row-object JSON.
- [ ] Convert worksheet ranges to JSON.
- [ ] Populate worksheet ranges from JSON.
- [ ] Preserve missing versus empty values where possible and document unavoidable format differences.

## SQL and Domino integration

- [ ] Allow SQL query results to be exported directly to CSV/XLSX without requiring application-side manual row copying.
- [ ] Allow CSV/XLSX data to feed parameterized SQL import workflows through explicit application code.
- [ ] Allow Domino REST API document/view results to be exported to CSV/XLSX through the shared tabular/JSON model.
- [ ] Keep CSV/XLSX runtime independent of SQL and Domino provider implementations.

## Web runtime integration

- [ ] Read uploaded CSV/XLSX files through existing web upload APIs.
- [ ] Return CSV directly with an appropriate `text/csv` content type and safe attachment filename.
- [ ] Return XLSX directly with `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.
- [ ] Support inline/in-memory generation without requiring a public temporary file.
- [ ] Work through Kestrel, CGI and FastCGI.
- [ ] Apply identical file-size, row-count and memory/resource limits in standalone and web execution.

## Security and resource limits

- [x] Apply existing XPScript path-security rules to input and output files.
- [ ] Prevent output path traversal and unrelated-file overwrite.
- [ ] Bound input file size, row count, column count, field length, worksheet count, cell count and decompressed XLSX size.
- [ ] Defend against ZIP/XML decompression bombs using library/framework limits plus XPScript-level limits.
- [ ] Disable or ignore external workbook links by default.
- [ ] Do not execute macros, scripts or external data connections.
- [ ] Treat formulas beginning with `=`, `+`, `-` or `@` carefully when exporting untrusted values to CSV to mitigate spreadsheet-formula injection; provide a safe-export mode enabled by default for untrusted data.
- [ ] Dispose streams, workbook objects and file handles deterministically.
- [ ] Add concurrency tests proving separate workbook/CSV instances do not share mutable state.

## API examples to validate

- [x] `Dim csv As CSVDocument = CSVDocument.Load("input.csv")` or equivalent compiler-valid syntax.
- [x] `Print csv.RowCount`
- [ ] `Print csv.GetValue(1, "Name")`
- [x] `Call csv.Save("output.csv")`
- [ ] `Dim book As New ExcelWorkbook`
- [ ] `Dim sheet As Variant = book.AddWorksheet("Data")`
- [ ] `Call sheet.SetValue("A1", "Name")`
- [ ] `Call sheet.SetValue("B1", 123)`
- [ ] `Call book.Save("output.xlsx")`
- [ ] Validate exact XPScript syntax against the compiler before freezing the public API.

## Tests and quality gates

- [x] CSV quoted-field/newline/escaping regression tests on Windows, Ubuntu and macOS.
- [x] CSV UTF-8/Unicode/BOM regression tests.
- [ ] CSV malformed/limit negative tests.
- [ ] CSV formula-injection safe-export tests.
- [x] XLSX create/save/reopen round-trip tests.
- [ ] XLSX Unicode, dates, numbers, booleans and formulas tests.
- [x] XLSX multi-sheet and style round-trip tests.
- [ ] XLSX malformed/ZIP-bomb/resource-limit negative tests.
- [x] CSV-to-XLSX and XLSX-to-CSV conversion tests.
- [ ] JSON round-trip tests.
- [ ] SQL-result and Domino-result export integration tests when those providers are implemented.
- [ ] Kestrel, CGI and FastCGI upload/download smoke tests.
- [ ] Add documentation and reusable examples under `docs/` and `examples/`.
