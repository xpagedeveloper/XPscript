namespace XPScript.Compiler;

internal static class NotesMailPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string streamFactory = """
    public XPScriptNotesStream CreateStream()
    {
        EnsureAlive();
        return new XPScriptNotesStream(this);
    }
""";

        const string sessionReplacement = """
    public XPScriptNotesMail CreateMail()
    {
        EnsureAlive();
        return new XPScriptNotesMail(this);
    }

    public XPScriptNotesStream CreateStream()
    {
        EnsureAlive();
        return new XPScriptNotesStream(this);
    }
""";

        if (!source.Contains(streamFactory, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesMail session factory.");
        source = source.Replace(streamFactory, sessionReplacement, StringComparison.Ordinal);

        source += "\n\n" + MailRuntime;
        return source;
    }

    private const string MailRuntime = """
internal sealed class XPScriptNotesMail : XPScriptNotesObject
{
    private enum BodyKind
    {
        Text,
        RichText,
        Mime
    }

    private sealed record Attachment(string Path, string FileName, string ContentType);

    private string[] _sendTo = [];
    private string[] _copyTo = [];
    private string[] _blindCopyTo = [];
    private string _from = "";
    private string _replyTo = "";
    private string _principal = "";
    private string _subject = "";
    private string _bodyText = "";
    private byte[] _mimeBody = [];
    private string _mimeContentType = "text/plain; charset=UTF-8";
    private XPScriptNotesRichTextItem? _richTextBody;
    private readonly List<Attachment> _attachments = [];
    private BodyKind _bodyKind = BodyKind.Text;
    private string _deliveryPriority = "N";
    private string _deliveryReport = "N";
    private bool _returnReceipt;

    internal XPScriptNotesMail(XPScriptNotesSession session) : base(session)
    {
        _from = session.UserName;
    }

    public object SendTo
    {
        get { EnsureAlive(); return ToVariantValue(_sendTo); }
        set { EnsureAlive(); _sendTo = NormalizeRecipients(value, "SendTo"); }
    }

    public object CopyTo
    {
        get { EnsureAlive(); return ToVariantValue(_copyTo); }
        set { EnsureAlive(); _copyTo = NormalizeRecipients(value, "CopyTo", allowEmpty: true); }
    }

    public object BlindCopyTo
    {
        get { EnsureAlive(); return ToVariantValue(_blindCopyTo); }
        set { EnsureAlive(); _blindCopyTo = NormalizeRecipients(value, "BlindCopyTo", allowEmpty: true); }
    }

    public string From
    {
        get { EnsureAlive(); return _from; }
        set { EnsureAlive(); _from = (value ?? "").Trim(); }
    }

    public string ReplyTo
    {
        get { EnsureAlive(); return _replyTo; }
        set { EnsureAlive(); _replyTo = (value ?? "").Trim(); }
    }

    public string Principal
    {
        get { EnsureAlive(); return _principal; }
        set { EnsureAlive(); _principal = (value ?? "").Trim(); }
    }

    public string Subject
    {
        get { EnsureAlive(); return _subject; }
        set { EnsureAlive(); _subject = value ?? ""; }
    }

    public string Body
    {
        get { EnsureAlive(); return _bodyKind == BodyKind.Text ? _bodyText : ""; }
        set { SetBodyText(value); }
    }

    public string DeliveryPriority
    {
        get { EnsureAlive(); return _deliveryPriority; }
        set
        {
            EnsureAlive();
            var priority = (value ?? "").Trim().ToUpperInvariant();
            _deliveryPriority = priority switch
            {
                "H" or "HIGH" => "H",
                "L" or "LOW" => "L",
                "N" or "NORMAL" or "" => "N",
                _ => throw new XPScriptRuntimeException(5, "NotesMail.DeliveryPriority must be High/H, Normal/N, or Low/L.")
            };
        }
    }

    public string DeliveryReport
    {
        get { EnsureAlive(); return _deliveryReport; }
        set
        {
            EnsureAlive();
            var report = (value ?? "").Trim().ToUpperInvariant();
            _deliveryReport = report switch
            {
                "N" or "NONE" or "0" or "" => "N",
                "B" or "FAILURE" or "FAILUREONLY" or "1" => "B",
                "C" or "CONFIRM" or "CONFIRMDELIVERY" or "2" => "C",
                _ => throw new XPScriptRuntimeException(5, "NotesMail.DeliveryReport must be None/N, Failure/B, or Confirm/C.")
            };
        }
    }

    public bool ReturnReceipt
    {
        get { EnsureAlive(); return _returnReceipt; }
        set { EnsureAlive(); _returnReceipt = value; }
    }

    public bool IsMIME { get { EnsureAlive(); return _bodyKind == BodyKind.Mime || _attachments.Count > 0; } }
    public int AttachmentCount { get { EnsureAlive(); return _attachments.Count; } }

    public void SetBodyText(object? textValue)
    {
        EnsureAlive();
        _bodyText = XPScriptRuntime.CStr(textValue);
        _mimeBody = [];
        _mimeContentType = "text/plain; charset=UTF-8";
        _richTextBody = null;
        _bodyKind = BodyKind.Text;
    }

    public void SetBodyRichText(object? richTextValue)
    {
        EnsureAlive();
        if (richTextValue is not XPScriptNotesRichTextItem richText)
            throw new XPScriptRuntimeException(13, "NotesMail.SetBodyRichText requires a NotesRichTextItem.");
        _richTextBody = richText;
        _bodyText = "";
        _mimeBody = [];
        _bodyKind = BodyKind.RichText;
    }

    public void SetBodyHTML(object? htmlValue)
    {
        EnsureAlive();
        SetMimeBytes(System.Text.Encoding.UTF8.GetBytes(XPScriptRuntime.CStr(htmlValue)), "text/html; charset=UTF-8");
    }

    public void SetMIME(object? contentValue, object? contentTypeValue)
    {
        EnsureAlive();
        var contentType = XPScriptRuntime.CStr(contentTypeValue).Trim();
        if (contentType.Length == 0) contentType = "application/octet-stream";
        ValidateHeaderValue(contentType, "MIME content type");

        if (contentValue is XPScriptNotesStream stream)
        {
            SetMimeBytes((byte[])stream.Read(), contentType);
            return;
        }

        if (contentValue is byte[] bytes)
        {
            SetMimeBytes(bytes.ToArray(), contentType);
            return;
        }

        SetMimeBytes(System.Text.Encoding.UTF8.GetBytes(XPScriptRuntime.CStr(contentValue)), contentType);
    }

    public void SetMIMEBody(object? contentValue, object? contentTypeValue) => SetMIME(contentValue, contentTypeValue);

    public void AddAttachment(object? pathValue) => AddAttachment(pathValue, null, null);
    public void AddAttachment(object? pathValue, object? fileNameValue) => AddAttachment(pathValue, fileNameValue, null);

    public void AddAttachment(object? pathValue, object? fileNameValue, object? contentTypeValue)
    {
        EnsureAlive();
        var path = XPScriptRuntime.CStr(pathValue).Trim();
        if (path.Length == 0) throw new XPScriptRuntimeException(5, "NotesMail attachment path cannot be empty.");
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new XPScriptRuntimeException(53, "NotesMail attachment was not found: " + fullPath);

        var fileName = XPScriptRuntime.CStr(fileNameValue).Trim();
        if (fileName.Length == 0) fileName = Path.GetFileName(fullPath);
        fileName = SanitizeHeaderToken(fileName);
        if (fileName.Length == 0) throw new XPScriptRuntimeException(5, "NotesMail attachment file name cannot be empty.");

        var contentType = XPScriptRuntime.CStr(contentTypeValue).Trim();
        if (contentType.Length == 0) contentType = "application/octet-stream";
        ValidateHeaderValue(contentType, "attachment content type");
        _attachments.Add(new Attachment(fullPath, fileName, contentType));
    }

    public void ClearAttachments()
    {
        EnsureAlive();
        _attachments.Clear();
    }

    public void Send()
    {
        EnsureAlive();
        if (_sendTo.Length + _copyTo.Length + _blindCopyTo.Length == 0)
            throw new XPScriptRuntimeException(5, "NotesMail requires at least one recipient in SendTo, CopyTo, or BlindCopyTo.");
        if (_bodyKind == BodyKind.RichText && _attachments.Count > 0)
            throw new XPScriptRuntimeException(5, "NotesMail.AddAttachment cannot be combined with SetBodyRichText. Add attachments to the NotesRichTextItem before assigning it, or use a MIME body.");

        var database = OpenTransientDatabase();
        try
        {
            var document = database.CreateDocument();
            try
            {
                PopulateHeaders(document);
                PopulateBody(document);
                document.Send(false);
            }
            finally { document.Recycle(); }
        }
        finally { database.Recycle(); }
    }

    private void PopulateHeaders(XPScriptNotesDocument document)
    {
        document.ReplaceItemValue("Form", "Memo");
        document.ReplaceItemValue("SendTo", ToItemValue(_sendTo));
        if (_copyTo.Length > 0) document.ReplaceItemValue("CopyTo", ToItemValue(_copyTo));
        if (_blindCopyTo.Length > 0) document.ReplaceItemValue("BlindCopyTo", ToItemValue(_blindCopyTo));
        if (_from.Length > 0) document.ReplaceItemValue("From", _from);
        if (_replyTo.Length > 0) document.ReplaceItemValue("ReplyTo", _replyTo);
        if (_principal.Length > 0) document.ReplaceItemValue("Principal", _principal);
        if (_subject.Length > 0) document.ReplaceItemValue("Subject", _subject);
        document.ReplaceItemValue("DeliveryPriority", _deliveryPriority);
        document.ReplaceItemValue("DeliveryReport", _deliveryReport);
        document.ReplaceItemValue("ReturnReceipt", _returnReceipt ? "1" : "0");

        var composed = Session.CreateDateTimeNow();
        try { document.ReplaceItemValue("ComposedDate", composed); }
        finally { composed.Recycle(); }
    }

    private void PopulateBody(XPScriptNotesDocument document)
    {
        switch (_bodyKind)
        {
            case BodyKind.RichText:
                _richTextBody!.CopyToDocument(document, "Body");
                return;
            case BodyKind.Mime:
                WriteMimeBody(document, _mimeBody, _mimeContentType);
                return;
            default:
                if (_attachments.Count == 0)
                {
                    document.ReplaceItemValue("Body", _bodyText);
                    return;
                }
                WriteMimeBody(document, System.Text.Encoding.UTF8.GetBytes(_bodyText), "text/plain; charset=UTF-8");
                return;
        }
    }

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

    private XPScriptNotesDatabase OpenTransientDatabase()
    {
        var server = Session.Api.GetEnvironmentString("MailServer").Trim();
        var mailFile = Session.Api.GetEnvironmentString("MailFile").Trim();

        if (mailFile.Length > 0)
        {
            var mailDb = Session.OpenDatabase(server, mailFile);
            if (mailDb.IsOpen) return mailDb;
            mailDb.Recycle();
            if (server.Length > 0)
            {
                mailDb = Session.OpenDatabase("", mailFile);
                if (mailDb.IsOpen) return mailDb;
                mailDb.Recycle();
            }
        }

        var names = Session.OpenDatabase("", "names.nsf");
        if (names.IsOpen) return names;
        names.Recycle();

        var mailBox = Session.OpenDatabase(server, "mail.box");
        if (mailBox.IsOpen) return mailBox;
        mailBox.Recycle();

        throw new XPScriptRuntimeException(91, "NotesMail could not open a temporary Notes database. Configure MailFile/MailServer or make names.nsf available.");
    }

    private void SetMimeBytes(byte[] bytes, string contentType)
    {
        _mimeBody = bytes;
        _mimeContentType = contentType;
        _bodyText = "";
        _richTextBody = null;
        _bodyKind = BodyKind.Mime;
    }

    private static string[] NormalizeRecipients(object? value, string property, bool allowEmpty = false)
    {
        var values = new List<string>();
        if (value is LSArray array)
        {
            if (array.IsAllocated)
            {
                if (array.Rank != 1) throw new XPScriptRuntimeException(13, "NotesMail." + property + " requires a one-dimensional array.");
                for (var i = array.LBound(); i <= array.UBound(); i++) AddRecipient(values, array.Get(i));
            }
        }
        else AddRecipient(values, value);

        if (!allowEmpty && values.Count == 0)
            throw new XPScriptRuntimeException(5, "NotesMail." + property + " cannot be empty.");
        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddRecipient(List<string> values, object? value)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (text.Length > 0) values.Add(text);
    }

    private static object ToVariantValue(string[] values)
    {
        if (values.Length == 0) return "";
        if (values.Length == 1) return values[0];
        return ToItemValue(values);
    }

    private static object ToItemValue(string[] values)
    {
        if (values.Length == 1) return values[0];
        var array = new LSArray("String", true, [0], [values.Length - 1]);
        for (var i = 0; i < values.Length; i++) array.Set(values[i], i);
        return array;
    }

    private static string SanitizeHeaderToken(string value) =>
        value.Replace("\r", "", StringComparison.Ordinal)
             .Replace("\n", "", StringComparison.Ordinal)
             .Replace('"', '\'');

    private static void ValidateHeaderValue(string value, string label)
    {
        if (value.Contains('\r') || value.Contains('\n'))
            throw new XPScriptRuntimeException(5, "NotesMail " + label + " cannot contain CR or LF characters.");
    }

    protected override void ReleaseNative()
    {
        _richTextBody = null;
        _mimeBody = [];
        _attachments.Clear();
        _sendTo = [];
        _copyTo = [];
        _blindCopyTo = [];
    }
}
""";
}
