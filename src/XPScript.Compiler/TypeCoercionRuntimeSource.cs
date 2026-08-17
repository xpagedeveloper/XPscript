namespace XPScript.Compiler;

internal static class TypeCoercionRuntimeSource
{
    public const string Code = """
internal static class XPScriptNullRuntime
{
    // Use the CLR-standard database-null marker at managed interop boundaries.
    // This keeps Variant EMPTY (CLR null) and XPScript NULL distinguishable without
    // exposing a private XPScript sentinel type to referenced .NET assemblies.
    public static readonly object NullValue = System.DBNull.Value;

    public static bool IsNull(object? value) => ReferenceEquals(value, System.DBNull.Value);
    public static bool IsEmpty(object? value) => value is null;
    public static bool IsObject(object? value) => IsNull(value) ? false : XPScriptRuntime.IsObject(value);
    public static bool IsScalar(object? value) => IsNull(value) ? true : XPScriptRuntime.IsScalar(value);

    public static int DataType(object? value) => IsNull(value) ? 1 : XPScriptRuntime.DataType(value);
    public static string TypeName(object? value) => IsNull(value) ? "NULL" : XPScriptRuntime.TypeName(value);

    public static bool ConditionValue(object? value)
    {
        if (IsNull(value) || IsEmpty(value)) return false;
        try
        {
            return Convert.ToBoolean(value, System.Globalization.CultureInfo.CurrentCulture);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new InvalidCastException($"Unable to use {TypeName(value)} as a Boolean condition.", ex);
        }
    }
}

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
        if (XPScriptNullRuntime.IsNull(left) || XPScriptNullRuntime.IsNull(right)) return XPScriptNullRuntime.NullValue;
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
        if (XPScriptNullRuntime.IsNull(value)) throw new InvalidCastException("Unable to convert Null to a numeric value for addition.");
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
        if (XPScriptNullRuntime.IsNull(value)) throw new InvalidCastException("Unable to convert Null to a numeric value for addition.");
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
""" + IncrementRuntimeSource.Code;
}