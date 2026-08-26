namespace XPScript.Compiler;

internal static class NotesNativeApiDatabaseSource
{
    public const string Code = """
internal sealed partial class XPScriptNotesNativeApi
{
    private const int NsfInfoSize = 128;
    private const ushort InfoParseTitle = 0;
    private const ushort InfoParseCategories = 1;
    private const ushort InfoParseClass = 2;
    private const ushort InfoParseDesignClass = 3;

    internal string GetDatabaseTitle(nint db) => GetDatabaseInfoPart(db, InfoParseTitle);
    internal string GetDatabaseCategories(nint db) => GetDatabaseInfoPart(db, InfoParseCategories);
    internal string GetDatabaseTemplateName(nint db) => GetDatabaseInfoPart(db, InfoParseClass);
    internal string GetDatabaseDesignTemplateName(nint db) => GetDatabaseInfoPart(db, InfoParseDesignClass);

    internal void SetDatabaseTitle(nint db, string value) => SetDatabaseInfoPart(db, InfoParseTitle, value);
    internal void SetDatabaseCategories(nint db, string value) => SetDatabaseInfoPart(db, InfoParseCategories, value);

    private string GetDatabaseInfoPart(nint db, ushort what)
    {
        EnsureInitialized();
        var info = System.Runtime.InteropServices.Marshal.AllocHGlobal(NsfInfoSize);
        var output = System.Runtime.InteropServices.Marshal.AllocHGlobal(NsfInfoSize);
        try
        {
            Zero(info, NsfInfoSize);
            Zero(output, NsfInfoSize);
            Check(Resolve<NSFDbInfoGetDelegate>("NSFDbInfoGet")(db, info), "NSFDbInfoGet");
            Resolve<NSFDbInfoParseDelegate>("NSFDbInfoParse")(info, what, output, NsfInfoSize - 1);
            return FromLmbcsZeroTerminated(output, NsfInfoSize - 1);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(output);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(info);
        }
    }

    private void SetDatabaseInfoPart(nint db, ushort what, string value)
    {
        EnsureInitialized();
        var info = System.Runtime.InteropServices.Marshal.AllocHGlobal(NsfInfoSize);
        try
        {
            Zero(info, NsfInfoSize);
            Check(Resolve<NSFDbInfoGetDelegate>("NSFDbInfoGet")(db, info), "NSFDbInfoGet");
            using var text = ToLmbcs(value ?? "");
            Resolve<NSFDbInfoModifyDelegate>("NSFDbInfoModify")(info, what, text.Pointer);
            Check(Resolve<NSFDbInfoSetDelegate>("NSFDbInfoSet")(db, info), "NSFDbInfoSet");
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(info); }
    }

    internal string GetDatabaseReplicaId(nint db)
    {
        EnsureInitialized();
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(32);
        try
        {
            Zero(buffer, 32);
            Check(Resolve<NSFDbReplicaInfoGetDelegate>("NSFDbReplicaInfoGet")(db, buffer), "NSFDbReplicaInfoGet");
            var innards0 = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(buffer, 0));
            var innards1 = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(buffer, 4));
            return innards1.ToString("X8", System.Globalization.CultureInfo.InvariantCulture) +
                   innards0.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
    }

    internal (long Size, double PercentUsed) GetDatabaseSpaceUsage(nint db)
    {
        EnsureInitialized();
        Check(Resolve<NSFDbSpaceUsageDelegate>("NSFDbSpaceUsage")(db, out var allocated, out var free), "NSFDbSpaceUsage");
        var total = (long)allocated + free;
        var percent = total == 0 ? 0d : allocated * 100d / total;
        return (total, percent);
    }

    internal int GetDatabaseCurrentAccessLevel(nint db)
    {
        EnsureInitialized();
        Resolve<NSFDbAccessGetDelegate>("NSFDbAccessGet")(db, out var level, out _);
        return level;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFDbInfoGetDelegate(nint db, nint buffer);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate void NSFDbInfoParseDelegate(nint info, ushort what, nint buffer, int length);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate void NSFDbInfoModifyDelegate(nint info, ushort what, nint buffer);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFDbInfoSetDelegate(nint db, nint buffer);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFDbReplicaInfoGetDelegate(nint db, nint replicaInfo);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFDbSpaceUsageDelegate(nint db, out uint allocatedBytes, out uint freeBytes);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate void NSFDbAccessGetDelegate(nint db, out ushort accessLevel, out ushort accessFlags);
}
""";
}
