namespace XPScript.Compiler;

internal static class NotesDatabaseReplicaPostProcessor
{
    internal static string Apply(string source)
    {
        const string declaration = "internal sealed class XPScriptNotesDatabase : XPScriptNotesObject";
        if (!source.Contains(declaration, StringComparison.Ordinal))
            throw new InvalidOperationException("NotesDatabase runtime declaration was not found.");

        source = source.Replace(
            declaration,
            "internal sealed partial class XPScriptNotesDatabase : XPScriptNotesObject",
            StringComparison.Ordinal);

        return source + "\n\n" + ExtraCode;
    }

    private const string ExtraCode = """
internal sealed partial class XPScriptNotesDatabase
{
    public XPScriptNotesDatabase? CreateCopy(object? serverValue, object? filePathValue)
    {
        EnsureAlive();
        if (!IsOpen) return null;

        var server = XPScriptRuntime.CStr(serverValue).Trim();
        var filePath = XPScriptRuntime.CStr(filePathValue).Trim();
        if (filePath.Length == 0)
            throw new XPScriptRuntimeException(5, "CreateCopy destination file path cannot be empty.");

        var handle = Session.Api.CreateDatabaseCopy(Server, FilePath, server, filePath);
        return new XPScriptNotesDatabase(Session, handle, server, filePath);
    }

    public void SetReplicaId(object? replicaIdValue)
    {
        EnsureAlive();
        if (!IsOpen)
            throw new XPScriptRuntimeException(91, "SetReplicaId requires an open NotesDatabase.");

        Session.Api.SetDatabaseReplicaId(_handle, XPScriptRuntime.CStr(replicaIdValue));
    }
}
""";
}
