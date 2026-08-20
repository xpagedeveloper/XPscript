namespace XPScript.Compiler;

internal static class HclReferenceCompatibilityRuntimeSource
{
    public const string Code = """
internal static class LSHclReferenceRuntime
{
    public static double ACos(object? value)
    {
        var number = XPScriptRuntime.CDbl(value);
        if (number < -1d || number > 1d) throw new XPScriptRuntimeException(5, "ACos argument must be between -1 and 1.");
        return Math.Acos(number);
    }

    public static double ASin(object? value)
    {
        var number = XPScriptRuntime.CDbl(value);
        if (number < -1d || number > 1d) throw new XPScriptRuntimeException(5, "ASin argument must be between -1 and 1.");
        return Math.Asin(number);
    }

    public static double ATn(object? value) => Math.Atan(XPScriptRuntime.CDbl(value));

    public static double ATn2(object? y, object? x) => Math.Atan2(XPScriptRuntime.CDbl(y), XPScriptRuntime.CDbl(x));

    public static string Bin(object? value) => Convert.ToString(XPScriptRuntime.CLng(value), 2) ?? "0";
    public static string Hex(object? value) => XPScriptRuntime.CLng(value).ToString("X", CultureInfo.InvariantCulture);
    public static string Oct(object? value) => Convert.ToString(XPScriptRuntime.CLng(value), 8) ?? "0";

    public static double Fraction(object? value)
    {
        var number = XPScriptRuntime.CDbl(value);
        return number - Math.Truncate(number);
    }

    public static object FullTrim(object? value)
    {
        if (value is Array array)
        {
            var output = new List<object?>();
            foreach (var item in array)
            {
                var text = CollapseWhitespace(XPScriptRuntime.CStr(item));
                if (text.Length > 0) output.Add(text);
            }
            return output.ToArray();
        }
        return CollapseWhitespace(XPScriptRuntime.CStr(value));
    }

    public static string Implode(object? value, object? delimiter = null)
    {
        var separator = delimiter is null ? " " : XPScriptRuntime.CStr(delimiter);
        if (value is not System.Collections.IEnumerable enumerable || value is string)
            throw new XPScriptRuntimeException(13, "Implode requires an array.");
        return string.Join(separator, enumerable.Cast<object?>().Select(XPScriptRuntime.CStr));
    }

    public static bool IsScalar(object? value) => value is not null && value is not Array && value is not System.Collections.IEnumerable;

    public static string LTrim(object? value) => XPScriptRuntime.CStr(value).TrimStart(' ');
    public static string RTrim(object? value) => XPScriptRuntime.CStr(value).TrimEnd(' ');

    public static int StrCompare(object? left, object? right, object? compare = null)
    {
        var comparison = compare is null || XPScriptRuntime.CInt(compare) == 0
            ? StringComparison.CurrentCulture
            : StringComparison.CurrentCultureIgnoreCase;
        return Math.Sign(string.Compare(XPScriptRuntime.CStr(left), XPScriptRuntime.CStr(right), comparison));
    }

    public static string StrLeft(object? text, object? pattern)
    {
        var source = XPScriptRuntime.CStr(text);
        var token = XPScriptRuntime.CStr(pattern);
        if (token.Length == 0) return "";
        var index = source.IndexOf(token, StringComparison.CurrentCulture);
        return index < 0 ? source : source[..index];
    }

    public static string StrLeftBack(object? text, object? pattern)
    {
        var source = XPScriptRuntime.CStr(text);
        var token = XPScriptRuntime.CStr(pattern);
        if (token.Length == 0) return "";
        var index = source.LastIndexOf(token, StringComparison.CurrentCulture);
        return index < 0 ? source : source[..index];
    }

    public static string StrRight(object? text, object? pattern)
    {
        var source = XPScriptRuntime.CStr(text);
        var token = XPScriptRuntime.CStr(pattern);
        if (token.Length == 0) return source;
        var index = source.IndexOf(token, StringComparison.CurrentCulture);
        return index < 0 ? "" : source[(index + token.Length)..];
    }

    public static string StrRightBack(object? text, object? pattern)
    {
        var source = XPScriptRuntime.CStr(text);
        var token = XPScriptRuntime.CStr(pattern);
        if (token.Length == 0) return source;
        var index = source.LastIndexOf(token, StringComparison.CurrentCulture);
        return index < 0 ? "" : source[(index + token.Length)..];
    }

    public static string StrToken(object? text, object? separators, object? tokenNumber)
    {
        var source = XPScriptRuntime.CStr(text);
        var separatorText = XPScriptRuntime.CStr(separators);
        var index = XPScriptRuntime.CInt(tokenNumber);
        if (index < 1) return "";
        var chars = separatorText.Length == 0 ? new[] { ' ' } : separatorText.ToCharArray();
        var tokens = source.Split(chars, StringSplitOptions.RemoveEmptyEntries);
        return index <= tokens.Length ? tokens[index - 1] : "";
    }

    public static string UChr(object? code)
    {
        var value = XPScriptRuntime.CLng(code);
        if (value < 0 || value > 0x10FFFF || value is >= 0xD800 and <= 0xDFFF)
            throw new XPScriptRuntimeException(5, "UChr requires a valid Unicode scalar value.");
        return char.ConvertFromUtf32((int)value);
    }

    public static int Uni(object? value)
    {
        var text = XPScriptRuntime.CStr(value);
        if (text.Length == 0) throw new XPScriptRuntimeException(5, "Uni requires a non-empty string.");
        return char.ConvertToUtf32(text, 0);
    }

    public static string UString(object? count, object? character)
    {
        var length = XPScriptRuntime.CInt(count);
        if (length < 0) throw new XPScriptRuntimeException(5, "UString count cannot be negative.");
        var unit = character is string text ? (text.Length == 0 ? "" : char.ConvertFromUtf32(char.ConvertToUtf32(text, 0))) : UChr(character);
        if (unit.Length == 0 || length == 0) return "";
        var builder = new StringBuilder(unit.Length * length);
        for (var i = 0; i < length; i++) builder.Append(unit);
        return builder.ToString();
    }

    private static string CollapseWhitespace(string value)
    {
        if (value.Length == 0) return "";
        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts);
    }
}
""";
}
