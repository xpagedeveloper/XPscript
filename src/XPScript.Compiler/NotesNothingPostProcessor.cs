namespace XPScript.Compiler;

internal static class NotesNothingPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "internal static class XPScriptNotes\n{",
            """
internal sealed class XPScriptNotesNothing : ILSObjectReference
{
    internal static readonly XPScriptNotesNothing Value = new();
    private XPScriptNotesNothing() { }
    public bool IsNothing => true;
    public override string ToString() => throw new InvalidCastException("Nothing cannot be converted to String.");
}

internal static class XPScriptNotes
{
    public static readonly object NothingValue = XPScriptNotesNothing.Value;
    public static object NormalizeObjectResult(object? value) => value ?? NothingValue;
""",
            "notes-nothing-state");

        source = ReplaceRequired(
            source,
            "        if (value is null) return;\n        if (value is XPScriptNotesObject notesObject)",
            "        if (value is null || ReferenceEquals(value, NothingValue)) return;\n        if (value is XPScriptNotesObject notesObject)",
            "notes-nothing-recycle");

        source = ReplaceRequired(
            source,
            "        if (current is null || ReferenceEquals(current, replacement)) return;",
            "        if (current is null || ReferenceEquals(current, NothingValue) || ReferenceEquals(current, replacement)) return;",
            "notes-nothing-replacement");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string name)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new InvalidOperationException("Unable to apply Notes Nothing runtime patch: " + name + ".");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
