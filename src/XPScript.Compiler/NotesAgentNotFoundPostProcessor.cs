namespace XPScript.Compiler;

internal static class NotesAgentNotFoundPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "    internal string RunAgent(uint db, string name, uint documentContext)",
            "    internal string? RunAgent(uint db, string name, uint documentContext)",
            "native-runagent-nullable");

        source = ReplaceRequired(
            source,
            "        Check(status, \"NIFFindDesignNote(agent)\");",
            "        if ((status & 0x3FFF) == 0x0404) return null;\n        Check(status, \"NIFFindDesignNote(agent)\");",
            "native-runagent-not-found");

        source = ReplaceRequired(
            source,
            "    private XPScriptNotesAgentResult RunAgentCore(object? nameValue, XPScriptNotesDocument? document)",
            "    private XPScriptNotesAgentResult? RunAgentCore(object? nameValue, XPScriptNotesDocument? document)",
            "database-runagent-nullable");

        source = ReplaceRequired(
            source,
            "        var output = Session.Api.RunAgent(_handle, name, document?.NativeHandle ?? 0);\n        return new XPScriptNotesAgentResult(Session, this, output);",
            "        var output = Session.Api.RunAgent(_handle, name, document?.NativeHandle ?? 0);\n        return output is null ? null : new XPScriptNotesAgentResult(Session, this, output);",
            "database-runagent-nothing");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes RunAgent not-found patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
