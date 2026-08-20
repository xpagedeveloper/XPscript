namespace XPScript.Compiler;

internal static class HclArrayReplaceRuntimeSource
{
    public const string Code = """
internal static class LSHclArrayRuntime
{
    public static object ArrayReplace(object? sourceArray, object? compareArray, object? replaceArray)
    {
        var compare = ToValues(compareArray);
        var replace = ToValues(replaceArray);

        if (sourceArray is LSArray source)
        {
            if (!source.IsAllocated) throw new XPScriptRuntimeException(9, "ArrayReplace source array is not allocated.");
            var elementType = ResultElementType(source.ElementType, replace);
            var result = new LSArray(elementType, true);
            result.ReDim(source.LowerBounds.ToArray(), source.UpperBounds.ToArray(), false);
            VisitLsArray(source, indices => result.Set(ReplacementFor(source.Get(indices.Cast<object?>().ToArray()), compare, replace), indices.Cast<object?>().ToArray()));
            return result;
        }

        if (sourceArray is System.Array clr)
        {
            var lengths = Enumerable.Range(0, clr.Rank).Select(clr.GetLength).ToArray();
            var lower = Enumerable.Range(0, clr.Rank).Select(clr.GetLowerBound).ToArray();
            var result = System.Array.CreateInstance(typeof(object), lengths, lower);
            VisitClrArray(clr, indices => result.SetValue(ReplacementFor(clr.GetValue(indices), compare, replace), indices));
            return result;
        }

        throw new XPScriptRuntimeException(13, "ArrayReplace sourceArray must be an array.");
    }

    private static string ResultElementType(string sourceElementType, IReadOnlyList<object?> replacements)
    {
        if (sourceElementType.Equals("Variant", StringComparison.OrdinalIgnoreCase)) return "Variant";
        var canonicalSource = sourceElementType.ToUpperInvariant();
        foreach (var replacement in replacements)
        {
            if (replacement is null || XPScriptNullRuntime.IsNull(replacement)) return "Variant";
            var type = XPScriptRuntime.TypeName(replacement).ToUpperInvariant();
            if (!EquivalentType(canonicalSource, type)) return "Variant";
        }
        return sourceElementType;
    }

    private static bool EquivalentType(string source, string runtime) => source switch
    {
        "INTEGER" => runtime == "INTEGER",
        "LONG" => runtime == "LONG",
        "SINGLE" => runtime == "SINGLE",
        "DOUBLE" => runtime == "DOUBLE",
        "CURRENCY" => runtime == "CURRENCY",
        "BYTE" => runtime == "BYTE",
        "BOOLEAN" => runtime == "BOOLEAN",
        "DATE" => runtime == "DATE",
        "STRING" => runtime == "STRING",
        _ => source == runtime
    };

    private static object? ReplacementFor(object? value, IReadOnlyList<object?> compare, IReadOnlyList<object?> replace)
    {
        for (var i = 0; i < compare.Count; i++)
        {
            if (!ValuesEqual(value, compare[i])) continue;
            return i < replace.Count ? replace[i] : value;
        }
        return value;
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        if (XPScriptNullRuntime.IsNull(left) || XPScriptNullRuntime.IsNull(right))
            return XPScriptNullRuntime.IsNull(left) && XPScriptNullRuntime.IsNull(right);
        if (left is null || right is null) return left is null && right is null;
        if (XPScriptRuntime.IsNumeric(left) && XPScriptRuntime.IsNumeric(right))
            return XPScriptRuntime.CDbl(left).Equals(XPScriptRuntime.CDbl(right));
        if (left is DateTime || right is DateTime)
        {
            try { return XPScriptRuntime.CDat(left).Equals(XPScriptRuntime.CDat(right)); }
            catch { return false; }
        }
        return string.Equals(XPScriptRuntime.CStr(left), XPScriptRuntime.CStr(right), StringComparison.CurrentCulture);
    }

    private static List<object?> ToValues(object? value)
    {
        if (value is LSArray ls)
        {
            if (!ls.IsAllocated) throw new XPScriptRuntimeException(9, "ArrayReplace compare/replace array is not allocated.");
            var values = new List<object?>();
            VisitLsArray(ls, indices => values.Add(ls.Get(indices.Cast<object?>().ToArray())));
            return values;
        }
        if (value is System.Array array)
        {
            var values = new List<object?>();
            VisitClrArray(array, indices => values.Add(array.GetValue(indices)));
            return values;
        }
        return [value];
    }

    private static void VisitLsArray(LSArray array, Action<int[]> visitor)
    {
        var indices = new int[array.Rank];
        Visit(0);
        void Visit(int dimension)
        {
            if (dimension == array.Rank) { visitor(indices.ToArray()); return; }
            for (var i = array.LowerBounds[dimension]; i <= array.UpperBounds[dimension]; i++)
            {
                indices[dimension] = i;
                Visit(dimension + 1);
            }
        }
    }

    private static void VisitClrArray(System.Array array, Action<int[]> visitor)
    {
        var indices = new int[array.Rank];
        Visit(0);
        void Visit(int dimension)
        {
            if (dimension == array.Rank) { visitor(indices.ToArray()); return; }
            for (var i = array.GetLowerBound(dimension); i <= array.GetUpperBound(dimension); i++)
            {
                indices[dimension] = i;
                Visit(dimension + 1);
            }
        }
    }
}
""";
}
