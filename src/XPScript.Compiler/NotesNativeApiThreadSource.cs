namespace XPScript.Compiler;

internal static class NotesNativeApiThreadSource
{
    public const string Code = """
internal sealed partial class XPScriptNotesNativeApi
{
    [ThreadStatic]
    private static int NotesThreadScopeDepth;

    private int _processInitializationManagedThreadId;

    internal void MarkProcessInitializationThread()
    {
        _processInitializationManagedThreadId = Environment.CurrentManagedThreadId;
    }

    internal NotesThreadScope EnterNotesThread()
    {
        EnsureNotDisposed();
        if (!_initialized)
            throw new XPScriptRuntimeException(91, "Notes C API runtime is not initialized.");

        // NotesInitExtended initializes the thread that performed process initialization.
        if (Environment.CurrentManagedThreadId == _processInitializationManagedThreadId)
            return default;

        if (NotesThreadScopeDepth == 0)
        {
            var status = ResolveRaw<NotesInitThreadDelegate>("NotesInitThread")();
            if (status != 0)
                throw new XPScriptRuntimeException(5,
                    "NotesInitThread failed with Notes status 0x" +
                    status.ToString("X4", System.Globalization.CultureInfo.InvariantCulture) + ".");
        }

        NotesThreadScopeDepth++;
        return new NotesThreadScope(this, ownsInitialization: true);
    }

    private void ExitNotesThread()
    {
        if (NotesThreadScopeDepth <= 0) return;
        NotesThreadScopeDepth--;
        if (NotesThreadScopeDepth == 0)
            ResolveRaw<NotesTermThreadDelegate>("NotesTermThread")();
    }

    private T ResolveRaw<T>(string name) where T : Delegate
    {
        EnsureNotDisposed();
        try
        {
            return System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<T>(
                System.Runtime.InteropServices.NativeLibrary.GetExport(_library, name));
        }
        catch (EntryPointNotFoundException)
        {
            throw new XPScriptRuntimeException(453, "Notes/Domino C API entry point is unavailable: " + name);
        }
    }

    internal readonly struct NotesThreadScope : IDisposable
    {
        private readonly XPScriptNotesNativeApi? _owner;
        private readonly bool _ownsInitialization;

        internal NotesThreadScope(XPScriptNotesNativeApi owner, bool ownsInitialization)
        {
            _owner = owner;
            _ownsInitialization = ownsInitialization;
        }

        public void Dispose()
        {
            if (_ownsInitialization)
                _owner?.ExitNotesThread();
        }
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort NotesInitThreadDelegate();

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate void NotesTermThreadDelegate();
}
""";
}
