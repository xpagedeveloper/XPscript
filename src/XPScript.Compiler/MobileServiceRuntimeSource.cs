namespace XPScript.Compiler;

internal static class MobileServiceRuntimeSource
{
    public const string Code = """
public interface IXPScriptMobileService
{
    string Capability { get; }
    bool IsAvailable { get; }
    string UnavailableReason { get; }
    string Permission { get; }
    XPScriptMobilePermissionState PermissionState { get; }
}

public enum XPScriptMobilePermissionState
{
    NotRequired,
    Unknown,
    Granted,
    Denied
}

public interface IXPScriptCameraService : IXPScriptMobileService
{
    string CapturePhoto(string outputPath);
}

public static class XPScriptCamera
{
    public static string CapturePhoto(object? outputPath)
    {
        var path = XPScriptRuntime.CStr(outputPath);
        if (!XPScriptMobileServices.TryGet("camera", out var service) || service is not IXPScriptCameraService camera)
            throw new XPScriptRuntimeException(5, "camera: " + XPScriptMobileServices.UnavailableReason("camera"));
        if (!camera.IsAvailable) throw new XPScriptRuntimeException(5, "camera: " + camera.UnavailableReason);
        return camera.CapturePhoto(path);
    }
}

public static class XPScriptMobileServices
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, IXPScriptMobileService> Services = new(StringComparer.OrdinalIgnoreCase);

    public static void Register(IXPScriptMobileService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (string.IsNullOrWhiteSpace(service.Capability)) throw new ArgumentException("Mobile service capability is required.", nameof(service));
        Services[service.Capability] = service;
    }

    public static bool TryGet(string capability, out IXPScriptMobileService? service)
        => Services.TryGetValue(capability, out service);

    public static bool IsAvailable(object? capability)
        => Services.TryGetValue(XPScriptRuntime.CStr(capability), out var service) && service.IsAvailable;

    public static string UnavailableReason(object? capability)
        => Services.TryGetValue(XPScriptRuntime.CStr(capability), out var service) ? service.UnavailableReason : "Mobile capability is not registered.";

    public static void RequireAvailable(object? capability)
    {
        var name = XPScriptRuntime.CStr(capability);
        if (IsAvailable(name)) return;
        throw new XPScriptRuntimeException(5, name + ": " + UnavailableReason(name));
    }
}
""";
}
