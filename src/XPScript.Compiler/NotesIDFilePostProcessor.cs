namespace XPScript.Compiler;

internal static class NotesIDFilePostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string sessionMarker = "    public XPScriptNotesDateTime CreateDateTimeNow()\n    {\n        EnsureAlive();\n        return XPScriptNotesDateTime.CreateNow(this);\n    }";
        const string sessionReplacement = sessionMarker + "\n\n" + """
    public XPScriptNotesIDFile OpenIDFile(object? filePath, object? password)
    {
        EnsureAlive();
        return new XPScriptNotesIDFile(this, XPScriptRuntime.CStr(filePath), XPScriptRuntime.CStr(password));
    }
""";

        if (!source.Contains(sessionMarker, StringComparison.Ordinal))
            throw new InvalidOperationException("NotesIDFilePostProcessor could not find NotesSession insertion point.");

        source = source.Replace(sessionMarker, sessionReplacement, StringComparison.Ordinal);
        return source + "\n\n" + IDFileRuntime;
    }

    private const string IDFileRuntime = """
internal sealed class XPScriptNotesIDFile : XPScriptNotesObject
{
    private readonly string _password;

    internal XPScriptNotesIDFile(XPScriptNotesSession session, string filePath, string password) : base(session)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new XPScriptRuntimeException(5, "NotesIDFile requires an ID file path.");

        FilePath = Path.GetFullPath(filePath);
        if (!File.Exists(FilePath))
            throw new XPScriptRuntimeException(53, "Notes ID file was not found: " + FilePath);

        _password = password ?? "";
        // Validate that the supplied password can actually open the ID file.
        Session.Api.ValidateIDFile(FilePath, _password);
        Name = Session.Api.GetIDFileName(FilePath);
        PublicKey = Session.Api.GetIDFilePublicKey(FilePath);
        Certifier = GetParentCertifier(Name);
    }

    public string FilePath { get; }
    public string Name { get; }
    public string PublicKey { get; }

    // A Notes certified public key/certificate is not the same blob as REGIDGetPublicKey.
    // Keep this property explicit instead of silently returning the raw public key.
    public string Certificate
    {
        get
        {
            EnsureAlive();
            throw new XPScriptRuntimeException(4458, "NotesIDFile.Certificate requires certified-public-key enumeration support and is not available from REGGetIDInfo.");
        }
    }

    public string Certifier { get; }

    public object Expiration
    {
        get
        {
            EnsureAlive();
            throw new XPScriptRuntimeException(4458, "NotesIDFile.Expiration requires Notes certificate enumeration support and is not available from REGGetIDInfo.");
        }
    }

    protected override void ReleaseNative() { }

    private static string GetParentCertifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var slash = name.IndexOf('/');
        return slash < 0 || slash == name.Length - 1 ? "" : name[(slash + 1)..];
    }
}
""";
}
