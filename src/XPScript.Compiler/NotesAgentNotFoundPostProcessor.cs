using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal static class NotesAgentNotFoundPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var signatureMatch = Regex.Match(
            source,
            @"(?m)^\s*internal\s+string(?<nullable>\?)?\s+RunAgent\([^\r\n]*\bdb\s*,\s*string\s+name\s*,[^\r\n]*\bdocumentContext\s*\)\s*$");
        if (!signatureMatch.Success)
        {
            // Notes runtime features are emitted on demand. Database-level RunAgent
            // helpers can exist even when the native agent implementation is omitted,
            // so only the native design-note lookup proves that this patch is required.
            if (!source.Contains("NIFFindDesignNote(agent)", StringComparison.Ordinal))
                return source;
            throw new CompilerException("Unable to apply Notes RunAgent not-found patch (native-runagent-nullable).");
        }

        var nullableNativeSignature = signatureMatch.Value;
        if (!signatureMatch.Groups["nullable"].Success)
        {
            nullableNativeSignature = Regex.Replace(
                nullableNativeSignature,
                @"\binternal\s+string\s+RunAgent",
                "internal string? RunAgent",
                RegexOptions.CultureInvariant);
            source = source[..signatureMatch.Index] + nullableNativeSignature + source[(signatureMatch.Index + signatureMatch.Length)..];
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

        source = EnsureReplacement(
            source,
            "    private XPScriptNotesAgentResult RunAgentCore(object? nameValue, XPScriptNotesDocument? document)",
            "    private XPScriptNotesAgentResult? RunAgentCore(object? nameValue, XPScriptNotesDocument? document)",
            "database-runagent-nullable");

        source = EnsureReplacement(
            source,
            "        var output = Session.Api.RunAgent(_handle, name, document?.NativeHandle ?? 0);\n        return new XPScriptNotesAgentResult(Session, this, output);",
            "        var output = Session.Api.RunAgent(_handle, name, document?.NativeHandle ?? 0);\n        return output is null ? null : new XPScriptNotesAgentResult(Session, this, output);",
            "database-runagent-nothing");

        return source;
    }

    private static string EnsureReplacementAfter(string source, string anchor, string oldValue, string newValue, string stage)
    {
        var anchorIndex = source.IndexOf(anchor, StringComparison.Ordinal);
        if (anchorIndex < 0)
            throw new CompilerException("Unable to apply Notes RunAgent not-found patch (" + stage + "-anchor).");

        var searchStart = anchorIndex + anchor.Length;
        if (source.IndexOf(newValue, searchStart, StringComparison.Ordinal) >= 0)
            return source;

        var matchIndex = source.IndexOf(oldValue, searchStart, StringComparison.Ordinal);
        if (matchIndex < 0)
            throw new CompilerException("Unable to apply Notes RunAgent not-found patch (" + stage + ").");

        return source[..matchIndex] + newValue + source[(matchIndex + oldValue.Length)..];
    }

    private static string EnsureReplacement(string source, string oldValue, string newValue, string stage)
    {
        if (source.Contains(newValue, StringComparison.Ordinal))
            return source;
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes RunAgent not-found patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
