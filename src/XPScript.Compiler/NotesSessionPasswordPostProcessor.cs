namespace XPScript.Compiler;

internal static class NotesSessionPasswordPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string anchor = "    public XPScriptNotesDatabase OpenDatabase(object? serverValue, object? fileValue)";
        const string methods = """
    public string HashPassword(object? passwordValue)
    {
        EnsureAlive();
        return Api.HashPassword(XPScriptRuntime.CStr(passwordValue));
    }

    public bool VerifyPassword(object? passwordValue, object? hashedPasswordValue)
    {
        EnsureAlive();
        return Api.VerifyPassword(
            XPScriptRuntime.CStr(passwordValue),
            XPScriptRuntime.CStr(hashedPasswordValue));
    }

""";

        if (!source.Contains(anchor, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesSession password surface.");
        return source.Replace(anchor, methods + anchor, StringComparison.Ordinal);
    }
}
