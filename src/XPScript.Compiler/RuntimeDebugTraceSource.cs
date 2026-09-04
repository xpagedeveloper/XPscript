namespace XPScript.Compiler;

internal static class RuntimeDebugTraceSource
{
    public const string Code = """
internal static class XPScriptRuntimeDebugTrace
{
    public static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable("XPSCRIPT_RUNTIME_DEBUG"), "1", StringComparison.Ordinal);

    public static bool InfoEnabled =>
        string.Equals(Environment.GetEnvironmentVariable("XPSCRIPT_RUNTIME_INFO"), "1", StringComparison.Ordinal);

    public static bool DetailedEnabled => Enabled && InfoEnabled;

    public static void WriteLine(string message)
    {
        if (!DetailedEnabled) return;
        Console.Error.WriteLine(message);
    }

    public static IDisposable SuppressNativeStandardErrorUnlessDetailed() =>
        DetailedEnabled ? NoopDisposable.Instance : NativeStandardErrorScope.TryCreate();

    public static void TraceHandled(Exception original, Exception normalized, int sourceLine)
    {
        if (!Enabled) return;
        if (IsExpectedComputeWithFormValidation(normalized)) return;

        Console.Error.WriteLine(sourceLine > 0
            ? "DEBUG runtime exception trapped at XPScript line " + sourceLine.ToString(System.Globalization.CultureInfo.InvariantCulture) + " (handled by On Error):"
            : "DEBUG runtime exception trapped (handled by On Error):");
        Console.Error.WriteLine(normalized.ToString());

        if (!ReferenceEquals(original, normalized))
        {
            Console.Error.WriteLine("DEBUG underlying managed exception:");
            Console.Error.WriteLine(original.ToString());
        }
    }

    private static bool IsExpectedComputeWithFormValidation(Exception exception) =>
        exception is XPScriptRuntimeException
        {
            Number: 5,
            Message: "ComputeWithForm validation failed."
        };

    private sealed class NoopDisposable : IDisposable
    {
        internal static readonly NoopDisposable Instance = new();
        public void Dispose() { }
    }

    private sealed class NativeStandardErrorScope : IDisposable
    {
        private const int StdErrorHandle = -12;
        private nint _savedHandle;
        private nint _nullHandle;
        private int _savedFd = -1;
        private int _nullFd = -1;
        private bool _active;

        internal static IDisposable TryCreate()
        {
            var scope = new NativeStandardErrorScope();
            try { scope.Activate(); }
            catch { scope.Dispose(); }
            return scope;
        }

        private void Activate()
        {
            if (OperatingSystem.IsWindows())
            {
                _savedHandle = GetStdHandle(StdErrorHandle);
                _nullHandle = CreateFileW("NUL", 0x40000000, 0x00000003, 0, 3, 0, 0);
                if (_savedHandle == 0 || _savedHandle == new nint(-1) || _nullHandle == 0 || _nullHandle == new nint(-1)) return;
                if (!SetStdHandle(StdErrorHandle, _nullHandle)) return;
                _active = true;
                return;
            }

            _savedFd = dup(2);
            if (_savedFd < 0) return;
            _nullFd = open("/dev/null", 1);
            if (_nullFd < 0) return;
            if (dup2(_nullFd, 2) < 0) return;
            _active = true;
        }

        public void Dispose()
        {
            try
            {
                if (_active)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        if (_savedHandle != 0 && _savedHandle != new nint(-1)) SetStdHandle(StdErrorHandle, _savedHandle);
                    }
                    else if (_savedFd >= 0)
                    {
                        dup2(_savedFd, 2);
                    }
                }
            }
            catch { }
            finally
            {
                if (OperatingSystem.IsWindows())
                {
                    if (_nullHandle != 0 && _nullHandle != new nint(-1)) CloseHandle(_nullHandle);
                }
                else
                {
                    if (_nullFd >= 0) close(_nullFd);
                    if (_savedFd >= 0) close(_savedFd);
                }
                _active = false;
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern nint GetStdHandle(int nStdHandle);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetStdHandle(int nStdHandle, nint handle);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern nint CreateFileW(string fileName, uint desiredAccess, uint shareMode, nint securityAttributes, uint creationDisposition, uint flagsAndAttributes, nint templateFile);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(nint handle);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        private static extern int dup(int oldfd);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        private static extern int dup2(int oldfd, int newfd);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        private static extern int open(string pathname, int flags);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        private static extern int close(int fd);
    }
}
""";
}
