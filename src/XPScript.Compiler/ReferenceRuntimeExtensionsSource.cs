namespace XPScript.Compiler;

internal static class ReferenceRuntimeExtensionsSource
{
    public const string Code = """
internal static class XPScriptReferenceRuntime
{
    private static Encoding ByteEncoding => Encoding.Default;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

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

    public static string StrConv(object? value, object? conversion)
    {
        var text = XPScriptRuntime.CStr(value);
        if (conversion is string name)
        {
            return name.Trim().ToLowerInvariant() switch
            {
                "upper" or "uppercase" => text.ToUpper(CultureInfo.CurrentCulture),
                "lower" or "lowercase" => text.ToLower(CultureInfo.CurrentCulture),
                "proper" or "propercase" => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower(CultureInfo.CurrentCulture)),
                _ => throw new XPScriptRuntimeException(5, "Unsupported StrConv conversion: " + name)
            };
        }

        return XPScriptRuntime.CInt(conversion) switch
        {
            1 => text.ToUpper(CultureInfo.CurrentCulture),
            2 => text.ToLower(CultureInfo.CurrentCulture),
            3 => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower(CultureInfo.CurrentCulture)),
            _ => throw new XPScriptRuntimeException(5, "Unsupported StrConv conversion value: " + XPScriptRuntime.CStr(conversion))
        };
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

    public static object? CType(object? value, object? targetType)
    {
        var type = XPScriptRuntime.CStr(targetType).Trim().ToLowerInvariant();
        return type switch
        {
            "variant" => value,
            "string" => XPScriptRuntime.CStr(value),
            "boolean" or "bool" => XPScriptRuntime.CBool(value),
            "byte" => XPScriptRuntime.CByte(value),
            "integer" or "int" => XPScriptRuntime.CInt(value),
            "long" => XPScriptRuntime.CLng(value),
            "single" => XPScriptRuntime.CSng(value),
            "double" => XPScriptRuntime.CDbl(value),
            "currency" => XPScriptRuntime.CCur(value),
            "date" => XPScriptRuntime.CDate(value),
            "object" => value,
            _ => throw new XPScriptRuntimeException(13, "Unsupported CType target type: " + XPScriptRuntime.CStr(targetType))
        };
    }

    public static DateTime CVDate(object? value) => XPScriptRuntime.CDate(value);

    public static bool IsList(object? value)
    {
        if (value is null) return false;
        var type = value.GetType();
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(LSList<>);
    }

    public static bool IsUnknown(object? value) => value is null;

    public static bool RegexValidate(object? source, object? pattern)
    {
        var regex = CompileRegex(pattern);
        return regex.IsMatch(XPScriptRuntime.CStr(source));
    }

    public static LSArray RegexMatch(object? source, object? pattern)
    {
        var regex = CompileRegex(pattern);
        var matches = regex.Matches(XPScriptRuntime.CStr(source));
        if (matches.Count == 0)
            return new LSArray("String", true);

        var result = new LSArray("String", false, [0], [matches.Count - 1]);
        for (var index = 0; index < matches.Count; index++)
            result.Set(matches[index].Value, index);
        return result;
    }

    private static System.Text.RegularExpressions.Regex CompileRegex(object? pattern)
    {
        var value = XPScriptRuntime.CStr(pattern);
        if (value.Length > 4096)
            throw new XPScriptRuntimeException(5, "Regex pattern must contain at most 4096 characters.");
        if (value.Any(char.IsControl))
            throw new XPScriptRuntimeException(5, "Regex pattern contains a control character.");
        try
        {
            return new System.Text.RegularExpressions.Regex(
                value,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                RegexTimeout);
        }
        catch (ArgumentException ex)
        {
            throw new XPScriptRuntimeException(5, "Regex pattern is invalid: " + ex.Message);
        }
    }

    public static string Base64Encode(object? value) => XPScriptTextIO.ToBase64(value);
    public static string Base64Encode(object? value, object? charset) => XPScriptTextIO.ToBase64(value, charset);
    public static string Base64Decode(object? value) => XPScriptTextIO.FromBase64(value);
    public static string Base64Decode(object? value, object? charset) => XPScriptTextIO.FromBase64(value, charset);

    // Raw binary form. The return value is an XPScript Byte array using the normal LSArray runtime,
    // so LBound/UBound, IsArray and indexed access can use the same semantics as other arrays.
    public static LSArray Base64DecodeBinary(object? value)
    {
        var bytes = Convert.FromBase64String(XPScriptRuntime.CStr(value).Trim());
        if (bytes.Length == 0)
            return new LSArray("Byte", true);

        var result = new LSArray("Byte", false, [0], [bytes.Length - 1]);
        for (var index = 0; index < bytes.Length; index++)
            result.Set(bytes[index], index);
        return result;
    }
}
""";
}
