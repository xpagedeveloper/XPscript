namespace XPScript.Compiler;

internal static class NotesViewNavigatorBufferMaxEntriesPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "        var cacheSize = Math.Clamp(XPScriptRuntime.CInt(cacheSizeValue), 0, 512);",
            "        var cacheSize = XPScriptRuntime.CInt(cacheSizeValue);",
            "navigator-create-buffer-max-entries");

        source = ReplaceRequired(
            source,
            "        _cacheSize = Math.Clamp(cacheSize, 0, 512);\n        _viewGeneration = view.NavigationGeneration;",
            "        _bufferMaxEntries = cacheSize;\n        _cacheSize = Math.Clamp(cacheSize, 0, 512);\n        _viewGeneration = view.NavigationGeneration;",
            "navigator-constructor-buffer-max-entries");

        source = ReplaceRequired(
            source,
            "    private int _cacheSize = 64;\n    private int _maxLevel = int.MaxValue;\n    private bool _streaming;\n    private bool _streamExhausted;\n    private long _viewGeneration;\n    public int CacheSize { get { EnsureAlive(); return _cacheSize; } set { EnsureAlive(); _cacheSize = Math.Clamp(value, 0, 512); } }",
            "    private int _cacheSize = 64;\n    private int _bufferMaxEntries = 64;\n    private int _maxLevel = int.MaxValue;\n    private bool _streaming;\n    private bool _streamExhausted;\n    private long _viewGeneration;\n    private int EffectiveBufferMaxEntries => Math.Clamp(_bufferMaxEntries, 0, 512);\n    public int BufferMaxEntries\n    {\n        get\n        {\n            EnsureAlive();\n            _bufferMaxEntries = Math.Clamp(_bufferMaxEntries, 0, 512);\n            return _bufferMaxEntries;\n        }\n        set\n        {\n            EnsureAlive();\n            _bufferMaxEntries = value;\n            _cacheSize = Math.Clamp(value, 0, 512);\n        }\n    }\n    public int CacheSize { get { return BufferMaxEntries; } set { BufferMaxEntries = value; } }",
            "navigator-buffer-max-entries-property");

        source = source.Replace(
            "_view.ReadRowWindow(afterPosition, _cacheSize)",
            "_view.ReadRowWindow(afterPosition, Math.Max(1, EffectiveBufferMaxEntries))",
            StringComparison.Ordinal);
        source = source.Replace(
            "_view.ReadRowWindow(currentPosition, _cacheSize)",
            "_view.ReadRowWindow(currentPosition, Math.Max(1, EffectiveBufferMaxEntries))",
            StringComparison.Ordinal);

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesViewNavigator BufferMaxEntries patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
