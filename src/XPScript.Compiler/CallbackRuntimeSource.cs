namespace XPScript.Compiler;

internal static class CallbackRuntimeSource
{
    public const string Code = """
internal static class XPScriptCallbackRuntime
{
    private const int MaxCallbackArguments = 64;
    private const int MaxCallbackNameLength = 256;

    public static object? Invoke(object? callbackNameValue, string operation, params object?[] arguments)
    {
        var callbackName = XPScriptRuntime.CStr(callbackNameValue).Trim();
        ValidateCallbackName(callbackName);
        arguments ??= [];
        if (arguments.Length > MaxCallbackArguments)
            throw new XPScriptRuntimeException(5, $"{operation} callback exceeds the {MaxCallbackArguments}-argument limit.");

        var scriptType = typeof(XPScriptCallbackRuntime).Assembly.GetType("Script", throwOnError: false, ignoreCase: false);
        if (scriptType is null)
            throw new XPScriptRuntimeException(5, $"{operation} callback target is unavailable.");

        var candidates = scriptType
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Where(method => method.Name.Equals(callbackName, StringComparison.OrdinalIgnoreCase))
            .Where(method => method.GetParameters().Length == arguments.Length)
            .ToArray();

        if (candidates.Length == 0)
            throw new XPScriptRuntimeException(5, $"{operation} callback '{callbackName}' was not found with {arguments.Length} parameter(s).");

        System.Reflection.MethodInfo? selected = null;
        object?[]? converted = null;
        foreach (var candidate in candidates)
        {
            if (!TryConvertArguments(candidate.GetParameters(), arguments, out var current)) continue;
            if (selected is not null)
                throw new XPScriptRuntimeException(5, $"{operation} callback '{callbackName}' is ambiguous for the supplied parameters.");
            selected = candidate;
            converted = current;
        }

        if (selected is null || converted is null)
            throw new XPScriptRuntimeException(5, $"{operation} callback '{callbackName}' has an incompatible parameter signature.");

        try
        {
            return selected.Invoke(null, converted);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is XPScriptRuntimeException runtime)
        {
            throw runtime;
        }
        catch (System.Reflection.TargetInvocationException)
        {
            throw new XPScriptRuntimeException(5, $"{operation} callback '{callbackName}' failed.");
        }
        catch (Exception)
        {
            throw new XPScriptRuntimeException(5, $"{operation} callback '{callbackName}' could not be invoked.");
        }
    }

    public static object?[] Prepend(object? first, object?[]? trailing)
    {
        trailing ??= [];
        if (trailing.Length >= MaxCallbackArguments)
            throw new XPScriptRuntimeException(5, $"Callback exceeds the {MaxCallbackArguments}-argument limit.");
        var result = new object?[trailing.Length + 1];
        result[0] = first;
        Array.Copy(trailing, 0, result, 1, trailing.Length);
        return result;
    }

    private static bool TryConvertArguments(
        System.Reflection.ParameterInfo[] parameters,
        object?[] arguments,
        out object?[] converted)
    {
        converted = new object?[arguments.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            if (!TryConvertArgument(arguments[index], parameters[index].ParameterType, out converted[index]))
                return false;
        }
        return true;
    }

    private static bool TryConvertArgument(object? value, Type targetType, out object? converted)
    {
        if (targetType == typeof(object))
        {
            converted = value;
            return true;
        }

        if (value is null || XPScriptNullRuntime.IsNull(value))
        {
            if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null)
            {
                converted = null;
                return true;
            }
            converted = null;
            return false;
        }

        if (targetType.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }

        var effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        try
        {
            if (effectiveType == typeof(string))
            {
                converted = XPScriptRuntime.CStr(value);
                return true;
            }
            if (effectiveType == typeof(bool))
            {
                converted = Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            if (effectiveType == typeof(int))
            {
                converted = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            if (effectiveType == typeof(long))
            {
                converted = Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            if (effectiveType == typeof(double))
            {
                converted = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            if (effectiveType == typeof(decimal))
            {
                converted = Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
        }
        catch (Exception)
        {
            converted = null;
            return false;
        }

        converted = null;
        return false;
    }

    private static void ValidateCallbackName(string callbackName)
    {
        if (callbackName.Length == 0 || callbackName.Length > MaxCallbackNameLength ||
            !(char.IsLetter(callbackName[0]) || callbackName[0] == '_'))
            throw new XPScriptRuntimeException(5, "Callback name is invalid.");
        for (var index = 1; index < callbackName.Length; index++)
            if (!(char.IsLetterOrDigit(callbackName[index]) || callbackName[index] == '_'))
                throw new XPScriptRuntimeException(5, "Callback name is invalid.");
    }
}
""";
}
