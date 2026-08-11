namespace XPScript.Compiler;

internal static class ReferenceRuntimeExtensionsSource
{
    public const string Code = """
internal static class XPScriptReferenceRuntime
{
    private static Encoding ByteEncoding => Encoding.Default;

    public static int InstrB(object? source, object? find) => InstrB(1, source, find);

    public static int InstrB(object? startValue, object? source, object? find)
    {
        var start = Math.Max(1, XPScriptRuntime.CInt(startValue));
        var sourceBytes = ByteEncoding.GetBytes(XPScriptRuntime.CStr(source));
        var findBytes = ByteEncoding.GetBytes(XPScriptRuntime.CStr(find));
        if (findBytes.Length == 0) return Math.Min(start, sourceBytes.Length + 1);
        var startIndex = Math.Max(0, start - 1);
        for (var i = startIndex; i <= sourceBytes.Length - findBytes.Length; i++)
        {
            var match = true;
            for (var j = 0; j < findBytes.Length; j++)
            {
                if (sourceBytes[i + j] == findBytes[j]) continue;
                match = false;
                break;
            }
            if (match) return i + 1;
        }
        return 0;
    }

    public static string LeftB(object? value, object? countValue)
    {
        var bytes = ByteEncoding.GetBytes(XPScriptRuntime.CStr(value));
        var count = Math.Clamp(XPScriptRuntime.CInt(countValue), 0, bytes.Length);
        return DecodeSafe(bytes.AsSpan(0, count));
    }

    public static string RightB(object? value, object? countValue)
    {
        var bytes = ByteEncoding.GetBytes(XPScriptRuntime.CStr(value));
        var count = Math.Clamp(XPScriptRuntime.CInt(countValue), 0, bytes.Length);
        return DecodeSafe(bytes.AsSpan(bytes.Length - count, count));
    }

    public static string MidB(object? value, object? startValue) => MidB(value, startValue, int.MaxValue);

    public static string MidB(object? value, object? startValue, object? countValue)
    {
        var bytes = ByteEncoding.GetBytes(XPScriptRuntime.CStr(value));
        var start = Math.Max(1, XPScriptRuntime.CInt(startValue));
        var index = start - 1;
        if (index >= bytes.Length) return "";
        var requested = XPScriptRuntime.CInt(countValue);
        var count = requested == int.MaxValue ? bytes.Length - index : Math.Clamp(requested, 0, bytes.Length - index);
        return DecodeSafe(bytes.AsSpan(index, count));
    }

    private static string DecodeSafe(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0) return "";
        return ByteEncoding.GetString(bytes);
    }

    public static string StrLeft(object? value, object? delimiter)
    {
        var text = XPScriptRuntime.CStr(value);
        var token = XPScriptRuntime.CStr(delimiter);
        if (token.Length == 0) return text;
        var index = text.IndexOf(token, StringComparison.CurrentCulture);
        return index < 0 ? text : text[..index];
    }

    public static string StrLeftBack(object? value, object? delimiter)
    {
        var text = XPScriptRuntime.CStr(value);
        var token = XPScriptRuntime.CStr(delimiter);
        if (token.Length == 0) return text;
        var index = text.LastIndexOf(token, StringComparison.CurrentCulture);
        return index < 0 ? text : text[..index];
    }

    public static string StrRight(object? value, object? delimiter)
    {
        var text = XPScriptRuntime.CStr(value);
        var token = XPScriptRuntime.CStr(delimiter);
        if (token.Length == 0) return "";
        var index = text.IndexOf(token, StringComparison.CurrentCulture);
        return index < 0 ? "" : text[(index + token.Length)..];
    }

    public static string StrRightBack(object? value, object? delimiter)
    {
        var text = XPScriptRuntime.CStr(value);
        var token = XPScriptRuntime.CStr(delimiter);
        if (token.Length == 0) return "";
        var index = text.LastIndexOf(token, StringComparison.CurrentCulture);
        return index < 0 ? "" : text[(index + token.Length)..];
    }

    public static string StrToken(object? value, object? delimiter, object? indexValue)
    {
        var text = XPScriptRuntime.CStr(value);
        var token = XPScriptRuntime.CStr(delimiter);
        var oneBased = XPScriptRuntime.CInt(indexValue);
        if (oneBased < 1) return "";
        if (token.Length == 0) return oneBased == 1 ? text : "";
        var parts = text.Split([token], StringSplitOptions.None);
        return oneBased <= parts.Length ? parts[oneBased - 1] : "";
    }

    public static string LSet(object? value, object? widthValue)
    {
        var width = Math.Max(0, XPScriptRuntime.CInt(widthValue));
        var text = XPScriptRuntime.CStr(value);
        if (text.Length >= width) return text[..width];
        return text.PadRight(width);
    }

    public static string RSet(object? value, object? widthValue)
    {
        var width = Math.Max(0, XPScriptRuntime.CInt(widthValue));
        var text = XPScriptRuntime.CStr(value);
        if (text.Length >= width) return text[^width..];
        return text.PadLeft(width);
    }

    public static string UChr(object? codeValue)
    {
        var code = XPScriptRuntime.CInt(codeValue);
        if (!Rune.IsValid(code)) throw new XPScriptRuntimeException(5, "Invalid Unicode code value.");
        return new Rune(code).ToString();
    }

    public static int Uni(object? value)
    {
        var text = XPScriptRuntime.CStr(value);
        if (text.Length == 0) return 0;
        return Rune.GetRuneAt(text, 0).Value;
    }

    public static DateTime CVDate(object? value) => XPScriptRuntime.CDate(value);

    public static bool IsList(object? value)
    {
        if (value is null) return false;
        var type = value.GetType();
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(LSList<>);
    }

    public static bool IsUnknown(object? value) => value is null;

    public static string Base64Encode(object? value) => XPScriptTextIO.ToBase64(value);
    public static string Base64Encode(object? value, object? charset) => XPScriptTextIO.ToBase64(value, charset);
    public static string Base64Decode(object? value) => XPScriptTextIO.FromBase64(value);
    public static string Base64Decode(object? value, object? charset) => XPScriptTextIO.FromBase64(value, charset);
}
""";
}
