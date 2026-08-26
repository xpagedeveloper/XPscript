namespace XPScript.Compiler;

internal static class NotesNativeApiPasswordSource
{
    public const string Code = """
internal sealed partial class XPScriptNotesNativeApi
{
    private const uint KfmSwitchIdDontSetEnvVar = 0x00000008;

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
                userNameCapacity - 1,
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

    private string GetEnvironmentString(string name)
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
                capacity - 1);
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
    private delegate int OSGetEnvironmentStringDelegate(nint variableName, nint returnValueBuffer, int bufferLength);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort SECKFMSwitchToIDFileDelegate(
        nint idFileName,
        nint password,
        nint userName,
        int maxUserNameLength,
        uint flags,
        nint reserved);
}
""";
}
