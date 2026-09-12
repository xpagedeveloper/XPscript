namespace XPScript.Compiler;

internal static class NotesNativeApiPasswordSource
{
    public const string Code = """
internal sealed partial class XPScriptNotesNativeApi
{
    private const uint KfmSwitchIdDontSetEnvVar = 0x00000008;
    private const int PasswordDigestCapacity = 2048;

    internal void Initialize(string? notesIni, string? idPassword)
    {
        Initialize(notesIni);
        if (string.IsNullOrEmpty(idPassword)) return;
        AuthenticateCurrentId(idPassword);
    }

    private void AuthenticateCurrentId(string idPassword)
    {
        EnsureInitialized();

        var idFile = GetEnvironmentString("KeyFileName");
        if (idFile.Length == 0)
            idFile = GetEnvironmentString("ServerKeyFileName");
        if (idFile.Length == 0)
            throw new XPScriptRuntimeException(5, "Unable to determine the Notes/Domino ID file from KeyFileName or ServerKeyFileName.");

        using var idFileText = ToLmbcs(idFile);
        using var passwordText = ToLmbcs(idPassword);
        const int userNameCapacity = 2048;
        var userName = System.Runtime.InteropServices.Marshal.AllocHGlobal(userNameCapacity);
        try
        {
            Zero(userName, userNameCapacity);
            var status = Resolve<SECKFMSwitchToIDFileDelegate>("SECKFMSwitchToIDFile")(
                idFileText.Pointer,
                passwordText.Pointer,
                userName,
                checked((ushort)(userNameCapacity - 1)),
                KfmSwitchIdDontSetEnvVar,
                0);
            Check(status, "SECKFMSwitchToIDFile");
        }
        finally
        {
            Zero(passwordText.Pointer, passwordText.Length + 1);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(userName);
        }
    }

    internal string HashPassword(string password)
    {
        EnsureInitialized();
        using var passwordText = ToLmbcs(password);
        if (passwordText.Length > ushort.MaxValue)
            throw new XPScriptRuntimeException(5, "Password is too long for NotesSession.HashPassword.");

        var digest = System.Runtime.InteropServices.Marshal.AllocHGlobal(PasswordDigestCapacity);
        try
        {
            Zero(digest, PasswordDigestCapacity);
            var status = Resolve<SECHashPasswordDelegate>("SECHashPassword")(
                checked((ushort)passwordText.Length),
                passwordText.Pointer,
                checked((ushort)PasswordDigestCapacity),
                out var digestLength,
                digest,
                0,
                0);
            Check(status, "SECHashPassword");
            if (digestLength >= PasswordDigestCapacity)
                throw new XPScriptRuntimeException(5, "SECHashPassword returned an invalid digest length.");
            return FromLmbcs(digest, digestLength);
        }
        finally
        {
            Zero(passwordText.Pointer, passwordText.Length + 1);
            Zero(digest, PasswordDigestCapacity);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(digest);
        }
    }

    internal bool VerifyPassword(string password, string hashedPassword)
    {
        EnsureInitialized();
        using var passwordText = ToLmbcs(password);
        using var digestText = ToLmbcs(hashedPassword);
        if (passwordText.Length > ushort.MaxValue || digestText.Length > ushort.MaxValue)
            return false;

        try
        {
            return Resolve<SECVerifyPasswordDelegate>("SECVerifyPassword")(
                checked((ushort)passwordText.Length),
                passwordText.Pointer,
                checked((ushort)digestText.Length),
                digestText.Pointer,
                0,
                0) == 0;
        }
        finally
        {
            Zero(passwordText.Pointer, passwordText.Length + 1);
        }
    }

    internal string GetEnvironmentString(string name)
    {
        EnsureInitialized();
        using var variableName = ToLmbcs(name);
        const int capacity = 4096;
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(buffer, capacity);
            var found = Resolve<OSGetEnvironmentStringDelegate>("OSGetEnvironmentString")(
                variableName.Pointer,
                buffer,
                checked((ushort)(capacity - 1)));
            return found == 0 ? "" : FromLmbcsZeroTerminated(buffer, capacity - 1);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
        }
    }

    // Kept as a no-op so session cleanup remains source-compatible with the
    // earlier password-hook implementation. Password authentication no longer
    // registers an Extension Manager hook in a standalone C API process.
    internal void ReleasePasswordHook()
    {
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate int OSGetEnvironmentStringDelegate(nint variableName, nint returnValueBuffer, ushort bufferLength);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort SECKFMSwitchToIDFileDelegate(
        nint idFileName,
        nint password,
        nint userName,
        ushort maxUserNameLength,
        uint flags,
        nint reserved);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort SECHashPasswordDelegate(
        ushort passwordLength,
        nint password,
        ushort maximumDigestLength,
        out ushort digestLength,
        nint digest,
        uint reservedFlags,
        nint reserved);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort SECVerifyPasswordDelegate(
        ushort passwordLength,
        nint password,
        ushort digestLength,
        nint digest,
        uint reservedFlags,
        nint reserved);
}
""";
}
