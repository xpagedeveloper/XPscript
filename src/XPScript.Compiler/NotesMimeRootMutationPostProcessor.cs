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
            "    public string ContentAsText { get { EnsureEntityAlive(); throw new System.NotSupportedException(\"NotesMIMEEntity.ContentAsText requires verified Domino MIME entity-data decoding support; managed MIME decoding is intentionally not used.\"); } }",
            "    public string ContentAsText { get { EnsureEntityAlive(); return _nativeEntity == _mimeDirectoryOwner.RootEntity ? ReadRootText(\"ContentAsText\") : ReadEntityText(\"ContentAsText\"); } }",
            "root ContentAsText");

        source = ReplaceRequired(source,
            "        throw new System.NotSupportedException(\"NotesMIMEEntity.GetContentAsBytes requires verified Domino MIME entity-data decoding support; managed MIME decoding is intentionally not used.\");",
            "        stream.Write(_nativeEntity == _mimeDirectoryOwner.RootEntity ? ReadRootContent(\"GetContentAsBytes\") : ReadEntityContent(\"GetContentAsBytes\"));",
            "root GetContentAsBytes");

        source = ReplaceRequired(source,
            "        throw new System.NotSupportedException(\"NotesMIMEEntity.GetContentAsText requires verified Domino MIME entity-data decoding support; managed MIME decoding is intentionally not used.\");",
            "        stream.WriteText(_nativeEntity == _mimeDirectoryOwner.RootEntity ? ReadRootText(\"GetContentAsText\") : ReadEntityText(\"GetContentAsText\"));",
            "root GetContentAsText");

        source = ReplaceRequired(source,
            "        get { EnsureEntityAlive(); throw new System.NotSupportedException(\"NotesMIMEEntity.Preamble requires verified Domino MIME entity-data support; managed multipart parsing is intentionally not used.\"); }\n        set { EnsureEntityAlive(); throw new System.NotSupportedException(\"NotesMIMEEntity.Preamble requires verified Domino MIME entity-data support; managed multipart serialization is intentionally not used.\"); }",
            "        get { EnsureEntityAlive(); return ReadEntityPreamble(\"Preamble\"); }\n        set { EnsureEntityAlive(); WriteEntityPreamble(value ?? \"\", \"Preamble\"); }",
            "MIME entity Preamble");

        source = ReplaceRequired(source,
            "        throw new System.NotSupportedException(\"NotesMIMEEntity.GetEntityAsText requires verified Domino per-entity RFC822 data access; the root MIME stream is intentionally not returned for child entities.\");",
            "        stream.Write(_nativeEntity == _mimeDirectoryOwner.RootEntity ? ReadRootEntity(\"GetEntityAsText\") : ReadEntityRaw(\"GetEntityAsText\"));",
            "root GetEntityAsText");

        source = ReplaceRequired(source,
            "        throw new System.NotSupportedException(\"NotesMIMEEntity.SetContentFromText requires verified Domino per-entity content mutation support; managed MIME serialization is intentionally not used.\");",
            "        WriteRootContent((byte[])stream.Read(), XPScriptRuntime.CStr(contentTypeValue), XPScriptRuntime.CInt(encodingValue), \"SetContentFromText\");",
            "root SetContentFromText");

        source = ReplaceRequired(source,
            "        throw new System.NotSupportedException(\"NotesMIMEEntity.SetContentFromBytes requires verified Domino per-entity content mutation support; managed MIME serialization is intentionally not used.\");",
            "        WriteRootContent((byte[])stream.Read(), XPScriptRuntime.CStr(contentTypeValue), XPScriptRuntime.CInt(encodingValue), \"SetContentFromBytes\");",
            "root SetContentFromBytes");

        source = ReplaceRequired(source,
            "        throw new System.NotSupportedException(\"NotesMIMEEntity.EncodeContent requires verified Domino MIME entity encoding support; managed transfer encoding is intentionally not used.\");",
            "        EncodeEntityContent(XPScriptRuntime.CInt(encodingValue), \"EncodeContent\");",
            "MIME entity EncodeContent");

        source = ReplaceRequired(source,
            "        throw new System.NotSupportedException(\"NotesMIMEEntity.DecodeContent requires verified Domino MIME entity decoding support; managed transfer decoding is intentionally not used.\");",
            "        EncodeEntityContent(1725, \"DecodeContent\");",
            "MIME entity DecodeContent");

        source = ReplaceRequired(source,
            "    public string Headers { get { EnsureEntityAlive(); throw new System.NotSupportedException(\"NotesMIMEEntity.Headers requires verified Domino MIME entity header access; managed root-stream header parsing is intentionally not used.\"); } }",
            "    public string Headers { get { EnsureEntityAlive(); return ReadEntityHeadersText(\"Headers\", null); } }",
            "MIME entity Headers");

        source = ReplaceRequired(source,
            "            throw new System.NotSupportedException(\"NotesMIMEEntity.HeaderObjects requires verified Domino MIME entity header enumeration; managed root-stream header parsing is intentionally not used.\");",
            "            return GetHeaders();",
            "MIME entity HeaderObjects");

        source = ReplaceRequired(source,
            "        throw new System.NotSupportedException(\"NotesMIMEEntity.GetSomeHeaders requires verified Domino MIME entity header enumeration; managed root-stream header parsing is intentionally not used.\");",
            "        return ReadEntityHeadersText(\"GetSomeHeaders\", XPScriptRuntime.CStr(headerNamesValue));",
            "MIME entity GetSomeHeaders");

        source = ReplaceRequired(source,
            "    private static string MimeSymbolText(int symbol) => symbol switch",
            "    public object InputStream { get { EnsureEntityAlive(); return ReadEntityInputStream(\"InputStream\", true); } }\n    public object GetInputStream(object? decodeValue) { EnsureEntityAlive(); return ReadEntityInputStream(\"GetInputStream\", XPScriptRuntime.CBool(decodeValue)); }\n    public object Reader { get { EnsureEntityAlive(); return ReadEntityReader(\"Reader\"); } }\n\n" + RootMutationHelpers + "\n    private static string MimeSymbolText(int symbol) => symbol switch",
            "root MIME mutation helpers");

        return source;
    }

    private const string RootMutationHelpers = """
    private void EnsureRootEntity(string member)
    {
        EnsureEntityAlive();
        if (_nativeEntity != _mimeDirectoryOwner.RootEntity)
            throw new System.NotSupportedException("NotesMIMEEntity." + member + " currently supports the root entity only; verified Domino child-entity content access is not available.");
    }

    private byte[] ReadRootEntity(string member)
    {
        EnsureRootEntity(member);
        return Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
    }

    private byte[] ReadRootContent(string member)
    {
        var raw = ReadRootEntity(member);
        var bodyOffset = FindRootBodyOffset(raw);
        var body = bodyOffset >= raw.Length ? [] : raw[bodyOffset..];
        var transferEncoding = GetRootHeader(raw, bodyOffset, "Content-Transfer-Encoding").Trim();

        if (transferEncoding.Equals("base64", StringComparison.OrdinalIgnoreCase))
        {
            try { return Convert.FromBase64String(System.Text.Encoding.ASCII.GetString(body)); }
            catch (FormatException ex) { throw new XPScriptRuntimeException(5, "Invalid base64 MIME root content: " + ex.Message); }
        }

        if (transferEncoding.Equals("quoted-printable", StringComparison.OrdinalIgnoreCase))
            return DecodeRootQuotedPrintable(body);

        return body;
    }

    private string ReadRootText(string member)
    {
        var content = ReadRootContent(member);
        var charset = _mimeDirectoryOwner.TypeParam(_nativeEntity, XPScriptNotesConst.MIME_SYMBOL_CHARSET).Trim();
        if (charset.Length == 0) return System.Text.Encoding.UTF8.GetString(content);
        try { return System.Text.Encoding.GetEncoding(charset).GetString(content); }
        catch (ArgumentException ex) { throw new XPScriptRuntimeException(5, "Unsupported MIME root charset '" + charset + "': " + ex.Message); }
    }

    private static int FindRootBodyOffset(byte[] raw)
    {
        for (var i = 0; i + 3 < raw.Length; i++)
            if (raw[i] == 13 && raw[i + 1] == 10 && raw[i + 2] == 13 && raw[i + 3] == 10)
                return i + 4;

        for (var i = 0; i + 1 < raw.Length; i++)
            if (raw[i] == 10 && raw[i + 1] == 10)
                return i + 2;

        return raw.Length;
    }

    private static string GetRootHeader(byte[] raw, int bodyOffset, string name)
    {
        var headerLength = Math.Max(0, Math.Min(raw.Length, bodyOffset));
        var text = System.Text.Encoding.Latin1.GetString(raw, 0, headerLength)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var currentName = "";
        var currentValue = new System.Text.StringBuilder();

        foreach (var line in text.Split('\n'))
        {
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && currentName.Length > 0)
            {
                currentValue.Append(' ').Append(line.Trim());
                continue;
            }

            if (currentName.Equals(name, StringComparison.OrdinalIgnoreCase))
                return currentValue.ToString();

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                currentName = "";
                currentValue.Clear();
                continue;
            }

            currentName = line[..colon].Trim();
            currentValue.Clear();
            currentValue.Append(line[(colon + 1)..].Trim());
        }

        return currentName.Equals(name, StringComparison.OrdinalIgnoreCase) ? currentValue.ToString() : "";
    }

    private static byte[] DecodeRootQuotedPrintable(byte[] input)
    {
        using var output = new MemoryStream();
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] == '=' && i + 1 < input.Length && (input[i + 1] == '\r' || input[i + 1] == '\n'))
            {
                if (input[i + 1] == '\r' && i + 2 < input.Length && input[i + 2] == '\n') i += 2;
                else i++;
                continue;
            }

            if (input[i] == '=' && i + 2 < input.Length && TryRootHex(input[i + 1], out var hi) && TryRootHex(input[i + 2], out var lo))
            {
                output.WriteByte((byte)((hi << 4) | lo));
                i += 2;
                continue;
            }

            output.WriteByte(input[i]);
        }
        return output.ToArray();
    }

    private static bool TryRootHex(byte value, out int result)
    {
        if (value >= '0' && value <= '9') { result = value - '0'; return true; }
        if (value >= 'A' && value <= 'F') { result = value - 'A' + 10; return true; }
        if (value >= 'a' && value <= 'f') { result = value - 'a' + 10; return true; }
        result = 0;
        return false;
    }

    private void WriteRootContent(byte[] data, string contentType, int encoding, string member)
    {
        EnsureRootEntity(member);

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
