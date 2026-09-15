namespace XPScript.Compiler;

internal static class SpreadsheetCsvInteropRuntimeSource
{
    public const string Code = """
internal static class XPScriptSpreadsheetCsvInterop
{
    public static object LoadCsv(object? path, object? encoding = null, object? delimiter = null, object? hasHeaders = null)
    {
        var csvRuntime = System.Reflection.Assembly.GetExecutingAssembly().GetType("XPScriptNativeCsv", throwOnError: false)
            ?? throw new XPScriptRuntimeException(5, "CSV runtime is not available.");
        byte[] bytes;
        try
        {
            var file = System.IO.Path.GetFullPath(XPScriptRuntime.CStr(path));
            bytes = System.IO.File.ReadAllBytes(file);
        }
        catch (System.UnauthorizedAccessException exception)
        {
            throw new XPScriptRuntimeException(70, exception.Message);
        }
        catch (System.IO.FileNotFoundException exception)
        {
            throw new XPScriptRuntimeException(53, exception.Message);
        }
        catch (System.IO.DirectoryNotFoundException exception)
        {
            throw new XPScriptRuntimeException(76, exception.Message);
        }
        catch (System.IO.IOException exception)
        {
            throw new XPScriptRuntimeException(75, exception.Message);
        }
        var requested = encoding is null ? "auto" : XPScriptRuntime.CStr(encoding).Trim();
        string actualEncoding;
        if (requested.Length == 0 || requested.Equals("auto", StringComparison.OrdinalIgnoreCase))
            actualEncoding = DetectCsvEncoding(bytes);
        else
        {
            var normalize = csvRuntime.GetMethod("NormalizeEncodingName", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!;
            actualEncoding = (string)normalize.Invoke(null, [requested])!;
        }
        var actualDelimiter = delimiter is null ? "," : delimiter;
        var actualHeaders = hasHeaders is null || XPScriptRuntime.CBool(hasHeaders);
        var parseBytes = csvRuntime.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .First(method => method.Name == "ParseBytes" && method.GetParameters().Length == 4);
        return parseBytes.Invoke(null, [bytes, actualEncoding, actualDelimiter, actualHeaders])
            ?? throw new XPScriptRuntimeException(5, "Unable to parse CSV file.");
    }

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
        if (csvType is null) throw new XPScriptRuntimeException(5, "CSV runtime is not available. Declare or use XPCsvDocument in the script.");
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

    private static string DetectCsvEncoding(byte[] bytes)
    {
        if (StartsWith(bytes, [0xEF, 0xBB, 0xBF])) return "utf-8-bom";
        if (StartsWith(bytes, [0xFF, 0xFE])) return "utf-16";
        if (StartsWith(bytes, [0xFE, 0xFF])) return "utf-16be";
        if (bytes.Length >= 8)
        {
            var pairs = bytes.Length / 2;
            var evenNulls = 0;
            var oddNulls = 0;
            for (var i = 0; i + 1 < bytes.Length; i += 2)
            {
                if (bytes[i] == 0) evenNulls++;
                if (bytes[i + 1] == 0) oddNulls++;
            }
            if (oddNulls * 4 >= pairs * 3 && evenNulls * 4 <= pairs) return "utf-16";
            if (evenNulls * 4 >= pairs * 3 && oddNulls * 4 <= pairs) return "utf-16be";
        }
        try
        {
            _ = new System.Text.UTF8Encoding(false, true).GetString(bytes);
            return "utf-8";
        }
        catch (System.Text.DecoderFallbackException)
        {
            return "windows-1252";
        }
    }

    private static bool StartsWith(byte[] value, byte[] prefix)
    {
        if (value.Length < prefix.Length) return false;
        for (var i = 0; i < prefix.Length; i++) if (value[i] != prefix[i]) return false;
        return true;
    }

    private static object? Unwrap(object? value)
    {
        if (value is ILSObjectReference reference) return reference.IsNothing ? null : reference.ObjectValue;
        return value;
    }
}
""";
}
