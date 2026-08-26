using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal static class NotesThreadLifecyclePostProcessor
{
    private static readonly Regex EnsureInitializedCall = new(
        @"(?m)^(?<indent>[ \t]*)EnsureInitialized\(\);",
        RegexOptions.CultureInvariant);

    public static string Apply(string source)
    {
        var scopeIndex = 0;
        source = EnsureInitializedCall.Replace(source, match =>
            match.Groups["indent"].Value +
            "using var __notesThreadScope" + scopeIndex++ + " = EnterNotesThread();");

        source = ReplaceRequired(source,
            "        _initialized = true;",
            "        _initialized = true;\n        MarkProcessInitializationThread();",
            "process-initialization-thread");

        // Cleanup methods are also callable directly from Recycle() and therefore need
        // their own thread scopes even though they historically did not call EnsureInitialized().
        source = ReplaceRequired(source,
            "    internal void CloseDatabase(nint db)\n    {\n        if (db != 0) Check(Resolve<NSFDbCloseDelegate>(\"NSFDbClose\")(db), \"NSFDbClose\");\n    }",
            "    internal void CloseDatabase(nint db)\n    {\n        using var __notesThreadScopeCloseDatabase = EnterNotesThread();\n        if (db != 0) Check(Resolve<NSFDbCloseDelegate>(\"NSFDbClose\")(db), \"NSFDbClose\");\n    }",
            "close-database-thread-scope");

        source = ReplaceRequired(source,
            "    internal void CloseView(nint collection)\n    {\n        if (collection != 0) Check(Resolve<NIFCloseCollectionDelegate>(\"NIFCloseCollection\")(collection), \"NIFCloseCollection\");\n    }",
            "    internal void CloseView(nint collection)\n    {\n        using var __notesThreadScopeCloseView = EnterNotesThread();\n        if (collection != 0) Check(Resolve<NIFCloseCollectionDelegate>(\"NIFCloseCollection\")(collection), \"NIFCloseCollection\");\n    }",
            "close-view-thread-scope");

        source = ReplaceRequired(source,
            "    internal void CloseNote(nint note)\n    {\n        if (note != 0) Check(Resolve<NSFNoteCloseDelegate>(\"NSFNoteClose\")(note), \"NSFNoteClose\");\n    }",
            "    internal void CloseNote(nint note)\n    {\n        using var __notesThreadScopeCloseNote = EnterNotesThread();\n        if (note != 0) Check(Resolve<NSFNoteCloseDelegate>(\"NSFNoteClose\")(note), \"NSFNoteClose\");\n    }",
            "close-note-thread-scope");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string name)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new InvalidOperationException("Notes thread lifecycle source marker not found: " + name);
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
