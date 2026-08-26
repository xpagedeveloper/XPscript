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

    private static bool IsVariantNull(object? value) => ReferenceEquals(value, System.DBNull.Value);
    private static bool IsObjectNothing(object? value) => value is ILSObjectReference reference && reference.IsNothing;

    public static bool IsNull(object? value) => IsVariantNull(value) || IsObjectNothing(value);
    public static bool IsEmpty(object? value) => value is null;
    public static bool IsObject(object? value) => IsVariantNull(value) ? false : XPScriptRuntime.IsObject(value);
    public static bool IsScalar(object? value) => IsVariantNull(value) ? true : XPScriptRuntime.IsScalar(value);

    public static int DataType(object? value) => IsVariantNull(value) ? 1 : XPScriptRuntime.DataType(value);
    public static string TypeName(object? value) => IsVariantNull(value) ? "NULL" : XPScriptRuntime.TypeName(value);

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
                return ToDouble(left, "addition") + parsed;
            return XPScriptRuntime.CStr(left) + rightText;
        }

        return ToDouble(left, "addition") + ToDouble(right, "addition");
    }

    public static object? ConcatVariant(object? left, object? right)
    {
        var leftNull = XPScriptNullRuntime.IsNull(left);
        var rightNull = XPScriptNullRuntime.IsNull(right);
        if (leftNull && rightNull) return XPScriptNullRuntime.NullValue;

        var leftText = leftNull || left is null ? string.Empty : XPScriptRuntime.CStr(left);
        var rightText = rightNull || right is null ? string.Empty : XPScriptRuntime.CStr(right);
        return leftText + rightText;
    }

    public static object? SubtractVariant(object? left, object? right)
    {
        if (XPScriptNullRuntime.IsNull(left) || XPScriptNullRuntime.IsNull(right)) return XPScriptNullRuntime.NullValue;
        return ToDouble(left, "subtraction") - ToDouble(right, "subtraction");
    }

    public static object? MultiplyVariant(object? left, object? right)
    {
        if (XPScriptNullRuntime.IsNull(left) || XPScriptNullRuntime.IsNull(right)) return XPScriptNullRuntime.NullValue;
        return ToDouble(left, "multiplication") * ToDouble(right, "multiplication");
    }

    public static object? DivideVariant(object? left, object? right)
    {
        if (XPScriptNullRuntime.IsNull(left) || XPScriptNullRuntime.IsNull(right)) return XPScriptNullRuntime.NullValue;
        var divisor = ToDouble(right, "division");
        if (divisor == 0d) throw new XPScriptRuntimeException(11, "Division by zero.");
        return ToDouble(left, "division") / divisor;
    }

    public static object? IntegerDivideVariant(object? left, object? right)
    {
        if (XPScriptNullRuntime.IsNull(left) || XPScriptNullRuntime.IsNull(right)) return XPScriptNullRuntime.NullValue;
        var dividend = ToRoundedLong(left, "integer division");
        var divisor = ToRoundedLong(right, "integer division");
        if (divisor == 0) throw new XPScriptRuntimeException(11, "Division by zero.");
        return dividend / divisor;
    }

    public static object? ModVariant(object? left, object? right)
    {
        if (XPScriptNullRuntime.IsNull(left) || XPScriptNullRuntime.IsNull(right)) return XPScriptNullRuntime.NullValue;
        var dividend = ToRoundedLong(left, "Mod");
        var divisor = ToRoundedLong(right, "Mod");
        if (divisor == 0) throw new XPScriptRuntimeException(11, "Division by zero.");
        return dividend % divisor;
    }

    public static object? UnaryPlusVariant(object? value)
    {
        if (XPScriptNullRuntime.IsNull(value)) return XPScriptNullRuntime.NullValue;
        return ToDouble(value, "unary plus");
    }

    public static object? NegateVariant(object? value)
    {
        if (XPScriptNullRuntime.IsNull(value)) return XPScriptNullRuntime.NullValue;
        return -ToDouble(value, "negation");
    }

    public static object? PowerVariant(object? left, object? right)
    {
        if (XPScriptNullRuntime.IsNull(value: left) || XPScriptNullRuntime.IsNull(right)) return XPScriptNullRuntime.NullValue;
        var number = ToDouble(left, "exponentiation");
        var exponent = ToDouble(right, "exponentiation");
        if ((number == 0d && exponent <= 0d) || (number < 0d && exponent != Math.Truncate(exponent)))
            throw new XPScriptRuntimeException(5, "Invalid ^ operator operands.");
        var result = Math.Pow(number, exponent);
        if (double.IsInfinity(result)) throw new XPScriptRuntimeException(6, "Overflow.");
        if (double.IsNaN(result)) throw new XPScriptRuntimeException(5, "Invalid ^ operator operands.");
        return result;
    }

    public static object? RelateVariant(object? left, string operation, object? right)
    {
        if (XPScriptNullRuntime.IsNull(left) || XPScriptNullRuntime.IsNull(right)) return XPScriptNullRuntime.NullValue;

        int comparison;
        var numericComparison = (left is null || XPScriptRuntime.IsNumeric(left)) &&
                                (right is null || XPScriptRuntime.IsNumeric(right));
        if (numericComparison)
        {
            comparison = ToDouble(left, "comparison").CompareTo(ToDouble(right, "comparison"));
        }
        else if (left is DateTime || right is DateTime)
        {
            comparison = XPScriptRuntime.CDat(left).CompareTo(XPScriptRuntime.CDat(right));
        }
        else
        {
            comparison = string.Compare(
                XPScriptRuntime.CStr(left),
                XPScriptRuntime.CStr(right),
                StringComparison.CurrentCulture);
        }

        return operation switch
        {
            "=" => comparison == 0,
            "<>" => comparison != 0,
            "<" => comparison < 0,
            "<=" => comparison <= 0,
            ">" => comparison > 0,
            ">=" => comparison >= 0,
            _ => throw new XPScriptRuntimeException(5, "Unsupported relational operator.")
        };
    }

    private static decimal AddDecimal(object? left, object? right)
    {
        return ToDecimal(left) + ToDecimal(right);
    }

    private static double AddDoubleCore(object? left, object? right)
    {
        return ToDouble(left, "addition") + ToDouble(right, "addition");
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

    private static double ToDouble(object? value, string operation)
    {
        if (XPScriptNullRuntime.IsNull(value)) throw new InvalidCastException($"Unable to convert Null to a numeric value for {operation}.");
        if (value is null) return 0d;
        if (value is string text)
        {
            if (TryDouble(text, out var number)) return number;
            throw new InvalidCastException($"Unable to convert String value '{text}' to a numeric value for {operation}.");
        }
        try { return Convert.ToDouble(value, System.Globalization.CultureInfo.CurrentCulture); }
        catch (Exception ex) { throw new InvalidCastException($"Unable to convert {value.GetType().Name} to a numeric value for {operation}.", ex); }
    }

    private static long ToRoundedLong(object? value, string operation)
    {
        try
        {
            return checked(Convert.ToInt64(ToDouble(value, operation)));
        }
        catch (OverflowException ex)
        {
            throw new XPScriptRuntimeException(6, ex.Message);
        }
    }

    private static bool TryDouble(string text, out double value)
    {
        if (double.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out value)) return true;
        return double.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
""" + IncrementRuntimeSource.Code;
}