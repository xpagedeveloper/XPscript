namespace XPScript.Compiler;

internal static class NotesNativeApiAddressBookSource
{
    public const string Code = """
internal sealed partial class XPScriptNotesNativeApi
{
    internal (string Server, string FilePath)[] GetAddressBooks()
    {
        EnsureInitialized();
        nint buffer = 0;
        Check(Resolve<NAMEGetAddressBooksDelegate>("NAMEGetAddressBooks")(0, 0, out var count, out var length, out buffer), "NAMEGetAddressBooks");
        if (count == 0 || buffer == 0 || length == 0)
        {
            if (buffer != 0) Resolve<AddressBookMemFreeDelegate>("OSMemFree")(buffer);
            return Array.Empty<(string Server, string FilePath)>();
        }

        try
        {
            var pointer = Resolve<AddressBookLockObjectDelegate>("OSLockObject")(buffer);
            if (pointer == 0) throw new XPScriptRuntimeException(5, "Unable to lock Notes address book list.");
            try
            {
                var result = new List<(string Server, string FilePath)>(count);
                var offset = 0;
                for (var i = 0; i < count && offset < length; i++)
                {
                    var remaining = length - offset;
                    var path = FromLmbcsZeroTerminated(pointer + offset, remaining);
                    offset += Math.Min(remaining, EncodingLength(pointer + offset, remaining) + 1);
                    if (path.Length == 0) continue;
                    result.Add(ParseNetworkPath(path));
                }
                return result.ToArray();
            }
            finally { Resolve<AddressBookUnlockObjectDelegate>("OSUnlockObject")(buffer); }
        }
        finally { Resolve<AddressBookMemFreeDelegate>("OSMemFree")(buffer); }
    }

    private (string Server, string FilePath) ParseNetworkPath(string path)
    {
        using var pathText = ToLmbcs(path);
        const int capacity = 4096;
        var server = System.Runtime.InteropServices.Marshal.AllocHGlobal(capacity);
        var file = System.Runtime.InteropServices.Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(server, capacity);
            Zero(file, capacity);
            Check(Resolve<OSPathNetParseAddressBookDelegate>("OSPathNetParse")(pathText.Pointer, 0, server, file), "OSPathNetParse(address book)");
            return (FromLmbcsZeroTerminated(server, capacity - 1), FromLmbcsZeroTerminated(file, capacity - 1));
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(server);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(file);
        }
    }

    private static int EncodingLength(nint pointer, int maximum)
    {
        var length = 0;
        while (length < maximum && System.Runtime.InteropServices.Marshal.ReadByte(pointer, length) != 0) length++;
        return length;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort NAMEGetAddressBooksDelegate(nint serverName, ushort options, out ushort returnCount, out ushort returnLength, out nint returnHandle);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate nint AddressBookLockObjectDelegate(nint handle);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate void AddressBookUnlockObjectDelegate(nint handle);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort AddressBookMemFreeDelegate(nint handle);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort OSPathNetParseAddressBookDelegate(nint pathName, nint portName, nint serverName, nint fileName);
}
""";
}
