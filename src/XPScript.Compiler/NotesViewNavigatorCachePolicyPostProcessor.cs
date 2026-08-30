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

        source = ReplaceRequired(
            source,
            "    public void Refresh()\n    {\n        EnsureAlive();\n        Session.Api.UpdateCollection(_handle);\n        _navigationNoteIds = Session.Api.ReadAllViewNoteIds(_handle).ToArray();\n    }",
            "    public void Refresh()\n    {\n        EnsureAlive();\n        Session.Api.UpdateCollection(_handle);\n        _navigationNoteIds = Session.Api.ReadAllViewNoteIds(_handle).ToArray();\n        _navigationGeneration++;\n    }",
            "view-refresh-generation");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesView navigator cache policy (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
