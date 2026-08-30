namespace XPScript.Compiler;

internal static class NotesViewNavigatorCachePolicyPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "    internal nint NativeHandle { get { EnsureAlive(); return _handle; } }\n    public string Name { get; }",
            "    internal nint NativeHandle { get { EnsureAlive(); return _handle; } }\n    public string Name { get; }\n    private long _navigationGeneration;\n    internal long NavigationGeneration { get { EnsureAlive(); return _navigationGeneration; } }",
            "view-navigation-generation");

        const string refreshStart = "    public void Refresh()\n    {\n        EnsureAlive();";
        var start = source.IndexOf(refreshStart, StringComparison.Ordinal);
        if (start < 0) throw new CompilerException("Unable to apply NotesView navigator cache policy (view-refresh-start).");
        var end = source.IndexOf("\n    }", start + refreshStart.Length, StringComparison.Ordinal);
        if (end < 0) throw new CompilerException("Unable to apply NotesView navigator cache policy (view-refresh-end).");
        var refresh = source[start..end];
        if (!refresh.Contains("_navigationGeneration++", StringComparison.Ordinal))
            source = source.Insert(end, "\n        _navigationGeneration++;");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesView navigator cache policy (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
