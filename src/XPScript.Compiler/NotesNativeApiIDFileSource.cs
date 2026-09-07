namespace XPScript.Compiler;

internal static class NotesNativeApiIDFileSource
{
    public const string Code = """
internal sealed partial class XPScriptNotesNativeApi
{
    private const ushort REGIDGetName = 7;
    private const ushort REGIDGetPublicKey = 8;

    internal void ValidateIDFile(string filePath, string password)
    {
        EnsureInitialized();
        using var path = ToLmbcs(filePath);
        using var pwd = ToLmbcs(password);
        nint keyFile = 0;
        Check(Resolve<SECKFMOpenDelegate>("SECKFMOpen")(out keyFile, path.Pointer, pwd.Pointer, 0, 0, 0), "SECKFMOpen");
        if (keyFile == 0) throw new XPScriptRuntimeException(5, "SECKFMOpen returned an empty ID-file context.");
        Check(Resolve<SECKFMCloseDelegate>("SECKFMClose")(ref keyFile, 0, 0, 0), "SECKFMClose");
    }

    internal string GetIDFileName(string filePath)
    {
        var bytes = GetIDFileInfo(filePath, REGIDGetName);
        var length = Array.IndexOf(bytes, (byte)0);
        if (length < 0) length = bytes.Length;
        if (length == 0) return "";
        var ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(length);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, ptr, length);
            return FromLmbcs(ptr, length);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr); }
    }

    internal string GetIDFilePublicKey(string filePath)
    {
        var bytes = GetIDFileInfo(filePath, REGIDGetPublicKey);
        return Convert.ToBase64String(bytes);
    }

    private byte[] GetIDFileInfo(string filePath, ushort infoType)
    {
        EnsureInitialized();
        using var path = ToLmbcs(filePath);
        ushort actualLength = 0;
        var status = Resolve<REGGetIDInfoDelegate>("REGGetIDInfo")(path.Pointer, infoType, 0, 0, out actualLength);
        // REGGetIDInfo documents NULL OutBufr as the supported way to query the required size.
        if (actualLength == 0 && status != 0) Check(status, "REGGetIDInfo");
        if (actualLength == 0) return [];

        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(actualLength);
        try
        {
            Zero(buffer, actualLength);
            Check(Resolve<REGGetIDInfoDelegate>("REGGetIDInfo")(path.Pointer, infoType, buffer, actualLength, out var returnedLength), "REGGetIDInfo");
            var bytes = new byte[returnedLength];
            if (returnedLength > 0) System.Runtime.InteropServices.Marshal.Copy(buffer, bytes, 0, returnedLength);
            return bytes;
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort SECKFMOpenDelegate(out nint keyFile, nint idFileName, nint password, uint flags, uint reserved, nint reservedPointer);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort SECKFMCloseDelegate(ref nint keyFile, uint flags, uint reserved, nint reservedPointer);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort REGGetIDInfoDelegate(nint idFileName, ushort infoType, nint output, ushort outputLength, out ushort actualLength);
}
""";
}
