namespace XPScript.Compiler;

// Shared by native CSV and spreadsheet interop; preserves the existing loader contract.
internal static class CsvFileRuntimeSource
{
    public const string Code = """
internal static class XPScriptCsvFileRuntime
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

}
""";
}
