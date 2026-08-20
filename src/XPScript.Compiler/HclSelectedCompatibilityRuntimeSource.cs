namespace XPScript.Compiler;

internal static class HclSelectedCompatibilityRuntimeSource
{
    public const string Code = """
internal static class LSHclSelectedRuntime
{
    private static Random _random = new();
    private static float _lastRandom;
    private static bool _hasLastRandom;

    public static object ArrayReplace(object? sourceArray, object? compareArray, object? replaceArray)
    {
        if (sourceArray is LSArray source)
        {
            if (!source.IsAllocated) throw new XPScriptRuntimeException(9, "ArrayReplace source array is not allocated.");
            var result = new LSArray(source.ElementType, true);
            result.ReDim(source.LowerBounds.ToArray(), source.UpperBounds.ToArray(), false);
            CopyReplace(source, result, compareArray, replaceArray);
            return result;
        }

        if (sourceArray is System.Array clr)
        {
            if (clr.Rank != 1) throw new XPScriptRuntimeException(5, "ArrayReplace currently requires a one-dimensional source array.");
            var lower = clr.GetLowerBound(0);
            var upper = clr.GetUpperBound(0);
            var output = new object?[upper - lower + 1];
            for (var i = lower; i <= upper; i++)
                output[i - lower] = ReplacementFor(clr.GetValue(i), compareArray, replaceArray);
            return output;
        }

        throw new XPScriptRuntimeException(13, "ArrayReplace sourceArray must be an array.");
    }

    private static void CopyReplace(LSArray source, LSArray result, object? compareArray, object? replaceArray)
    {
        if (source.Rank != 1) throw new XPScriptRuntimeException(5, "ArrayReplace currently requires a one-dimensional source array.");
        for (var i = source.LBound(); i <= source.UBound(); i++)
            result.Set(ReplacementFor(source.Get(i), compareArray, replaceArray), i);
    }

    private static object? ReplacementFor(object? value, object? compareArray, object? replaceArray)
    {
        var compare = ToValues(compareArray);
        var replace = ToValues(replaceArray);
        for (var i = 0; i < compare.Count; i++)
        {
            if (!ValuesEqual(value, compare[i])) continue;
            return i < replace.Count ? replace[i] : value;
        }
        return value;
    }

    private static List<object?> ToValues(object? value)
    {
        if (value is LSArray array)
        {
            if (!array.IsAllocated || array.Rank != 1) throw new XPScriptRuntimeException(5, "ArrayReplace compare/replace arrays must be allocated one-dimensional arrays.");
            var values = new List<object?>();
            for (var i = array.LBound(); i <= array.UBound(); i++) values.Add(array.Get(i));
            return values;
        }
        if (value is System.Array clr)
        {
            if (clr.Rank != 1) throw new XPScriptRuntimeException(5, "ArrayReplace compare/replace arrays must be one-dimensional arrays.");
            return clr.Cast<object?>().ToList();
        }
        return [value];
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        if (XPScriptNullRuntime.IsNull(left) || XPScriptNullRuntime.IsNull(right))
            return XPScriptNullRuntime.IsNull(left) && XPScriptNullRuntime.IsNull(right);
        if (left is null || right is null) return left is null && right is null;
        if (XPScriptRuntime.IsNumeric(left) && XPScriptRuntime.IsNumeric(right))
            return XPScriptRuntime.CDbl(left).Equals(XPScriptRuntime.CDbl(right));
        return string.Equals(XPScriptRuntime.CStr(left), XPScriptRuntime.CStr(right), StringComparison.CurrentCulture);
    }

    public static object FullTrim(object? value)
    {
        if (XPScriptNullRuntime.IsNull(value)) return XPScriptNullRuntime.NullValue;
        if (value is LSArray array)
        {
            if (!array.IsAllocated || array.Rank != 1) throw new XPScriptRuntimeException(5, "FullTrim requires a one-dimensional array.");
            var cleaned = new List<string>();
            for (var i = array.LBound(); i <= array.UBound(); i++)
            {
                var text = CollapseWhitespace(XPScriptRuntime.CStr(array.Get(i)));
                if (text.Length > 0) cleaned.Add(text);
            }
            var result = new LSArray("String", true);
            if (cleaned.Count == 0) return result;
            result.ReDim([0], [cleaned.Count - 1], false);
            for (var i = 0; i < cleaned.Count; i++) result.Set(cleaned[i], i);
            return result;
        }
        if (value is System.Array clr)
        {
            var cleaned = clr.Cast<object?>().Select(x => CollapseWhitespace(XPScriptRuntime.CStr(x))).Where(x => x.Length > 0).ToArray();
            return cleaned;
        }
        return CollapseWhitespace(XPScriptRuntime.CStr(value));
    }

    private static string CollapseWhitespace(string value) =>
        string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string Implode(object? value, object? delimiter = null)
    {
        var separator = delimiter is null ? " " : XPScriptRuntime.CStr(delimiter);
        if (value is LSArray array)
        {
            if (!array.IsAllocated || array.Rank != 1) throw new XPScriptRuntimeException(5, "Implode requires an allocated one-dimensional array.");
            var parts = new List<string>();
            for (var i = array.LBound(); i <= array.UBound(); i++) parts.Add(XPScriptRuntime.CStr(array.Get(i)));
            return string.Join(separator, parts);
        }
        if (value is System.Collections.IEnumerable enumerable && value is not string)
            return string.Join(separator, enumerable.Cast<object?>().Select(XPScriptRuntime.CStr));
        throw new XPScriptRuntimeException(13, "Implode requires an array.");
    }

    public static string CurDrive()
    {
        if (!OperatingSystem.IsWindows()) return "";
        var root = Path.GetPathRoot(Environment.CurrentDirectory) ?? "";
        return root.Length >= 2 && root[1] == ':' ? root[..2] : "";
    }

    public static object? CreateObject(object? className)
    {
        if (!OperatingSystem.IsWindows()) throw new XPScriptRuntimeException(5, "CreateObject is supported only on Windows.");
        var progId = XPScriptRuntime.CStr(className).Trim();
        if (progId.Length == 0) throw new XPScriptRuntimeException(5, "CreateObject requires an OLE ProgID.");
        try
        {
            var type = Type.GetTypeFromProgID(progId, throwOnError: true)
                ?? throw new COMException("OLE class was not found.");
            return Activator.CreateInstance(type)
                ?? throw new COMException("OLE object could not be created.");
        }
        catch (Exception ex) when (ex is not XPScriptRuntimeException)
        {
            throw new XPScriptRuntimeException(5, "CreateObject failed for OLE class '" + progId + "': " + ex.Message);
        }
    }

    public static float Rnd() => NextRandom();

    public static float Rnd(object? value)
    {
        var number = XPScriptRuntime.CDbl(value);
        if (number == 0d) return _hasLastRandom ? _lastRandom : NextRandom();
        if (number < 0d)
        {
            var seed = unchecked((int)Math.Round(number, MidpointRounding.ToEven));
            _random = new Random(seed);
            _hasLastRandom = false;
            return NextRandom();
        }
        return NextRandom();
    }

    public static void Randomize() { _random = new Random(); _hasLastRandom = false; }
    public static void Randomize(object? seed) { _random = new Random(XPScriptRuntime.CInt(seed)); _hasLastRandom = false; }

    private static float NextRandom()
    {
        var value = (float)_random.NextDouble();
        if (value <= 0f) value = float.Epsilon;
        if (value >= 1f) value = MathF.BitDecrement(1f);
        _lastRandom = value;
        _hasLastRandom = true;
        return value;
    }

    public static int Len(object? value)
    {
        if (XPScriptNullRuntime.IsNull(value)) throw new XPScriptRuntimeException(94, "Invalid use of Null.");
        if (value is null) return 0;
        if (value is string text) return text.Length;
        return LenB(value);
    }

    public static int LenB(object? value)
    {
        if (XPScriptNullRuntime.IsNull(value)) throw new XPScriptRuntimeException(94, "Invalid use of Null.");
        return value switch
        {
            null => 0,
            string text => checked(text.Length * 2),
            byte => 1,
            bool => 2,
            short or int => 2,
            long => 4,
            float => 4,
            double => 8,
            decimal => 8,
            DateTime => 8,
            _ => throw new XPScriptRuntimeException(13, "Len/LenB does not support this value type.")
        };
    }

    public static string UString(object? countValue, object? character)
    {
        var count = Convert.ToInt32(Math.Round(XPScriptRuntime.CDbl(countValue), MidpointRounding.ToEven));
        if (count < 0) throw new XPScriptRuntimeException(5, "UString length cannot be negative.");
        char unit;
        if (character is string text)
        {
            if (text.Length == 0) return "";
            unit = text[0];
        }
        else
        {
            var code = Convert.ToInt32(Math.Round(XPScriptRuntime.CDbl(character), MidpointRounding.ToEven));
            if (code < 0 || code > 65535) throw new XPScriptRuntimeException(5, "UString character code must be between 0 and 65535.");
            unit = (char)code;
        }
        return new string(unit, count);
    }
}
""";
}
