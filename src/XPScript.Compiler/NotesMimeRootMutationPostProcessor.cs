namespace XPScript.Compiler;

internal static class NotesMimeRootMutationPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    private readonly XPScriptNotesMimeDirectoryOwner _mimeDirectoryOwner;\n    private readonly nint _nativeEntity;",
            "    private XPScriptNotesMimeDirectoryOwner _mimeDirectoryOwner;\n    private nint _nativeEntity;",
            "mutable root MIME directory binding");

        source = ReplaceRequired(source,
            "        throw new System.NotSupportedException(\"NotesMIMEEntity.SetContentFromText requires verified Domino per-entity content mutation support; managed MIME serialization is intentionally not used.\");",
            "        WriteRootContent((byte[])stream.Read(), XPScriptRuntime.CStr(contentTypeValue), XPScriptRuntime.CInt(encodingValue), \"SetContentFromText\");",
            "root SetContentFromText");

        source = ReplaceRequired(source,
            "        throw new System.NotSupportedException(\"NotesMIMEEntity.SetContentFromBytes requires verified Domino per-entity content mutation support; managed MIME serialization is intentionally not used.\");",
            "        WriteRootContent((byte[])stream.Read(), XPScriptRuntime.CStr(contentTypeValue), XPScriptRuntime.CInt(encodingValue), \"SetContentFromBytes\");",
            "root SetContentFromBytes");

        source = ReplaceRequired(source,
            "    private static string MimeSymbolText(int symbol) => symbol switch",
            RootMutationHelpers + "\n    private static string MimeSymbolText(int symbol) => symbol switch",
            "root MIME mutation helpers");

        return source;
    }

    private const string RootMutationHelpers = """
    private void WriteRootContent(byte[] data, string contentType, int encoding, string member)
    {
        EnsureEntityAlive();
        if (_nativeEntity != _mimeDirectoryOwner.RootEntity)
            throw new System.NotSupportedException("NotesMIMEEntity." + member + " currently supports the root entity only; verified Domino child-entity mutation support is not available.");

        contentType = contentType.Trim();
        if (contentType.Length == 0) contentType = "application/octet-stream";
        if (contentType.Contains('\r') || contentType.Contains('\n'))
            throw new XPScriptRuntimeException(5, "Invalid MIME content type.");

        var raw = BuildRootMimeStream(data, contentType, encoding);

        // MIME directory entity handles are tied to the note's current MIME structure.
        // Close the cached directory before itemizing the replacement stream. This also
        // invalidates sibling/child wrappers that still point at the old directory.
        _document.InvalidateMimeDirectory();
        Session.Api.WriteMimeStream(_document.NativeDatabaseHandle, _document.NativeHandle, _itemName, raw);

        // Keep the mutating root wrapper usable, but bind it to a fresh directory and
        // root entity so metadata reads immediately observe the itemized MIME content.
        _mimeDirectoryOwner = _document.GetMimeDirectoryOwner();
        _nativeEntity = _mimeDirectoryOwner.RootEntity;
    }

    private static byte[] BuildRootMimeStream(byte[] data, string contentType, int encoding)
    {
        string transferEncoding;
        byte[] body;

        switch (encoding)
        {
            case 1726:
                transferEncoding = "quoted-printable";
                body = EncodeRootQuotedPrintable(data);
                break;
            case 1727:
                transferEncoding = "base64";
                body = System.Text.Encoding.ASCII.GetBytes(Convert.ToBase64String(data, Base64FormattingOptions.InsertLineBreaks));
                break;
            case 1730:
                transferEncoding = "binary";
                body = data;
                break;
            default:
                transferEncoding = "8bit";
                body = data;
                break;
        }

        var headers = System.Text.Encoding.Latin1.GetBytes(
            "Content-Type: " + contentType + "\r\n" +
            "Content-Transfer-Encoding: " + transferEncoding + "\r\n\r\n");
        var result = new byte[headers.Length + body.Length];
        Buffer.BlockCopy(headers, 0, result, 0, headers.Length);
        Buffer.BlockCopy(body, 0, result, headers.Length, body.Length);
        return result;
    }

    private static byte[] EncodeRootQuotedPrintable(byte[] input)
    {
        var text = new System.Text.StringBuilder();
        foreach (var value in input)
        {
            if ((value >= 33 && value <= 60) || (value >= 62 && value <= 126) || value == 9 || value == 32 || value == 13 || value == 10)
                text.Append((char)value);
            else
                text.Append('=').Append(value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        }
        return System.Text.Encoding.ASCII.GetBytes(text.ToString());
    }

""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string label)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to inject {label}.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
