using System.Runtime.InteropServices;

if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
{
    Console.WriteLine("Native interop smoke test is intended for Linux and macOS.");
    return 0;
}

var libraryName = OperatingSystem.IsMacOS()
    ? "/usr/lib/libSystem.B.dylib"
    : "libc.so.6";

const string exportName = "getpid";

for (var iteration = 0; iteration < 500; iteration++)
{
    nint memory = Marshal.AllocHGlobal(4096);
    try
    {
        for (var i = 0; i < 4096; i++)
            Marshal.WriteByte(memory, i, unchecked((byte)(i + iteration)));

        for (var i = 0; i < 4096; i++)
        {
            var expected = unchecked((byte)(i + iteration));
            var actual = Marshal.ReadByte(memory, i);
            if (actual != expected)
                throw new InvalidOperationException($"Unmanaged memory mismatch at iteration {iteration}, offset {i}.");
        }
    }
    finally
    {
        Marshal.FreeHGlobal(memory);
    }

    nint library = NativeLibrary.Load(libraryName);
    try
    {
        if (!NativeLibrary.TryGetExport(library, exportName, out var address) || address == 0)
            throw new InvalidOperationException($"Unable to resolve {exportName} from {libraryName}.");

        var getPid = Marshal.GetDelegateForFunctionPointer<GetPidDelegate>(address);
        if (getPid() <= 0)
            throw new InvalidOperationException("Native getpid returned an invalid process id.");
    }
    finally
    {
        NativeLibrary.Free(library);
    }
}

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

Console.WriteLine($"Native interop smoke test passed on {RuntimeInformation.OSDescription} ({RuntimeInformation.ProcessArchitecture}).");
return 0;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate int GetPidDelegate();
