namespace XPScript.Compiler;

internal static class NotesSessionEnvironmentPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string anchor = "    public XPScriptNotesDatabase OpenDatabase(object? serverValue, object? fileValue)";
        const string methods = """
    public string GetEnvironmentString(object? nameValue) => GetEnvironmentString(nameValue, false);

    public string GetEnvironmentString(object? nameValue, object? systemValue)
    {
        EnsureAlive();
        var name = ResolveEnvironmentVariableName(nameValue, systemValue);
        return Api.GetEnvironmentString(name);
    }

    public object? GetEnvironmentValue(object? nameValue) => GetEnvironmentValue(nameValue, false);

    public object? GetEnvironmentValue(object? nameValue, object? systemValue)
    {
        EnsureAlive();
        var text = Api.GetEnvironmentString(ResolveEnvironmentVariableName(nameValue, systemValue));
        if (text.Length == 0) return null;
        if (int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value))
            return value;
        return null;
    }

    public void SetEnvironmentVar(object? nameValue, object? value) => SetEnvironmentVar(nameValue, value, false);

    public void SetEnvironmentVar(object? nameValue, object? value, object? systemValue)
    {
        EnsureAlive();
        var name = ResolveEnvironmentVariableName(nameValue, systemValue);
        Api.SetEnvironmentVariable(name, ResolveEnvironmentVariableValue(value));
    }

    private static string ResolveEnvironmentVariableName(object? nameValue, object? systemValue)
    {
        var name = XPScriptRuntime.CStr(nameValue).Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "NotesSession environment variable name cannot be empty.");
        if (XPScriptRuntime.CBool(systemValue) || name.StartsWith("$", StringComparison.Ordinal)) return name;
        return "$" + name;
    }

    private static string ResolveEnvironmentVariableValue(object? value)
    {
        return value switch
        {
            string text => text,
            sbyte or byte or short or ushort or int or uint or long or ulong => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "",
            DateTime => XPScriptRuntime.CStr(value),
            _ => throw new XPScriptRuntimeException(13, "Environment variables must be strings, dates, or integers.")
        };
    }

""";

        if (!source.Contains(anchor, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesSession environment surface.");
        return source.Replace(anchor, methods + anchor, StringComparison.Ordinal);
    }
}
