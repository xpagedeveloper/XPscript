namespace XPScript.Compiler;

internal static class NotesMailMimePruningPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string fullMimeWriter = """
    private void WriteMimeBody(XPScriptNotesDocument document, byte[] bodyBytes, string contentType)
    {
        var root = document.CreateMIMEEntity("Body");
        if (_attachments.Count == 0)
        {
            var stream = Session.CreateStream();
            try
            {
                stream.Write(bodyBytes);
                stream.Position = 0;
                root.SetContentFromBytes(stream, contentType, 1725);
            }
            finally { stream.Recycle(); root.Recycle(); }
            return;
        }

        var bodyPart = root.CreateChildEntity();
        var bodyStream = Session.CreateStream();
        try
        {
            bodyStream.Write(bodyBytes);
            bodyStream.Position = 0;
            bodyPart.SetContentFromBytes(bodyStream, contentType, 1725);
        }
        finally { bodyStream.Recycle(); bodyPart.Recycle(); root.Recycle(); }

        foreach (var attachment in _attachments)
        {
            var currentRoot = document.GetMIMEEntity("Body")
                ?? throw new XPScriptRuntimeException(91, "NotesMail MIME body is no longer available while adding attachments.");
            var part = currentRoot.CreateChildEntity();
            var stream = Session.CreateStream();
            try
            {
                if (!stream.Open(attachment.Path, "readonly"))
                    throw new XPScriptRuntimeException(53, "Unable to open NotesMail attachment: " + attachment.Path);
                part.SetContentFromBytes(
                    stream,
                    attachment.ContentType + "; name=\"" + attachment.FileName + "\"",
                    1727);
                var disposition = part.CreateHeader("Content-Disposition");
                disposition.SetHeaderVal("attachment; filename=\"" + attachment.FileName + "\"");
                disposition.Recycle();
            }
            finally { stream.Recycle(); part.Recycle(); currentRoot.Recycle(); }
        }
    }
""";

        const string disabledMimeWriter = """
    private void WriteMimeBody(XPScriptNotesDocument document, byte[] bodyBytes, string contentType)
    {
        throw new XPScriptRuntimeException(91, "NotesMail MIME support was not included because the application does not use a MIME NotesMail member.");
    }
""";

        if (!source.Contains(fullMimeWriter, StringComparison.Ordinal))
            throw new CompilerException("Unable to prune unused NotesMail MIME runtime.");

        return source.Replace(fullMimeWriter, disabledMimeWriter, StringComparison.Ordinal);
    }
}
