namespace XPScript.Compiler;

internal static class NotesLtpaPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source + """

internal sealed class XPScriptNotesLTPA : XPScriptNotesObject
{
    private readonly byte[] _secret;
    private readonly int _durationMinutes;

    public XPScriptNotesLTPA(object? sessionValue, object? serverValue, object? configurationValue)
        : base(RequireSession(sessionValue))
    {
        var server = XPScriptRuntime.CStr(serverValue).Trim();
        var configuration = XPScriptRuntime.CStr(configurationValue).Trim();
        if (server.Length == 0) throw new XPScriptRuntimeException(5, "NotesLTPA requires a server name.");
        if (configuration.Length == 0) throw new XPScriptRuntimeException(5, "NotesLTPA requires an SSO configuration name.");

        XPScriptNotesDatabase? names = null;
        XPScriptNotesView? view = null;
        XPScriptNotesDocument? document = null;
        try
        {
            names = Session.OpenDatabase(server, "names.nsf");
            if (!names.IsOpen) throw new XPScriptRuntimeException(5, "Unable to open names.nsf on server " + server + ".");
            view = names.OpenView("($WebSSOConfigs)");
            if (view is null) throw new XPScriptRuntimeException(5, "Unable to open ($WebSSOConfigs) in names.nsf.");
            document = view.GetDocumentByKey(configuration, true);
            if (document is null) throw new XPScriptRuntimeException(5, "Unable to find SSO configuration " + configuration + ".");

            var secretText = document.GetString("LTPA_DominoSecret").Trim();
            if (secretText.Length == 0) throw new XPScriptRuntimeException(5, "SSO configuration has no LTPA_DominoSecret value.");
            try { _secret = Convert.FromBase64String(secretText); }
            catch (FormatException) { throw new XPScriptRuntimeException(5, "SSO configuration contains an invalid LTPA_DominoSecret value."); }
            if (_secret.Length == 0) throw new XPScriptRuntimeException(5, "SSO configuration has an empty LTPA secret.");

            var duration = document.GetNumber("LTPA_TokenExpiration");
            if (duration <= 0 || duration > int.MaxValue) throw new XPScriptRuntimeException(5, "SSO configuration has an invalid LTPA_TokenExpiration value.");
            _durationMinutes = checked((int)duration);
        }
        finally
        {
            document?.Recycle();
            view?.Recycle();
            names?.Recycle();
        }
    }

    public string GenerateToken(object? userNameValue)
    {
        EnsureAlive();
        var userName = XPScriptRuntime.CStr(userNameValue);
        if (userName.Length == 0) throw new XPScriptRuntimeException(5, "NotesLTPA requires a user name.");

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expires = checked(created + (long)_durationMinutes * 60L);
        var tokenBase = new List<byte>(32 + userName.Length);
        tokenBase.AddRange([0, 1, 2, 3]);
        tokenBase.AddRange(System.Text.Encoding.ASCII.GetBytes(ToHexTimestamp(created)));
        tokenBase.AddRange(System.Text.Encoding.ASCII.GetBytes(ToHexTimestamp(expires)));
        using (var encodedName = Session.Api.ToLmbcs(userName))
        {
            var nameBytes = new byte[encodedName.Length];
            if (nameBytes.Length > 0) System.Runtime.InteropServices.Marshal.Copy(encodedName.Pointer, nameBytes, 0, nameBytes.Length);
            tokenBase.AddRange(nameBytes);
        }

        var digestInput = new byte[tokenBase.Count + _secret.Length];
        tokenBase.CopyTo(digestInput, 0);
        Buffer.BlockCopy(_secret, 0, digestInput, tokenBase.Count, _secret.Length);
        var digest = System.Security.Cryptography.SHA1.HashData(digestInput);
        var token = new byte[tokenBase.Count + digest.Length];
        tokenBase.CopyTo(token, 0);
        Buffer.BlockCopy(digest, 0, token, tokenBase.Count, digest.Length);
        return Convert.ToBase64String(token);
    }

    public int DurationMinutes => _durationMinutes;

    protected override void ReleaseNative()
    {
        Array.Clear(_secret, 0, _secret.Length);
    }

    private static XPScriptNotesSession RequireSession(object? value)
        => value as XPScriptNotesSession ?? throw new XPScriptRuntimeException(13, "NotesLTPA requires a NotesSession.");

    private static string ToHexTimestamp(long value)
        => ((uint)value).ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
}
""";
    }
}
