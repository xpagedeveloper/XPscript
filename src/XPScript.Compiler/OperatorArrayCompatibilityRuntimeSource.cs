namespace XPScript.Compiler;

internal static class OperatorArrayCompatibilityRuntimeSource
{
    public const string Code = """
internal static class LSOperatorArrayRuntime
{
    private static bool _compareNoCase;

    public static void SetCompareNoCase(bool value) => _compareNoCase = value;

    public static LSArray CreateArray(params object?[] items) =>
        FromValues(items, "Variant", 0);

    public static dynamic? LogicalNot(object? value)
    {
        if (value is null) return null;
        if (value is bool b) return !b;
        return ~ToLong(value);
    }

    public static dynamic? LogicalAnd(object? left, object? right)
    {
        if (left is null || right is null) return null;
        if (left is bool lb && right is bool rb) return lb && rb;
        return ToLong(left) & ToLong(right);
    }

    public static dynamic? LogicalOr(object? left, object? right)
    {
        if (left is null || right is null) return null;
        if (left is bool lb && right is bool rb) return lb || rb;
        return ToLong(left) | ToLong(right);
    }

    public static dynamic? Xor(object? left, object? right)
    {
        if (left is null || right is null) return null;
        if (left is bool lb && right is bool rb) return lb ^ rb;
        return ToLong(left) ^ ToLong(right);
    }

    public static dynamic? Eqv(object? left, object? right)
    {
        if (left is null || right is null) return null;
        if (left is bool lb && right is bool rb) return lb == rb;
        return ~(ToLong(left) ^ ToLong(right));
    }

    public static dynamic? Imp(object? left, object? right)
    {
        if (left is null || right is null) return null;
        if (left is bool lb && right is bool rb) return !lb || rb;
        return ~ToLong(left) | ToLong(right);
    }

    public static bool IsSame(object? left, object? right)
    {
        if (left is null || right is null) return ReferenceEquals(left, right);
        return ReferenceEquals(left, right);
    }

    public static double Pow(object? left, object? right)
    {
        if (left is null || right is null) throw new InvalidOperationException("NULL exponentiation result cannot be assigned to a scalar.");
        var basis = XPScriptRuntime.CDbl(left);
        var exponent = XPScriptRuntime.CDbl(right);
        if (basis < 0 && exponent != Math.Truncate(exponent))
            throw new ArgumentException("A negative base requires an integer exponent.");
        return Math.Pow(basis, exponent);
    }

    public static long IntDiv(object? left, object? right)
    {
        if (left is null || right is null) throw new InvalidOperationException("NULL integer division result cannot be assigned to a scalar.");
        var a = Convert.ToInt64(Math.Round(XPScriptRuntime.CDbl(left), MidpointRounding.ToEven));
        var b = Convert.ToInt64(Math.Round(XPScriptRuntime.CDbl(right), MidpointRounding.ToEven));
        if (b == 0) throw new DivideByZeroException();
        return a / b;
    }

    public static bool Like(object? value, object? pattern) => Like(value, pattern, !_compareNoCase);

    public static bool Like(object? value, object? pattern, bool caseSensitive)
    {
        if (value is null || pattern is null) return false;
        var input = XPScriptRuntime.CStr(value);
        var wildcard = XPScriptRuntime.CStr(pattern);
        var regex = new StringBuilder("^");
        for (var i = 0; i < wildcard.Length; i++)
        {
            var c = wildcard[i];
            switch (c)
            {
                case '*': regex.Append(".*"); break;
                case '?': regex.Append('.'); break;
                case '#': regex.Append("[0-9]"); break;
                case '[':
                    var end = FindClassEnd(wildcard, i + 1);
                    if (end < 0) throw new ArgumentException("Invalid Like pattern: unmatched '['.");
                    AppendCharacterClass(regex, wildcard[(i + 1)..end]);
                    i = end;
                    break;
                default: regex.Append(Regex.Escape(c.ToString())); break;
            }
        }
        regex.Append('$');
        var options = RegexOptions.CultureInvariant | RegexOptions.Singleline;
        if (!caseSensitive) options |= RegexOptions.IgnoreCase;
        return Regex.IsMatch(input, regex.ToString(), options);
    }

    private static int FindClassEnd(string pattern, int start)
    {
        for (var i = start; i < pattern.Length; i++) if (pattern[i] == ']') return i;
        return -1;
    }

    private static void AppendCharacterClass(StringBuilder regex, string content)
    {
        if (content.Length == 0) { regex.Append("\\[\\]"); return; }
        regex.Append('[');
        var index = 0;
        if (content[0] == '!') { regex.Append('^'); index = 1; }
        for (; index < content.Length; index++)
        {
            var c = content[index];
            if (c == '-' && index > (content[0] == '!' ? 1 : 0)) regex.Append('-');
            else if (c is '\\' or ']' or '^') regex.Append('\\').Append(c);
            else regex.Append(c);
        }
        regex.Append(']');
    }

    public static string Join(object? source, object? delimiter = null)
    {
        var sep = delimiter is null ? " " : XPScriptRuntime.CStr(delimiter);
        return string.Join(sep, Values(source).Select(XPScriptRuntime.CStr));
    }

    public static LSArray Explode(object? delimiter, object? value)
    {
        var sep = XPScriptRuntime.CStr(delimiter);
        var text = XPScriptRuntime.CStr(value);
        if (sep.Length == 0) throw new ArgumentException("Explode delimiter cannot be empty.");
        return FromValues(text.Split([sep], StringSplitOptions.None).Cast<object?>(), "String", 0);
    }

    public static LSArray ArrayAppend(object? sourceArray, object? source2)
    {
        var first = RequireOneDimensional(sourceArray);
        var values = Values(first).ToList();
        if (source2 is LSArray second) values.AddRange(Values(RequireOneDimensional(second)));
        else values.Add(source2);
        return FromValues(values, "Variant", first.LBound());
    }

    public static object? ArrayGetIndex(object? sourceArray, object? searchValue) => ArrayGetIndex(sourceArray, searchValue, _compareNoCase ? 1 : 0);

    public static object? ArrayGetIndex(object? sourceArray, object? searchValue, object? compMethod)
    {
        var array = RequireOneDimensional(sourceArray);
        var comparison = (XPScriptRuntime.CInt(compMethod) & 1) != 0 ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;
        var target = XPScriptRuntime.CStr(searchValue);
        for (var i = array.LBound(); i <= array.UBound(); i++)
        {
            var item = array.Get(i);
            try { if (string.Equals(XPScriptRuntime.CStr(item), target, comparison)) return (long)i; }
            catch { }
        }
        return null;
    }

    public static LSArray ArrayUnique(object? sourceArray) => ArrayUnique(sourceArray, _compareNoCase ? 1 : 0);

    public static LSArray ArrayUnique(object? sourceArray, object? compMethod)
    {
        var array = RequireOneDimensional(sourceArray);
        var ignoreCase = (XPScriptRuntime.CInt(compMethod) & 1) != 0;
        var result = new List<object?>();
        foreach (var value in Values(array))
            if (!result.Any(existing => Equivalent(existing, value, ignoreCase))) result.Add(value);
        return FromValues(result, array.ElementType, array.LBound());
    }

    public static LSArray ArraySplice(object? sourceArray, object? start) => ArraySplice(sourceArray, start, int.MaxValue, Array.Empty<object?>());
    public static LSArray ArraySplice(object? sourceArray, object? start, object? deleteCount) => ArraySplice(sourceArray, start, deleteCount, Array.Empty<object?>());
    public static LSArray ArraySplice(object? sourceArray, object? start, object? deleteCount, params object?[] items)
    {
        var array = RequireOneDimensional(sourceArray);
        var values = Values(array).ToList();
        var position = NormalizeStart(XPScriptRuntime.CInt(start), values.Count);
        var requested = XPScriptRuntime.CInt(deleteCount);
        var remove = requested == int.MaxValue ? values.Count - position : Math.Clamp(requested, 0, values.Count - position);
        var removed = values.GetRange(position, remove);
        values.RemoveRange(position, remove);
        if (items.Length > 0) values.InsertRange(position, items);
        ReplaceContents(array, values);
        return FromValues(removed, array.ElementType, 0);
    }

    public static LSArray ArraySlice(object? sourceArray) => ArraySlice(sourceArray, 0, int.MaxValue);
    public static LSArray ArraySlice(object? sourceArray, object? start) => ArraySlice(sourceArray, start, int.MaxValue);
    public static LSArray ArraySlice(object? sourceArray, object? start, object? end)
    {
        var array = RequireOneDimensional(sourceArray);
        var values = Values(array).ToList();
        var from = NormalizeStart(XPScriptRuntime.CInt(start), values.Count);
        var rawEnd = XPScriptRuntime.CInt(end);
        var to = rawEnd == int.MaxValue ? values.Count : NormalizeEnd(rawEnd, values.Count);
        if (to < from) to = from;
        return FromValues(values.Skip(from).Take(to - from), array.ElementType, 0);
    }

    private static int NormalizeStart(int start, int length) => start < 0 ? Math.Max(length + start, 0) : Math.Min(start, length);
    private static int NormalizeEnd(int end, int length) => end < 0 ? Math.Max(length + end, 0) : Math.Min(end, length);

    private static void ReplaceContents(LSArray array, IReadOnlyList<object?> values)
    {
        var lower = array.LBound();
        if (values.Count == 0)
        {
            if (!array.IsDynamic) throw new InvalidOperationException("ArraySplice cannot reduce a fixed array to zero elements.");
            array.Erase();
            return;
        }
        array.ReDim([lower], [checked(lower + values.Count - 1)], false);
        for (var i = 0; i < values.Count; i++) array.Set(values[i], lower + i);
    }

    private static LSArray FromValues(IEnumerable<object?> source, string type, int lower)
    {
        var values = source.ToList();
        var array = new LSArray(type, true);
        if (values.Count == 0) return array;
        array.ReDim([lower], [checked(lower + values.Count - 1)], false);
        for (var i = 0; i < values.Count; i++) array.Set(values[i], lower + i);
        return array;
    }

    private static IEnumerable<object?> Values(object? source)
    {
        if (source is LSArray array)
        {
            if (!array.IsAllocated) yield break;
            if (array.Rank != 1) throw new InvalidOperationException("Array function requires a one-dimensional array.");
            for (var i = array.LBound(); i <= array.UBound(); i++) yield return array.Get(i);
            yield break;
        }
        if (source is System.Collections.IEnumerable enumerable && source is not string)
        {
            foreach (var value in enumerable) yield return value;
            yield break;
        }
        throw new InvalidOperationException("Value is not an array.");
    }

    private static LSArray RequireOneDimensional(object? source)
    {
        var array = source as LSArray ?? throw new InvalidOperationException("Value is not an XPScript array.");
        if (!array.IsAllocated) throw new InvalidOperationException("Array has not been initialized.");
        if (array.Rank != 1) throw new InvalidOperationException("Array function requires a one-dimensional array.");
        return array;
    }

    private static bool Equivalent(object? left, object? right, bool ignoreCase)
    {
        if (left is null || right is null) return left is null && right is null;
        if (left.GetType() != right.GetType()) return false;
        if (left is string ls && right is string rs)
            return string.Equals(ls, rs, ignoreCase ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture);
        return Equals(left, right);
    }

    private static long ToLong(object? value) => Convert.ToInt64(value, CultureInfo.CurrentCulture);
}
""";
}
