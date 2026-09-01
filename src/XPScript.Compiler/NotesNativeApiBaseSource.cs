namespace XPScript.Compiler;

internal static class NotesNativeApiBaseSource
{
    public const string Code = """
internal sealed partial class XPScriptNotesNativeApi : IDisposable
{
    private const ushort TranslateLmbcsToUtf8 = 22;
    private const ushort TranslateUtf8ToLmbcs = 24;
    private readonly nint _library;
    private readonly string _runtimeDirectory;
    private readonly string _applicationDirectory;
    private bool _initialized;
    private bool _disposed;

    internal XPScriptNotesNativeApi(string runtimeDirectory)
    {
        _runtimeDirectory = Path.GetFullPath(runtimeDirectory);
        _applicationDirectory = Environment.CurrentDirectory;
        if (!Directory.Exists(_runtimeDirectory))
            throw new XPScriptRuntimeException(76, "Notes/Domino runtime directory does not exist: " + _runtimeDirectory);

        var candidates = OperatingSystem.IsWindows()
            ? new[] { "nnotes.dll" }
            : OperatingSystem.IsMacOS()
                ? new[] { "libnotes.dylib", "libnotes64.dylib" }
                : new[] { "libnotes.so", "libnotes64.so" };

        string? path = null;
        foreach (var candidate in candidates)
        {
            var current = Path.Combine(_runtimeDirectory, candidate);
            if (File.Exists(current)) { path = current; break; }
        }
        if (path is null)
            throw new XPScriptRuntimeException(53, "No Notes/Domino C API library was found in " + _runtimeDirectory + ".");

        if (OperatingSystem.IsWindows())
        {
            SetDllDirectory(_runtimeDirectory);
            SetCurrentDirectory(_runtimeDirectory);
        }

        try { _library = System.Runtime.InteropServices.NativeLibrary.Load(path); }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException)
        {
            throw new XPScriptRuntimeException(53, "Unable to load Notes/Domino C API library: " + path);
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string? path);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool SetCurrentDirectory(string path);

    internal void Initialize(string? notesIni)
    {
        EnsureNotDisposed();
        if (_initialized) return;

        var args = new List<string> { Path.Combine(_runtimeDirectory, "xpscript-notes") };
        if (!string.IsNullOrWhiteSpace(notesIni)) args.Add("=" + notesIni);
        using var argv = BuildArgv(args);
        Check(Resolve<NotesInitExtendedDelegate>("NotesInitExtended")(args.Count, argv.Pointer), "NotesInitExtended");
        _initialized = true;
    }

    internal void Terminate()
    {
        if (!_initialized || _disposed) return;
        Resolve<NotesTermDelegate>("NotesTerm")();
        _initialized = false;
    }

    internal string GetUserName()
    {
        EnsureInitialized();
        const int capacity = 2048;
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(buffer, capacity);
            Check(Resolve<SECKFMGetUserNameDelegate>("SECKFMGetUserName")(buffer), "SECKFMGetUserName");
            return FromLmbcsZeroTerminated(buffer, capacity - 1);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
    }

    internal nint OpenDatabase(string server, string file)
    {
        EnsureInitialized();
        using var fileText = ToLmbcs(file);
        using var serverText = ToLmbcs(server);
        nint networkPath = 0;
        try
        {
            // HCL documents a NULL pathname as the local Notes/Domino data directory.
            // For a remote server, build a network pathname to that server's data directory.
            var path = file.Length == 0 && server.Length == 0 ? 0 : fileText.Pointer;
            if (server.Length > 0)
            {
                networkPath = System.Runtime.InteropServices.Marshal.AllocHGlobal(4096);
                Zero(networkPath, 4096);
                Check(Resolve<OSPathNetConstructDelegate>("OSPathNetConstruct")(0, serverText.Pointer, fileText.Pointer, networkPath), "OSPathNetConstruct");
                path = networkPath;
            }
            Check(Resolve<NSFDbOpenDelegate>("NSFDbOpen")(path, out var db), "NSFDbOpen");
            return db;
        }
        finally
        {
            if (networkPath != 0) System.Runtime.InteropServices.Marshal.FreeHGlobal(networkPath);
        }
    }

    internal void CloseDatabase(nint db)
    {
        if (db != 0) Check(Resolve<NSFDbCloseDelegate>("NSFDbClose")(db), "NSFDbClose");
    }

    internal string CanonicalizeName(string value)
    {
        EnsureInitialized();
        using var input = ToLmbcs(value);
        var output = System.Runtime.InteropServices.Marshal.AllocHGlobal(4096);
        try
        {
            Zero(output, 4096);
            Check(Resolve<DNCanonicalizeDelegate>("DNCanonicalize")(0, 0, input.Pointer, output, 4095, out var length), "DNCanonicalize");
            return FromLmbcs(output, length);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(output); }
    }

    internal string AbbreviateName(string value)
    {
        EnsureInitialized();
        using var input = ToLmbcs(value);
        var output = System.Runtime.InteropServices.Marshal.AllocHGlobal(4096);
        try
        {
            Zero(output, 4096);
            Check(Resolve<DNAbbreviateDelegate>("DNAbbreviate")(0, 0, input.Pointer, output, 4095, out var length), "DNAbbreviate");
            return FromLmbcs(output, length);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(output); }
    }

    internal XPScriptNotesTimeDate ParseTimeDate(string value)
    {
        EnsureInitialized();
        using var text = ToLmbcs(value);
        var current = text.Pointer;
        Check(Resolve<ConvertTextToTimeDateDelegate>("ConvertTextToTIMEDATE")(0, 0, ref current, checked((ushort)Math.Min(text.Length, ushort.MaxValue)), out var result), "ConvertTextToTIMEDATE");
        return result;
    }

    internal XPScriptNotesTimeDate CurrentTimeDate()
    {
        EnsureInitialized();
        Resolve<OSCurrentTimeDateDelegate>("OSCurrentTIMEDATE")(out var value);
        return value;
    }

    internal string FormatTimeDate(XPScriptNotesTimeDate value)
    {
        EnsureInitialized();
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(1024);
        try
        {
            Zero(buffer, 1024);
            Check(Resolve<ConvertTimeDateToTextDelegate>("ConvertTIMEDATEToText")(0, 0, ref value, buffer, 1023, out var length), "ConvertTIMEDATEToText");
            return FromLmbcs(buffer, length);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
    }

    internal void AdjustTimeDate(ref XPScriptNotesTimeDate value, int seconds, int minutes, int hours, int days, int months, int years)
    {
        EnsureInitialized();
        if (Resolve<TimeDateAdjustDelegate>("TimeDateAdjust")(ref value, seconds, minutes, hours, days, months, years) != 0)
            throw new XPScriptRuntimeException(5, "TimeDateAdjust failed.");
    }

    internal LmbcsBuffer ToLmbcs(string value)
    {
        EnsureInitialized();
        var bytes = System.Text.Encoding.UTF8.GetBytes(value ?? "");
        var input = System.Runtime.InteropServices.Marshal.AllocHGlobal(Math.Max(1, bytes.Length));
        var capacity = Math.Max(16, checked(bytes.Length * 4 + 8));
        var output = System.Runtime.InteropServices.Marshal.AllocHGlobal(capacity);
        try
        {
            if (bytes.Length > 0) System.Runtime.InteropServices.Marshal.Copy(bytes, 0, input, bytes.Length);
            Zero(output, capacity);
            var length = Resolve<OSTranslate32Delegate>("OSTranslate32")(TranslateUtf8ToLmbcs, input, checked((uint)bytes.Length), checked((uint)(capacity - 1)), output);
            if (length >= capacity) throw new XPScriptRuntimeException(5, "LMBCS conversion exceeded the output buffer.");
            System.Runtime.InteropServices.Marshal.WriteByte(output, checked((int)length), 0);
            return new LmbcsBuffer(output, checked((int)length));
        }
        catch
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(output);
            throw;
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(input); }
    }

    internal string FromLmbcs(nint input, int length)
    {
        if (input == 0 || length <= 0) return "";
        EnsureInitialized();
        var capacity = Math.Max(16, checked(length * 4 + 8));
        var output = System.Runtime.InteropServices.Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(output, capacity);
            var count = Resolve<OSTranslate32Delegate>("OSTranslate32")(TranslateLmbcsToUtf8, input, checked((uint)length), checked((uint)(capacity - 1)), output);
            var bytes = new byte[count];
            if (count > 0) System.Runtime.InteropServices.Marshal.Copy(output, bytes, 0, checked((int)count));
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(output); }
    }

    internal string FromLmbcsZeroTerminated(nint input, int maximum)
    {
        var length = 0;
        while (length < maximum && System.Runtime.InteropServices.Marshal.ReadByte(input, length) != 0) length++;
        return FromLmbcs(input, length);
    }

    internal void Check(ushort status, string operation)
    {
        if (status == 0) return;
        var message = _initialized ? LoadStatusText(status) : "";
        throw new XPScriptRuntimeException(5, operation + " failed with Notes status 0x" + status.ToString("X4", System.Globalization.CultureInfo.InvariantCulture) + (message.Length == 0 ? "." : ": " + message));
    }

    private string LoadStatusText(ushort status)
    {
        if (!TryResolve<OSLoadStringDelegate>("OSLoadString", out var load) || load is null) return "";
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(2048);
        try
        {
            Zero(buffer, 2048);
            var length = load(0, (ushort)(status & 0x3fff), buffer, 2047);
            return length == 0 ? "" : FromLmbcs(buffer, length);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
    }

    internal T Resolve<T>(string name) where T : Delegate
    {
        EnsureNotDisposed();
        try
        {
            return System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<T>(System.Runtime.InteropServices.NativeLibrary.GetExport(_library, name));
        }
        catch (EntryPointNotFoundException)
        {
            throw new XPScriptRuntimeException(453, "Notes/Domino C API entry point is unavailable: " + name);
        }
    }

    internal bool TryResolve<T>(string name, out T? value) where T : Delegate
    {
        EnsureNotDisposed();
        if (!System.Runtime.InteropServices.NativeLibrary.TryGetExport(_library, name, out var address))
        {
            value = null;
            return false;
        }
        value = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<T>(address);
        return true;
    }

    internal void EnsureInitialized()
    {
        EnsureNotDisposed();
        if (!_initialized) throw new XPScriptRuntimeException(91, "Notes C API runtime is not initialized.");
    }

    private void EnsureNotDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(XPScriptNotesNativeApi));
    }

    internal static void Zero(nint pointer, int length)
    {
        for (var i = 0; i < length; i++) System.Runtime.InteropServices.Marshal.WriteByte(pointer, i, 0);
    }

    private ArgvBuffer BuildArgv(IReadOnlyList<string> args)
    {
        var values = new List<nint>();
        var argv = System.Runtime.InteropServices.Marshal.AllocHGlobal(IntPtr.Size * args.Count);
        try
        {
            for (var i = 0; i < args.Count; i++)
            {
                var value = System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(args[i] ?? "");
                values.Add(value);
                System.Runtime.InteropServices.Marshal.WriteIntPtr(argv, i * IntPtr.Size, value);
            }
            return new ArgvBuffer(argv, values);
        }
        catch
        {
            foreach (var value in values)
                if (value != 0) System.Runtime.InteropServices.Marshal.FreeHGlobal(value);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(argv);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_initialized) Terminate();
        _disposed = true;
        if (_library != 0) System.Runtime.InteropServices.NativeLibrary.Free(_library);
    }

    internal sealed class LmbcsBuffer : IDisposable
    {
        internal LmbcsBuffer(nint pointer, int length) { Pointer = pointer; Length = length; }
        internal nint Pointer { get; private set; }
        internal int Length { get; }
        public void Dispose()
        {
            var pointer = Pointer;
            Pointer = 0;
            if (pointer != 0) System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer);
        }
    }

    private sealed class ArgvBuffer : IDisposable
    {
        private readonly List<nint> _values;
        internal ArgvBuffer(nint pointer, List<nint> values) { Pointer = pointer; _values = values; }
        internal nint Pointer { get; private set; }
        public void Dispose()
        {
            foreach (var value in _values)
                if (value != 0) System.Runtime.InteropServices.Marshal.FreeHGlobal(value);
            if (Pointer != 0) System.Runtime.InteropServices.Marshal.FreeHGlobal(Pointer);
            Pointer = 0;
        }
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NotesInitExtendedDelegate(int argc, nint argv);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void NotesTermDelegate();
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate uint OSTranslate32Delegate(ushort mode, nint input, uint inputLength, uint outputSize, nint output);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort OSLoadStringDelegate(nint module, ushort code, nint output, ushort outputLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort SECKFMGetUserNameDelegate(nint output);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort OSPathNetConstructDelegate(nint portName, nint serverName, nint fileName, nint pathName);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFDbOpenDelegate(nint pathName, out nint db);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFDbCloseDelegate(nint db);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DNCanonicalizeDelegate(uint flags, nint templateName, nint input, nint output, ushort outputSize, out ushort outputLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DNAbbreviateDelegate(uint flags, nint templateName, nint input, nint output, ushort outputSize, out ushort outputLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort ConvertTextToTimeDateDelegate(nint intlFormat, nint textFormat, ref nint text, ushort maxLength, out XPScriptNotesTimeDate output);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort ConvertTimeDateToTextDelegate(nint intlFormat, nint textFormat, ref XPScriptNotesTimeDate value, nint output, ushort outputLength, out ushort textLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void OSCurrentTimeDateDelegate(out XPScriptNotesTimeDate value);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate int TimeDateAdjustDelegate(ref XPScriptNotesTimeDate value, int seconds, int minutes, int hours, int days, int months, int years);
}
""";
}
