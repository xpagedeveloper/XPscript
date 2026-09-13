namespace XPScript.Compiler;

internal static class NotesDocumentSendPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string marker = """
    public void Save()
    {
        EnsureAlive();
        Session.Api.SaveNote(_handle);
        NoteId = Session.Api.GetNoteId(_handle);
    }
""";

        const string replacement = """
    public void Save()
    {
        EnsureAlive();
        Session.Api.SaveNote(_handle);
        NoteId = Session.Api.GetNoteId(_handle);
    }

    public void Send() => Send(false, null);

    public void Send(object? attachFormValue) => Send(attachFormValue, null);

    public void Send(object? attachFormValue, object? recipientsValue)
    {
        EnsureAlive();
        var recipients = NormalizeMailRecipients(recipientsValue);
        Session.Api.SendNote(_handle, Database.Handle, XPScriptRuntime.CBool(attachFormValue), recipients);
    }

    private static string[]? NormalizeMailRecipients(object? recipientsValue)
    {
        if (recipientsValue is null) return null;
        if (recipientsValue is LSArray array)
        {
            var values = new List<string>();
            for (var index = array.LBound(); index <= array.UBound(); index++)
            {
                var value = XPScriptRuntime.CStr(array.Get(index)).Trim();
                if (value.Length != 0) values.Add(value);
            }
            if (values.Count == 0) throw new XPScriptRuntimeException(5, "NotesDocument.Send recipients cannot be empty.");
            return values.ToArray();
        }

        var recipient = XPScriptRuntime.CStr(recipientsValue).Trim();
        if (recipient.Length == 0) throw new XPScriptRuntimeException(5, "NotesDocument.Send recipient cannot be empty.");
        return [recipient];
    }
""";

        if (!source.Contains(marker, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesDocument Send surface.");
        return source.Replace(marker, replacement, StringComparison.Ordinal);
    }
}
