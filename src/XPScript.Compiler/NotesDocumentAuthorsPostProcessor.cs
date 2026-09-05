namespace XPScript.Compiler;

internal static class NotesDocumentAuthorsPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string marker = "    public bool IsDesign { get { EnsureAlive(); return ResolveDesignType().Length != 0; } }";
        const string replacement = """
    public object Authors
    {
        get
        {
            EnsureAlive();
            if (_handle == 0 || !Session.Api.TryGetFirstItemInfo(_handle, "$UpdatedBy", out var info))
                return LSOperatorArrayRuntime.CreateArray();
            var authors = Session.Api.GetItemValues(_handle, info, Session)
                .Select(value => (object?)XPScriptRuntime.CStr(value))
                .ToArray();
            return LSOperatorArrayRuntime.CreateArray(authors);
        }
    }
    public bool IsDesign { get { EnsureAlive(); return ResolveDesignType().Length != 0; } }
""";

        if (!source.Contains(marker, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesDocument Authors surface.");
        return source.Replace(marker, replacement, StringComparison.Ordinal);
    }
}
