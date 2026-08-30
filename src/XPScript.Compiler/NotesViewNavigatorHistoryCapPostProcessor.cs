namespace XPScript.Compiler;

internal static class NotesViewNavigatorHistoryCapPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source = ReplaceRequired(source,
            "    private const int MaxRetainedHistory = 2048;",
            "    private const int MaxRetainedHistory = 2048;\n    private int _historyOffset;",
            "history-offset");
        source = ReplaceRequired(source,
            "            _rows = [.. _rows, .. block];\n        }\n        return index < _rows.Length;",
            "            _rows = [.. _rows, .. block];\n            TrimHistory();\n            index = Math.Max(0, index - _historyOffset);\n            _historyOffset = 0;\n        }\n        return index < _rows.Length;",
            "history-trim-call");
        source = ReplaceRequired(source,
            "    private void SyncViewGeneration()",
            "    private void TrimHistory()\n    {\n        if (_currentIndex <= MaxRetainedHistory) return;\n        var remove = _currentIndex - MaxRetainedHistory;\n        if (remove <= 0) return;\n        _rows = _rows[remove..];\n        _currentIndex -= remove;\n        _historyOffset += remove;\n    }\n\n    private void SyncViewGeneration()",
            "history-trim-helper");
        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal)) throw new CompilerException("Unable to apply NotesView navigator history cap (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
