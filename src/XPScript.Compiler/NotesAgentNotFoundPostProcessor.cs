namespace XPScript.Compiler;

internal static class NotesAgentNotFoundPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        string[] nativeSignatures =
        [
            "internal string RunAgent(uint db, string name, uint documentContext, uint parameterNoteId = 0)",
            "internal string? RunAgent(uint db, string name, uint documentContext, uint parameterNoteId = 0)",
            "internal string RunAgent(uint db, string name, uint documentContext)",
            "internal string? RunAgent(uint db, string name, uint documentContext)",
            "internal string RunAgent(nint db, string name, nint documentContext)",
            "internal string? RunAgent(nint db, string name, nint documentContext)"
        ];

        var nativeSignature = nativeSignatures.FirstOrDefault(signature =>
            source.Contains(signature, StringComparison.Ordinal));
        if (nativeSignature is null)
        {
            if (!source.Contains("NIFFindDesignNote(agent)", StringComparison.Ordinal))
                return RemoveLegacyAgentResultSurface(DeduplicateTimeDateCollateDelegate(source));
            throw new CompilerException("Unable to apply Notes agent not-found patch (native-runagent-nullable).");
        }

        var nullableNativeSignature = nativeSignature;
        if (nativeSignature.StartsWith("internal string RunAgent", StringComparison.Ordinal))
        {
            nullableNativeSignature = nativeSignature.Replace(
                "internal string RunAgent",
                "internal string? RunAgent",
                StringComparison.Ordinal);
            source = source.Replace(nativeSignature, nullableNativeSignature, StringComparison.Ordinal);
        }

        const string oldFindAgent = """
        var find = Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote");
        var status = find(db, agentName.Pointer, NoteClassFilter, out var noteId);
        if (status != 0 && TryResolve<NIFFindPrivateDesignNoteDelegate>("NIFFindPrivateDesignNote", out var findPrivate) && findPrivate is not null)
            status = findPrivate(db, agentName.Pointer, NoteClassFilter, out noteId);
        Check(status, "NIFFindDesignNote(agent)");
""";
        const string newFindAgent = """
        var find = Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote");
        var status = find(db, agentName.Pointer, NoteClassFilter, out var noteId);
        if (status != 0 && TryResolve<NIFFindPrivateDesignNoteDelegate>("NIFFindPrivateDesignNote", out var findPrivate) && findPrivate is not null)
            status = findPrivate(db, agentName.Pointer, NoteClassFilter, out noteId);
        if ((status & 0x3FFF) == 0x0404) return null;
        Check(status, "NIFFindDesignNote(agent)");
""";
        source = EnsureReplacementAfter(
            source,
            nullableNativeSignature,
            oldFindAgent,
            newFindAgent,
            "native-runagent-not-found");

        source = DeduplicateTimeDateCollateDelegate(source);
        return RemoveLegacyAgentResultSurface(source);
    }

    private static string RemoveLegacyAgentResultSurface(string source)
    {
        const string firstMethod = "    public XPScriptNotesAgentResult? RunAgent(object? nameValue)";
        const string releaseAnchor = "    protected override void ReleaseNative()";
        var methodStart = source.IndexOf(firstMethod, StringComparison.Ordinal);
        if (methodStart >= 0)
        {
            var methodEnd = source.IndexOf(releaseAnchor, methodStart, StringComparison.Ordinal);
            if (methodEnd < 0)
                throw new CompilerException("Unable to remove legacy NotesDatabase.RunAgent surface.");
            source = source[..methodStart] + source[methodEnd..];
        }

        const string resultClass = "internal sealed class XPScriptNotesAgentResult : XPScriptNotesOwnedObject";
        var classStart = source.IndexOf(resultClass, StringComparison.Ordinal);
        if (classStart >= 0)
        {
            var nextClass = source.IndexOf("internal static class XPScriptNotesConvert", classStart, StringComparison.Ordinal);
            if (nextClass < 0)
                throw new CompilerException("Unable to remove legacy NotesAgentResult class.");
            source = source[..classStart] + source[nextClass..];
        }

        if (source.Contains("XPScriptNotesAgentResult", StringComparison.Ordinal))
            throw new CompilerException("Legacy NotesAgentResult references remain in generated runtime.");
        return source;
    }

    private static string DeduplicateTimeDateCollateDelegate(string source)
    {
        const string marker = "delegate int TimeDateCollateDelegate(";
        var first = source.IndexOf(marker, StringComparison.Ordinal);
        if (first < 0 || source.IndexOf(marker, first + marker.Length, StringComparison.Ordinal) < 0)
            return source;

        const string databaseDeclaration = """
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate int TimeDateCollateDelegate(ref XPScriptNotesTimeDate first, ref XPScriptNotesTimeDate second);
""";
        if (!source.Contains(databaseDeclaration, StringComparison.Ordinal))
            throw new CompilerException("Unable to deduplicate Notes TimeDateCollateDelegate safely.");
        return source.Replace(databaseDeclaration, string.Empty, StringComparison.Ordinal);
    }

    private static string EnsureReplacementAfter(string source, string anchor, string oldValue, string newValue, string stage)
    {
        var anchorIndex = source.IndexOf(anchor, StringComparison.Ordinal);
        if (anchorIndex < 0)
            throw new CompilerException("Unable to apply Notes agent not-found patch (" + stage + "-anchor).");

        var searchStart = anchorIndex + anchor.Length;
        if (source.IndexOf(newValue, searchStart, StringComparison.Ordinal) >= 0)
            return source;

        var matchIndex = source.IndexOf(oldValue, searchStart, StringComparison.Ordinal);
        if (matchIndex < 0)
            throw new CompilerException("Unable to apply Notes agent not-found patch (" + stage + ").");

        return source[..matchIndex] + newValue + source[(matchIndex + oldValue.Length)..];
    }
}
