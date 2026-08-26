namespace XPScript.Compiler;

internal static class NotesNativeApiReplicaSource
{
    public const string Code = """
internal sealed partial class XPScriptNotesNativeApi
{
    private const ushort NoteClassAll = 0x7fff;

    internal void SetDatabaseReplicaId(nint db, string replicaId)
    {
        EnsureInitialized();
        var id = ParseReplicaId(replicaId);
        const int replicaInfoSize = 32;
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(replicaInfoSize);
        try
        {
            Zero(buffer, replicaInfoSize);
            Check(Resolve<NSFDbReplicaInfoGetDelegate>("NSFDbReplicaInfoGet")(db, buffer), "NSFDbReplicaInfoGet");

            // DBREPLICAINFO starts with TIMEDATE ID. Preserve flags, cutoff interval,
            // cutoff date, and any trailing ABI fields returned by the installed runtime.
            System.Runtime.InteropServices.Marshal.WriteInt32(buffer, 0, unchecked((int)id.Innards0));
            System.Runtime.InteropServices.Marshal.WriteInt32(buffer, 4, unchecked((int)id.Innards1));
            Check(Resolve<NSFDbReplicaInfoSetDelegate>("NSFDbReplicaInfoSet")(db, buffer), "NSFDbReplicaInfoSet");
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
    }

    internal uint CreateDatabaseCopy(string sourceServer, string sourceFile, string destinationServer, string destinationFile)
    {
        EnsureInitialized();
        using var sourceFileText = ToLmbcs(sourceFile);
        using var sourceServerText = ToLmbcs(sourceServer);
        using var destinationFileText = ToLmbcs(destinationFile);
        using var destinationServerText = ToLmbcs(destinationServer);

        nint sourceNetworkPath = 0;
        nint destinationNetworkPath = 0;
        try
        {
            var sourcePath = sourceFileText.Pointer;
            if (sourceServer.Length > 0)
            {
                sourceNetworkPath = System.Runtime.InteropServices.Marshal.AllocHGlobal(4096);
                Zero(sourceNetworkPath, 4096);
                Check(Resolve<OSPathNetConstructDelegate>("OSPathNetConstruct")(
                    0, sourceServerText.Pointer, sourceFileText.Pointer, sourceNetworkPath), "OSPathNetConstruct(source)");
                sourcePath = sourceNetworkPath;
            }

            var destinationPath = destinationFileText.Pointer;
            if (destinationServer.Length > 0)
            {
                destinationNetworkPath = System.Runtime.InteropServices.Marshal.AllocHGlobal(4096);
                Zero(destinationNetworkPath, 4096);
                Check(Resolve<OSPathNetConstructDelegate>("OSPathNetConstruct")(
                    0, destinationServerText.Pointer, destinationFileText.Pointer, destinationNetworkPath), "OSPathNetConstruct(destination)");
                destinationPath = destinationNetworkPath;
            }

            Check(Resolve<NSFDbCreateAndCopyDelegate>("NSFDbCreateAndCopy")(
                sourcePath, destinationPath, NoteClassAll, 0, 0, out var newDb), "NSFDbCreateAndCopy");
            return newDb;
        }
        finally
        {
            if (destinationNetworkPath != 0) System.Runtime.InteropServices.Marshal.FreeHGlobal(destinationNetworkPath);
            if (sourceNetworkPath != 0) System.Runtime.InteropServices.Marshal.FreeHGlobal(sourceNetworkPath);
        }
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFDbReplicaInfoSetDelegate(nint db, nint replicaInfo);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFDbCreateAndCopyDelegate(
        nint sourceDb,
        nint destinationDb,
        ushort noteClass,
        ushort limit,
        uint flags,
        out uint returnHandle);
}
""";
}
