namespace XPScript.Compiler;

internal static class XpsWindowsServiceHostPostProcessor
{
    public static string Transform(string generated, XpsServiceDefinition definition)
    {
        if (!definition.IsService) return generated;

        const string runtimeCall = "XPScriptServiceRuntime.Run(typeof(Script));";
        const string hostCall = "XPScriptServiceHostRuntime.Run(typeof(Script));";
        if (!generated.Contains(runtimeCall, StringComparison.Ordinal))
            throw new CompilerException("Unable to wire service host entry point.");
        generated = generated.Replace(runtimeCall, hostCall, StringComparison.Ordinal);

        const string stoppingField = "    private static int _stopping;";
        if (!generated.Contains(stoppingField, StringComparison.Ordinal))
            throw new CompilerException("Unable to expose service stop state to the native host.");
        generated = generated.Replace(
            stoppingField,
            stoppingField + "\n    private static CancellationTokenSource? CurrentCancellation;",
            StringComparison.Ordinal);

        const string createCancellation = "        using var schedulerCancellation = new CancellationTokenSource();";
        if (!generated.Contains(createCancellation, StringComparison.Ordinal))
            throw new CompilerException("Unable to wire native service cancellation.");
        generated = generated.Replace(
            createCancellation,
            createCancellation + "\n        CurrentCancellation = schedulerCancellation;",
            StringComparison.Ordinal);

        const string requestStop = "    private static void RequestStop(CancellationTokenSource cancellation)";
        if (!generated.Contains(requestStop, StringComparison.Ordinal))
            throw new CompilerException("Unable to expose native service stop request.");
        generated = generated.Replace(
            requestStop,
            "    internal static void RequestStopFromHost()\n    {\n        var cancellation = CurrentCancellation;\n        if (cancellation is not null) RequestStop(cancellation);\n    }\n\n" + requestStop,
            StringComparison.Ordinal);

        return generated + "\n\n" + BuildHostSource(definition.StopTimeout) + "\n";
    }

    private static string BuildHostSource(TimeSpan stopTimeout)
    {
        var waitHint = (uint)Math.Clamp((long)Math.Ceiling(stopTimeout.TotalMilliseconds), 1000L, uint.MaxValue);
        return $$"""
internal static class XPScriptServiceHostRuntime
{
    public static void Run(Type scriptType)
    {
        if (!OperatingSystem.IsWindows())
        {
            XPScriptServiceRuntime.Run(scriptType);
            return;
        }

        XPScriptWindowsServiceControlManager.Run(scriptType);
    }
}

internal static class XPScriptWindowsServiceControlManager
{
    private const uint ServiceWin32OwnProcess = 0x00000010;
    private const uint ServiceStopped = 0x00000001;
    private const uint ServiceStartPending = 0x00000002;
    private const uint ServiceStopPending = 0x00000003;
    private const uint ServiceRunning = 0x00000004;
    private const uint ServiceAcceptStop = 0x00000001;
    private const uint ServiceAcceptShutdown = 0x00000004;
    private const uint ServiceControlStop = 0x00000001;
    private const uint ServiceControlInterrogate = 0x00000004;
    private const uint ServiceControlShutdown = 0x00000005;
    private const int ErrorFailedServiceControllerConnect = 1063;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct ServiceTableEntry
    {
        public string? ServiceName;
        public ServiceMainDelegate? ServiceProc;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct ServiceStatus
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate void ServiceMainDelegate(uint argc, IntPtr argv);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate uint ServiceControlHandlerDelegate(uint control, uint eventType, IntPtr eventData, IntPtr context);

    [System.Runtime.InteropServices.DllImport("advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool StartServiceCtrlDispatcherW([System.Runtime.InteropServices.In] ServiceTableEntry[] serviceTable);

    [System.Runtime.InteropServices.DllImport("advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr RegisterServiceCtrlHandlerExW(string serviceName, ServiceControlHandlerDelegate handlerProc, IntPtr context);

    [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetServiceStatus(IntPtr serviceStatusHandle, ref ServiceStatus serviceStatus);

    private static readonly ServiceMainDelegate ServiceMainCallback = ServiceMain;
    private static readonly ServiceControlHandlerDelegate HandlerCallback = Handler;
    private static Type? ScriptType;
    private static IntPtr StatusHandle;
    private static ServiceStatus Status;

    public static void Run(Type scriptType)
    {
        ScriptType = scriptType;
        var table = new[]
        {
            new ServiceTableEntry { ServiceName = string.Empty, ServiceProc = ServiceMainCallback },
            new ServiceTableEntry { ServiceName = null, ServiceProc = null }
        };

        if (StartServiceCtrlDispatcherW(table)) return;

        var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        if (error == ErrorFailedServiceControllerConnect)
            throw new InvalidOperationException("A compiled [Service] executable must be started by the Windows Service Control Manager.");
        throw new System.ComponentModel.Win32Exception(error, "Unable to connect the XPScript service to the Windows Service Control Manager.");
    }

    private static void ServiceMain(uint argc, IntPtr argv)
    {
        var serviceName = ReadServiceName(argc, argv);
        StatusHandle = RegisterServiceCtrlHandlerExW(serviceName, HandlerCallback, IntPtr.Zero);
        if (StatusHandle == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(), "Unable to register the XPScript service control handler.");

        Status = new ServiceStatus
        {
            ServiceType = ServiceWin32OwnProcess,
            CurrentState = ServiceStartPending,
            ControlsAccepted = 0,
            Win32ExitCode = 0,
            CheckPoint = 1,
            WaitHint = {{waitHint}}U
        };
        ReportStatus();

        Status.CurrentState = ServiceRunning;
        Status.ControlsAccepted = ServiceAcceptStop | ServiceAcceptShutdown;
        Status.CheckPoint = 0;
        Status.WaitHint = 0;
        ReportStatus();

        try
        {
            XPScriptServiceRuntime.Run(ScriptType ?? throw new InvalidOperationException("Service script type is unavailable."));
        }
        finally
        {
            Status.CurrentState = ServiceStopped;
            Status.ControlsAccepted = 0;
            Status.CheckPoint = 0;
            Status.WaitHint = 0;
            Status.Win32ExitCode = Environment.ExitCode == 0 ? 0U : unchecked((uint)Environment.ExitCode);
            ReportStatus();
        }
    }

    private static uint Handler(uint control, uint eventType, IntPtr eventData, IntPtr context)
    {
        if (control is ServiceControlStop or ServiceControlShutdown)
        {
            if (Status.CurrentState is ServiceStopped or ServiceStopPending) return 0;
            Status.CurrentState = ServiceStopPending;
            Status.ControlsAccepted = 0;
            Status.CheckPoint = 1;
            Status.WaitHint = {{waitHint}}U;
            ReportStatus();
            XPScriptServiceRuntime.RequestStopFromHost();
            return 0;
        }

        if (control == ServiceControlInterrogate) ReportStatus();
        return 0;
    }

    private static string ReadServiceName(uint argc, IntPtr argv)
    {
        if (argc == 0 || argv == IntPtr.Zero) return string.Empty;
        var first = System.Runtime.InteropServices.Marshal.ReadIntPtr(argv);
        return first == IntPtr.Zero ? string.Empty : System.Runtime.InteropServices.Marshal.PtrToStringUni(first) ?? string.Empty;
    }

    private static void ReportStatus()
    {
        if (StatusHandle != IntPtr.Zero) SetServiceStatus(StatusHandle, ref Status);
    }
}
""";
    }
}
