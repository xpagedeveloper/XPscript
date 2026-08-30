namespace XPScript.Compiler;

internal static class NotesViewNavigatorHistoryCapPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source = ReplaceRequired(source,
            "            _rows = [.. _rows, .. block];\n        }\n        return index < _rows.Length;",
            "            _rows = [.. _rows, .. block];\n            var removed = TrimHistory();\n            if (removed > 0) index = Math.Max(0, index - removed);\n        }\n        return index < _rows.Length;",
            "history-trim-call");
        source = ReplaceRequired(source,
            "    private void SyncViewGeneration()",
            "    private int TrimHistory()\n    {\n        if (_currentIndex <= MaxRetainedHistory) return 0;\n        var remove = _currentIndex - MaxRetainedHistory;\n        if (remove <= 0) return 0;\n        _rows = _rows[remove..];\n        _currentIndex -= remove;\n        return remove;\n    }\n\n    private void SyncViewGeneration()",
            "history-trim-helper");
        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal)) throw new CompilerException("Unable to apply NotesView navigator history cap (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
