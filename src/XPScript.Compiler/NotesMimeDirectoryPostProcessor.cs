namespace XPScript.Compiler;

internal static class NotesMimeDirectoryPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source + "\n\n" + NativeRuntime;
    }

    private const string NativeRuntime = """
internal sealed partial class XPScriptNotesNativeApi
{
    internal nint OpenMimeDirectory(uint note)
    {
        EnsureInitialized();
        Check(Resolve<MIMEOpenDirectoryDelegate>("MIMEOpenDirectory")(note, out var directory), "MIMEOpenDirectory");
        return directory;
    }

    internal void FreeMimeDirectory(nint directory)
    {
        if (directory == 0) return;
        EnsureInitialized();
        Resolve<MIMEFreeDirectoryDelegate>("MIMEFreeDirectory")(directory);
    }

    internal nint GetMimeRootEntity(nint directory)
    {
        EnsureInitialized();
        Check(Resolve<MIMEGetRootEntityDelegate>("MIMEGetRootEntity")(directory, out var entity), "MIMEGetRootEntity");
        return entity;
    }

    internal nint GetMimeFirstSubpart(nint entity)
    {
        EnsureInitialized();
        return Resolve<MIMEGetFirstSubpartDelegate>("MIMEGetFirstSubpart")(entity);
    }

    internal nint GetMimeNextSibling(nint entity)
    {
        EnsureInitialized();
        return Resolve<MIMEGetNextSiblingDelegate>("MIMEGetNextSibling")(entity);
    }

    internal nint GetMimePrevSibling(nint entity)
    {
        EnsureInitialized();
        return Resolve<MIMEGetPrevSiblingDelegate>("MIMEGetPrevSibling")(entity);
    }

    internal nint GetMimeParent(nint entity)
    {
        EnsureInitialized();
        return Resolve<MIMEGetParentDelegate>("MIMEGetParent")(entity);
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MIMEOpenDirectoryDelegate(uint note, out nint directory);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate void MIMEFreeDirectoryDelegate(nint directory);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MIMEGetRootEntityDelegate(nint directory, out nint entity);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate nint MIMEGetFirstSubpartDelegate(nint entity);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate nint MIMEGetNextSiblingDelegate(nint entity);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate nint MIMEGetPrevSiblingDelegate(nint entity);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate nint MIMEGetParentDelegate(nint entity);
}
""";
}
