namespace XPScript.Compiler;

internal static class NotesViewNavigationV2FixPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = source.Replace(
            "NotesViewNavigationV2PostProcessor.",
            "XPScriptNotesView.",
            StringComparison.Ordinal);

        const string misplaced = "    public int Count { get { EnsureAlive(); return VisibleRows().Length; } }\n    private int _cacheSize;\n    private int _maxLevel = int.MaxValue;\n    public int CacheSize { get { EnsureAlive(); return _cacheSize; } set { EnsureAlive(); _cacheSize = Math.Max(0, value); } }\n    public int MaxLevel { get { EnsureAlive(); return _maxLevel == int.MaxValue ? 30 : _maxLevel; } set { EnsureAlive(); _maxLevel = value < 0 ? 0 : value; NormalizeCurrent(); } }";
        if (!source.Contains(misplaced, StringComparison.Ordinal))
            throw new CompilerException("Unable to repair NotesView navigation V2 collection properties.");
        source = source.Replace(misplaced, "    public int Count { get { EnsureAlive(); return _rows.Length; } }", StringComparison.Ordinal);

        const string navigatorAnchor = "    public XPScriptNotesView ParentView { get { EnsureAlive(); return _view; } }\n    public int Count { get { EnsureAlive(); return _rows.Length; } }";
        const string navigatorReplacement = "    public XPScriptNotesView ParentView { get { EnsureAlive(); return _view; } }\n    public int Count { get { EnsureAlive(); return VisibleRows().Length; } }\n    private int _cacheSize;\n    private int _maxLevel = int.MaxValue;\n    public int CacheSize { get { EnsureAlive(); return _cacheSize; } set { EnsureAlive(); _cacheSize = Math.Max(0, value); } }\n    public int MaxLevel { get { EnsureAlive(); return _maxLevel == int.MaxValue ? 30 : _maxLevel; } set { EnsureAlive(); _maxLevel = value < 0 ? 0 : value; NormalizeCurrent(); } }";
        if (!source.Contains(navigatorAnchor, StringComparison.Ordinal))
            throw new CompilerException("Unable to repair NotesView navigation V2 navigator properties.");
        source = source.Replace(navigatorAnchor, navigatorReplacement, StringComparison.Ordinal);

        return source;
    }
}
