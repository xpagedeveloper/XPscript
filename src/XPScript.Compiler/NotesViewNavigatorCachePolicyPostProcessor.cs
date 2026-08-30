namespace XPScript.Compiler;

internal static class NotesViewNavigatorCachePolicyPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return ReplaceRequired(
            source,
            "    internal nint NativeHandle { get { EnsureAlive(); return _handle; } }\n    public string Name { get; }",
            "    internal nint NativeHandle { get { EnsureAlive(); return _handle; } }\n    public string Name { get; }\n    internal long NavigationGeneration => 0;",
            "view-navigation-generation");
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesView navigator cache policy (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
