namespace XPScript.Compiler;

internal static class NotesNativeApiVersionSource
{
    public const string Code = """
internal sealed partial class XPScriptNotesNativeApi
{
    internal long GetRuntimeBuildVersion(long fallback)
    {
        EnsureInitialized();
        nint db = 0;
        try
        {
            db = OpenDatabase("", "names.nsf");
            var status = Resolve<NSFDbGetBuildVersionDelegate>("NSFDbGetBuildVersion")(db, out var build);
            if (status != 0) return fallback;
            return build;
        }
        catch
        {
            return fallback;
        }
        finally
        {
            if (db != 0)
            {
                try { CloseDatabase(db); }
                catch { }
            }
        }
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFDbGetBuildVersionDelegate(nint db, out ushort buildVersion);
}
""";
}
