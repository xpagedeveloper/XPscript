namespace XPScript.Compiler;

internal static class SpreadsheetCsvInteropRuntimeSource
{
    public const string Code = """
internal static class XPScriptSpreadsheetCsvInterop
{
    public static XPScriptSpreadsheet ToSpreadsheet(object? csvValue, object? sheetName = null)
    {
        var csv = Unwrap(csvValue) ?? throw new XPScriptRuntimeException(91, "XPCsvDocument.ToSpreadsheet requires a CSV document.");
        var type = csv.GetType();
        if (!type.Name.Equals("XPScriptCsvDocument", StringComparison.Ordinal))
            throw new XPScriptRuntimeException(13, "XPCsvDocument.ToSpreadsheet requires a CSV document.");

        var book = new XPScriptSpreadsheet();
        var name = sheetName is null ? "Sheet1" : XPScriptRuntime.CStr(sheetName);
        var sheet = book.AddWorksheet(name);
        var hasHeaders = (bool)(type.GetProperty("HasHeaders")?.GetValue(csv) ?? false);
        var columnCount = System.Convert.ToInt32(type.GetProperty("ColumnCount")?.GetValue(csv) ?? 0, System.Globalization.CultureInfo.InvariantCulture);
        var rowCount = System.Convert.ToInt32(type.GetProperty("RowCount")?.GetValue(csv) ?? 0, System.Globalization.CultureInfo.InvariantCulture);
        var outputRow = 1;

        if (hasHeaders)
        {
            var headers = type.GetProperty("Headers")?.GetValue(csv);
            var getHeader = headers?.GetType().GetMethod("Get");
            for (var column = 0; column < columnCount; column++)
                sheet.GetCell(outputRow, column + 1).Value = XPScriptRuntime.CStr(getHeader!.Invoke(headers, [column]));
            outputRow++;
        }

        var rows = type.GetProperty("Rows")?.GetValue(csv);
        var getRow = rows?.GetType().GetMethod("Get");
        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var row = getRow!.Invoke(rows, [rowIndex]);
            var getValue = row!.GetType().GetMethod("Get");
            for (var column = 0; column < columnCount; column++)
                sheet.GetCell(outputRow, column + 1).Value = XPScriptRuntime.CStr(getValue!.Invoke(row, [column]));
            outputRow++;
        }
        return book;
    }

    public static object FromWorksheet(object? worksheetValue, object? hasHeaders = null)
    {
        var worksheet = Unwrap(worksheetValue) as XPScriptSpreadsheetWorksheet
            ?? throw new XPScriptRuntimeException(13, "XPCsvDocument.FromWorksheet requires an XPWorksheet.");
        var csvType = System.Reflection.Assembly.GetExecutingAssembly().GetType("XPScriptCsvDocument", throwOnError: false);
        if (csvType is null)
            throw new XPScriptRuntimeException(5, "CSV runtime is not available. Declare or use XPCsvDocument in the script.");
        var csv = System.Activator.CreateInstance(csvType, nonPublic: true)
            ?? throw new XPScriptRuntimeException(5, "Unable to create CSV document.");
        var headers = hasHeaders is null || XPScriptRuntime.CBool(hasHeaders);
        csvType.GetProperty("HasHeaders")!.SetValue(csv, headers);
        var addHeader = csvType.GetMethod("AddHeader")!;
        var addRow = csvType.GetMethod("AddRow", [typeof(object)])!;
        var firstDataRow = 1;

        if (headers && worksheet.UsedRowCount > 0)
        {
            for (var column = 1; column <= worksheet.UsedColumnCount; column++)
                addHeader.Invoke(csv, [XPScriptRuntime.CStr(worksheet.GetCell(1, column).Value)]);
            firstDataRow = 2;
        }

        for (var row = firstDataRow; row <= worksheet.UsedRowCount; row++)
        {
            var values = new string[worksheet.UsedColumnCount];
            for (var column = 1; column <= worksheet.UsedColumnCount; column++)
                values[column - 1] = XPScriptRuntime.CStr(worksheet.GetCell(row, column).Value);
            addRow.Invoke(csv, [values]);
        }
        return csv;
    }

    private static object? Unwrap(object? value)
    {
        if (value is ILSObjectReference reference) return reference.IsNothing ? null : reference.ObjectValue;
        return value;
    }
}
""";
}
