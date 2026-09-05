namespace XPScript.Compiler;

internal static class NotesSigningPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    public XPScriptNotesDocument CreateDocument()",
            """
    public void Sign()
    {
        EnsureAlive();
        if (!IsOpen) throw new XPScriptRuntimeException(91, "NotesDatabase is not open.");
        var design = CreateNoteCollection(false);
        try
        {
            design.SelectAllDesignElements(true);
            design.BuildCollection();
            foreach (var noteId in design.NativeNoteIds)
                Session.Api.SignNoteById(Handle, noteId);
        }
        finally { design.Recycle(); }
    }

    public XPScriptNotesDocument CreateDocument()
""",
            "database-sign");

        source = ReplaceRequired(source,
            "    public bool Remove(object? forceValue)",
            """
    public void Sign()
    {
        EnsureAlive();
        if (_handle == 0) throw new XPScriptRuntimeException(91, "NotesDocument has no open note handle.");
        Session.Api.SignNote(_handle);
    }

    public bool IsSigned
    {
        get
        {
            EnsureAlive();
            return _handle != 0 && Session.Api.HasItem(_handle, "$Signature");
        }
    }

    public string Signer
    {
        get
        {
            EnsureAlive();
            return _handle == 0 ? "" : Session.Api.GetNoteSignatureInfo(_handle).Signer;
        }
    }

    public string Verifier
    {
        get
        {
            EnsureAlive();
            return _handle == 0 ? "" : Session.Api.GetNoteSignatureInfo(_handle).Verifier;
        }
    }

    public bool Remove(object? forceValue)
""",
            "document-sign");

        const string nativeSigning = """
    internal void SignNote(uint note)
    {
        EnsureInitialized();
        Check(Resolve<NSFNoteSignDelegate>("NSFNoteSign")(note), "NSFNoteSign");
    }

    internal (string Signer, string Verifier) GetNoteSignatureInfo(uint note)
    {
        EnsureInitialized();
        if (!HasItem(note, "$Signature")) return ("", "");

        const int capacity = 2048;
        var signer = System.Runtime.InteropServices.Marshal.AllocHGlobal(capacity);
        var verifier = System.Runtime.InteropServices.Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(signer, capacity);
            Zero(verifier, capacity);
            var status = Resolve<NSFNoteVerifySignatureDelegate>("NSFNoteVerifySignature")(note, 0, 0, signer, verifier);
            if ((status & 0x3fff) != 0) return ("", "");
            return (FromLmbcsZeroTerminated(signer, capacity - 1), FromLmbcsZeroTerminated(verifier, capacity - 1));
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(verifier);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(signer);
        }
    }

    internal void SignNoteById(uint db, uint noteId)
    {
        EnsureInitialized();
        Check(Resolve<NSFNoteOpenDelegate>("NSFNoteOpen")(db, noteId, 0, out var note), "NSFNoteOpen(sign)");
        try
        {
            SignNote(note);
            Check(Resolve<NSFNoteUpdateDelegate>("NSFNoteUpdate")(note, 0), "NSFNoteUpdate(sign)");
        }
        finally { CloseNote(note); }
    }

""";
        source = InsertBeforeRequired(source, "    internal string RunAgent(", nativeSigning, "native-sign");

        const string signatureDelegate = """

[System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
internal delegate ushort NSFNoteVerifySignatureDelegate(uint note, nint signatureItemName, nint retWhenSigned, nint retSigner, nint retCertifier);
""";
        source += signatureDelegate;

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes signing surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }

    private static string InsertBeforeRequired(string source, string marker, string insertion, string stage)
    {
        var index = source.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
            throw new CompilerException("Unable to apply Notes signing surface (" + stage + ").");
        return source.Insert(index, insertion);
    }
}
