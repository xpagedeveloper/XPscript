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
    private const ushort ErrMimeNoData = 0x3AF9;

    internal bool NoteHasMimePart(nint note)
    {
        EnsureInitialized();
        return Resolve<NSFNoteHasMIMEPartDelegate>("NSFNoteHasMIMEPart")(note) != 0;
    }

    internal nint OpenMimeDirectory(nint note)
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

    internal nint IterateMimeNext(nint directory, nint topEntity, nint previousEntity)
    {
        EnsureInitialized();
        Check(Resolve<MIMEIterateNextDelegate>("MIMEIterateNext")(directory, topEntity, previousEntity, out var entity), "MIMEIterateNext");
        return entity;
    }

    internal int GetMimeEntityContentTypeSymbol(nint entity)
    {
        EnsureInitialized();
        return Resolve<MIMEEntityContentTypeDelegate>("MIMEEntityContentType")(entity);
    }

    internal int GetMimeEntityContentSubtypeSymbol(nint entity)
    {
        EnsureInitialized();
        return Resolve<MIMEEntityContentSubtypeDelegate>("MIMEEntityContentSubtype")(entity);
    }

    internal string GetMimeEntityTypeParam(nint entity, int symbol)
    {
        EnsureInitialized();
        var status = Resolve<MIMEEntityGetTypeParamDelegate>("MIMEEntityGetTypeParam")(entity, symbol, out var valueHandle, out var valueLength);
        if (status == ErrMimeNoData)
            return string.Empty;
        if (status != 0)
        {
            Check(status, "MIMEEntityGetTypeParam");
            return string.Empty;
        }
        if (valueHandle == 0 || valueLength == 0)
        {
            if (valueHandle != 0) Resolve<OSMemFreeDelegate>("OSMemFree")(valueHandle);
            return string.Empty;
        }

        nint value = 0;
        try
        {
            value = Resolve<OSLockObjectDelegate>("OSLockObject")(valueHandle);
            if (value == 0) throw new XPScriptRuntimeException(5, "Unable to lock MIME type parameter data.");
            var bytes = new byte[checked((int)valueLength)];
            System.Runtime.InteropServices.Marshal.Copy(value, bytes, 0, bytes.Length);
            return System.Text.Encoding.Latin1.GetString(bytes).TrimEnd('\0');
        }
        finally
        {
            if (value != 0) Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(valueHandle);
            Check(Resolve<OSMemFreeDelegate>("OSMemFree")(valueHandle), "OSMemFree(MIMEEntityGetTypeParam)");
        }
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate int NSFNoteHasMIMEPartDelegate(nint note);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MIMEOpenDirectoryDelegate(nint note, out nint directory);
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
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MIMEIterateNextDelegate(nint directory, nint topEntity, nint previousEntity, out nint entity);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate int MIMEEntityContentTypeDelegate(nint entity);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate int MIMEEntityContentSubtypeDelegate(nint entity);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MIMEEntityGetTypeParamDelegate(nint entity, int symbol, out uint valueHandle, out uint valueLength);
}
""";
}
