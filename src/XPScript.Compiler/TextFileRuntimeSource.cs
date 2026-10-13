namespace XPScript.Compiler;

// Text-file reads and charset selection shared by normal and AST compilation.
internal static class TextFileRuntimeSource
{
    public const string Code = """
internal static class XPScriptTextFileRuntime
{
    public static string ReadFile(object? path, object? charset = null)
    {
        var encoding = ResolveTextEncoding(charset);
        using var reader = new StreamReader(Path.GetFullPath(XPScriptRuntime.CStr(path)), encoding, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public static Encoding ResolveTextEncoding(object? charset)
    {
        if (charset is null || string.IsNullOrWhiteSpace(XPScriptRuntime.CStr(charset))) return new UTF8Encoding(false);
        var name = XPScriptRuntime.CStr(charset).Trim();
        var normalized = name.Replace("_", "-", StringComparison.Ordinal).ToLowerInvariant();
        if (normalized is "utf8" or "utf-8") return new UTF8Encoding(false);
        if (normalized is "utf16" or "utf-16" or "unicode") return new UnicodeEncoding(false, true);
        if (normalized is "utf-16be" or "unicodefffe") return new UnicodeEncoding(true, true);
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        try { return System.Text.Encoding.GetEncoding(name); }
        catch (ArgumentException) { throw new XPScriptRuntimeException(5, "Unsupported text charset: " + name); }
    }

}
""";
}
