namespace XPScript.Compiler;

internal static class SystemInventoryRuntimePostProcessor
{
    private const string Marker = "XPScriptSystemInventoryFactory.Create()";

    public static string Transform(string generated, string runtimeIdentifier)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (!generated.Contains(Marker, StringComparison.Ordinal)) return generated;

        if (runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase))
            throw new CompilerException(
                "SystemInventory cannot execute in browser client code. Use SystemInventory inside a [ServerSide] procedure. " +
                "The inventory will describe the server host, not the client device.");

        if (generated.Contains("internal sealed class XPScriptSystemInventory", StringComparison.Ordinal)) return generated;
        generated += Environment.NewLine + Environment.NewLine + SystemInventoryRuntimeSource.Code + Environment.NewLine;
        generated = generated.Replace(
            """
            unsafe
            {
                if (XPScriptSystemInventoryNative.sysctlbyname(name, (IntPtr)(&value), ref length, IntPtr.Zero, 0) == 0) return value;
            }
""",
            """
            var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(long));
            try
            {
                if (XPScriptSystemInventoryNative.sysctlbyname(name, pointer, ref length, IntPtr.Zero, 0) == 0)
                    return unchecked((ulong)System.Runtime.InteropServices.Marshal.ReadInt64(pointer));
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
""",
            StringComparison.Ordinal);
        return generated;
    }
}
