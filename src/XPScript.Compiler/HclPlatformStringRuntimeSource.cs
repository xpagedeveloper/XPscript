namespace XPScript.Compiler;

internal static class HclPlatformStringRuntimeSource
{
    public const string Code = """
internal static class LSHclPlatformStringRuntime
{
    private const System.Reflection.BindingFlags StaticAny = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

    public static int LenBP(object? value)
    {
        if (XPScriptNullRuntime.IsNull(value)) throw new XPScriptRuntimeException(94, "Invalid use of Null.");
        if (value is string text) return PlatformEncoding().GetByteCount(text);
        return LSHclSelectedRuntime.LenB(value);
    }

    public static string LeftBP(object? value, object? countValue)
    {
        if (XPScriptNullRuntime.IsNull(value)) return null!;
        var text = XPScriptRuntime.CStr(value);
        var count = Math.Max(0, XPScriptRuntime.CInt(countValue));
        var bytes = PlatformEncoding().GetBytes(text);
        if (count >= bytes.Length) return text;
        return DecodeSlice(bytes, 0, count, trimStart: false, trimEnd: true);
    }

    public static string RightBP(object? value, object? countValue)
    {
        if (XPScriptNullRuntime.IsNull(value)) return null!;
        var text = XPScriptRuntime.CStr(value);
        var count = Math.Max(0, XPScriptRuntime.CInt(countValue));
        var bytes = PlatformEncoding().GetBytes(text);
        if (count >= bytes.Length) return text;
        return DecodeSlice(bytes, bytes.Length - count, count, trimStart: true, trimEnd: false);
    }

    public static string MidBP(object? value, object? startValue) => MidBP(value, startValue, int.MaxValue);

    public static string MidBP(object? value, object? startValue, object? countValue)
    {
        if (XPScriptNullRuntime.IsNull(value)) return null!;
        var text = XPScriptRuntime.CStr(value);
        var start = Math.Max(1, XPScriptRuntime.CInt(startValue));
        var bytes = PlatformEncoding().GetBytes(text);
        if (start > bytes.Length) return "";
        var offset = start - 1;
        var count = countValue is int i && i == int.MaxValue ? bytes.Length - offset : Math.Max(0, XPScriptRuntime.CInt(countValue));
        count = Math.Min(count, bytes.Length - offset);
        return DecodeSlice(bytes, offset, count, trimStart: true, trimEnd: true);
    }

    public static int InStrBP(object? source, object? find) => InStrBP(1, source, find, 0);
    public static int InStrBP(object? start, object? source, object? find) => InStrBP(start, source, find, 0);

    public static int InStrBP(object? startValue, object? sourceValue, object? findValue, object? compareValue)
    {
        if (XPScriptNullRuntime.IsNull(sourceValue) || XPScriptNullRuntime.IsNull(findValue)) return 0;
        var encoding = PlatformEncoding();
        var sourceText = XPScriptRuntime.CStr(sourceValue);
        var findText = XPScriptRuntime.CStr(findValue);
        var compare = XPScriptRuntime.CInt(compareValue);
        var start = Math.Max(1, XPScriptRuntime.CInt(startValue));

        if (compare == 1)
        {
            sourceText = sourceText.ToUpper(CultureInfo.CurrentCulture);
            findText = findText.ToUpper(CultureInfo.CurrentCulture);
        }

        var source = encoding.GetBytes(sourceText);
        var find = encoding.GetBytes(findText);
        if (find.Length == 0) return start <= source.Length + 1 ? start : 0;
        var offset = start - 1;
        for (var i = offset; i <= source.Length - find.Length; i++)
        {
            if (source.AsSpan(i, find.Length).SequenceEqual(find)) return i + 1;
        }
        return 0;
    }

    public static int LenC(object? value)
    {
        if (XPScriptNullRuntime.IsNull(value)) throw new XPScriptRuntimeException(94, "Invalid use of Null.");
        return TextElementIndexes(XPScriptRuntime.CStr(value)).Length;
    }

    public static string LeftC(object? value, object? countValue)
    {
        if (XPScriptNullRuntime.IsNull(value)) return null!;
        var text = XPScriptRuntime.CStr(value);
        var count = Math.Max(0, XPScriptRuntime.CInt(countValue));
        var indexes = TextElementIndexes(text);
        if (count >= indexes.Length) return text;
        if (count == 0) return "";
        var end = indexes[count];
        return text[..end];
    }

    public static string RightC(object? value, object? countValue)
    {
        if (XPScriptNullRuntime.IsNull(value)) return null!;
        var text = XPScriptRuntime.CStr(value);
        var count = Math.Max(0, XPScriptRuntime.CInt(countValue));
        var indexes = TextElementIndexes(text);
        if (count >= indexes.Length) return text;
        if (count == 0) return "";
        return text[indexes[indexes.Length - count]..];
    }

    public static string MidC(object? value, object? startValue) => MidC(value, startValue, int.MaxValue);

    public static string MidC(object? value, object? startValue, object? countValue)
    {
        if (XPScriptNullRuntime.IsNull(value)) return null!;
        var text = XPScriptRuntime.CStr(value);
        var indexes = TextElementIndexes(text);
        var start = Math.Max(1, XPScriptRuntime.CInt(startValue));
        if (start > indexes.Length) return "";
        var first = start - 1;
        var count = countValue is int i && i == int.MaxValue ? indexes.Length - first : Math.Max(0, XPScriptRuntime.CInt(countValue));
        if (count == 0) return "";
        var last = Math.Min(indexes.Length, first + count);
        var startChar = indexes[first];
        var endChar = last >= indexes.Length ? text.Length : indexes[last];
        return text[startChar..endChar];
    }

    public static int InStrC(object? source, object? find) => InStrC(1, source, find, 0);
    public static int InStrC(object? start, object? source, object? find) => InStrC(start, source, find, 0);

    public static int InStrC(object? startValue, object? sourceValue, object? findValue, object? compareValue)
    {
        if (XPScriptNullRuntime.IsNull(sourceValue) || XPScriptNullRuntime.IsNull(findValue)) return 0;
        var source = XPScriptRuntime.CStr(sourceValue);
        var find = XPScriptRuntime.CStr(findValue);
        var sourceIndexes = TextElementIndexes(source);
        var start = Math.Max(1, XPScriptRuntime.CInt(startValue));
        if (start > sourceIndexes.Length + 1) return 0;
        if (find.Length == 0) return start;
        var charStart = start <= sourceIndexes.Length ? sourceIndexes[start - 1] : source.Length;
        var comparison = XPScriptRuntime.CInt(compareValue) == 1 ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;
        var charIndex = source.IndexOf(find, charStart, comparison);
        if (charIndex < 0) return 0;
        var column = 1;
        foreach (var index in sourceIndexes)
        {
            if (index >= charIndex) break;
            column++;
        }
        return column;
    }

    public static string InputBP(object? countValue, object? fileNumberValue)
    {
        var count = XPScriptRuntime.CInt(countValue);
        if (count < 0) throw new XPScriptRuntimeException(5, "InputBP count must be zero or greater.");
        if (count == 0) return "";

        var state = InvokePrivate(typeof(XPScriptFileIO), "GetOpenState", XPScriptRuntime.CInt(fileNumberValue))
            ?? throw new IOException("File number is not open.");
        var reader = InvokePrivate(typeof(XPScriptFileIO), "GetReader", state) as TextReader;
        var encoding = PlatformEncoding();

        if (reader is not null)
        {
            var builder = new StringBuilder();
            var bytes = 0;
            while (bytes < count)
            {
                var peek = reader.Peek();
                if (peek < 0) throw new EndOfStreamException("InputBP requested more bytes than remain in the file.");
                var character = ((char)peek).ToString();
                var charBytes = encoding.GetByteCount(character);
                if (bytes + charBytes > count) break;
                reader.Read();
                builder.Append(character);
                bytes += charBytes;
            }
            return builder.ToString();
        }

        var stream = InvokePrivate(typeof(XPScriptFileIO), "GetStream", state) as Stream
            ?? throw new IOException("File is not open for Input or Binary access.");
        var buffer = new byte[count];
        var total = 0;
        while (total < count)
        {
            var read = stream.Read(buffer, total, count - total);
            if (read <= 0) throw new EndOfStreamException("InputBP requested more bytes than remain in the file.");
            total += read;
        }
        return DecodeSlice(buffer, 0, buffer.Length, trimStart: false, trimEnd: true);
    }

    private static object? InvokePrivate(Type type, string name, params object?[] args)
    {
        var method = type.GetMethod(name, StaticAny) ?? throw new MissingMethodException(type.FullName, name);
        try { return method.Invoke(null, args); }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null) { throw ex.InnerException; }
    }

    private static int[] TextElementIndexes(string text) => System.Globalization.StringInfo.ParseCombiningCharacters(text);

    private static Encoding PlatformEncoding()
    {
        if (!OperatingSystem.IsWindows()) return new UTF8Encoding(false, true);
        try
        {
            Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        }
        catch
        {
            return Encoding.Latin1;
        }
    }

    private static string DecodeSlice(byte[] bytes, int offset, int count, bool trimStart, bool trimEnd)
    {
        if (count <= 0) return "";
        var encoding = PlatformEncoding();
        var start = offset;
        var length = count;
        while (length > 0)
        {
            try { return encoding.GetString(bytes, start, length); }
            catch (DecoderFallbackException)
            {
                if (trimEnd && length > 0) { length--; continue; }
                if (trimStart && length > 0) { start++; length--; continue; }
                throw;
            }
        }
        return "";
    }
}
""";
}
