namespace XPScript.Compiler;

internal static class TypeCoercionRuntimeSource
{
    public const string Code = """
internal static class XPScriptCoercion
{
    public static byte AddByte(object? left, object? right) => checked((byte)AddDecimal(left, right));
    public static int AddInteger(object? left, object? right) => checked((int)AddDecimal(left, right));
    public static long AddLong(object? left, object? right) => checked((long)AddDecimal(left, right));
    public static float AddSingle(object? left, object? right) => checked((float)AddDoubleCore(left, right));
    public static double AddDouble(object? left, object? right) => AddDoubleCore(left, right);
    public static decimal AddCurrency(object? left, object? right) => AddDecimal(left, right);

    // Shared forgiving Variant-style addition used when the target type is not statically known.
    // String on the left always means concatenation. A String on the right is treated as a
    // number when it can be parsed using XPScript's normal current/invariant numeric rules,
    // otherwise it is concatenated. Null propagates for dynamic expressions.
    public static object? AddVariant(object? left, object? right)
    {
        if (left is null || right is null) return null;
        if (left is string leftText) return leftText + XPScriptRuntime.CStr(right);

        if (right is string rightText)
        {
            if (TryDouble(rightText, out var parsed))
                return ToDouble(left) + parsed;
            return XPScriptRuntime.CStr(left) + rightText;
        }

        return ToDouble(left) + ToDouble(right);
    }

    private static decimal AddDecimal(object? left, object? right)
    {
        return ToDecimal(left) + ToDecimal(right);
    }

    private static double AddDoubleCore(object? left, object? right)
    {
        return ToDouble(left) + ToDouble(right);
    }

    private static decimal ToDecimal(object? value)
    {
        if (value is null) return 0m;
        if (value is string text)
        {
            if (decimal.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out var current)) return current;
            if (decimal.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var invariant)) return invariant;
            throw new InvalidCastException($"Unable to convert String value '{text}' to a numeric value for addition.");
        }
        try { return Convert.ToDecimal(value, System.Globalization.CultureInfo.CurrentCulture); }
        catch (Exception ex) { throw new InvalidCastException($"Unable to convert {value.GetType().Name} to a numeric value for addition.", ex); }
    }

    private static double ToDouble(object? value)
    {
        if (value is null) return 0d;
        if (value is string text)
        {
            if (TryDouble(text, out var number)) return number;
            throw new InvalidCastException($"Unable to convert String value '{text}' to a numeric value for addition.");
        }
        try { return Convert.ToDouble(value, System.Globalization.CultureInfo.CurrentCulture); }
        catch (Exception ex) { throw new InvalidCastException($"Unable to convert {value.GetType().Name} to a numeric value for addition.", ex); }
    }

    private static bool TryDouble(string text, out double value)
    {
        if (double.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out value)) return true;
        return double.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
""";
}