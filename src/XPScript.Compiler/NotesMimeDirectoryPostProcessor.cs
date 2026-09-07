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
        Check(Resolve<MIMEFreeDirectoryDelegate>("MIMEFreeDirectory")(directory), "MIMEFreeDirectory");
    }

    internal nint GetMimeRootEntity(nint directory)
    {
        EnsureInitialized();
        Check(Resolve<MIMEGetRootEntityDelegate>("MIMEGetRootEntity")(directory, out var entity), "MIMEGetRootEntity");
        return entity;
    }

    internal nint GetMimeFirstSubpart(nint directory, nint entity)
    {
        EnsureInitialized();
        Check(Resolve<MIMEGetFirstSubpartDelegate>("MIMEGetFirstSubpart")(directory, entity, out var child), "MIMEGetFirstSubpart");
        return child;
    }

    internal nint GetMimeNextSibling(nint directory, nint entity)
    {
        EnsureInitialized();
        Check(Resolve<MIMEGetNextSiblingDelegate>("MIMEGetNextSibling")(directory, entity, out var sibling), "MIMEGetNextSibling");
        return sibling;
    }

    internal nint GetMimePrevSibling(nint directory, nint entity)
    {
        EnsureInitialized();
        Check(Resolve<MIMEGetPrevSiblingDelegate>("MIMEGetPrevSibling")(directory, entity, out var sibling), "MIMEGetPrevSibling");
        return sibling;
    }

    internal nint GetMimeParent(nint directory, nint entity)
    {
        EnsureInitialized();
        Check(Resolve<MIMEGetParentDelegate>("MIMEGetParent")(directory, entity, out var parent), "MIMEGetParent");
        return parent;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MIMEOpenDirectoryDelegate(uint note, out nint directory);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MIMEFreeDirectoryDelegate(nint directory);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MIMEGetRootEntityDelegate(nint directory, out nint entity);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MIMEGetFirstSubpartDelegate(nint directory, nint entity, out nint child);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MIMEGetNextSiblingDelegate(nint directory, nint entity, out nint sibling);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MIMEGetPrevSiblingDelegate(nint directory, nint entity, out nint sibling);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MIMEGetParentDelegate(nint directory, nint entity, out nint parent);
}
""";
}
