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

public readonly record struct XPScriptLocationData(double Latitude, double Longitude, double? AccuracyMeters, double? AltitudeMeters, double? SpeedMetersPerSecond, DateTimeOffset Timestamp);

public sealed class XPScriptLocationUnavailableException : Exception
{
    public XPScriptLocationUnavailableException(string message) : base(message) { }
}

public interface IXPScriptLocationService : IXPScriptMobileService
{
    XPScriptLocationData GetCurrent(TimeSpan timeout);
}

public interface IXPScriptContinuousLocationService : IXPScriptLocationService
{
    void StartUpdates(TimeSpan minimumInterval, Action<XPScriptLocationData> onLocation);
    void StopUpdates();
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

public static class XPScriptLocation
{
    public static XPScriptLocationData GetCurrent(object? timeoutMilliseconds)
    {
        var timeout = TimeSpan.FromMilliseconds(XPScriptRuntime.CDbl(timeoutMilliseconds));
        if (timeout <= TimeSpan.Zero) throw new XPScriptRuntimeException(5, "location: timeout must be greater than zero.");
        if (!XPScriptMobileServices.TryGet("location", out var service) || service is not IXPScriptLocationService location)
            throw new XPScriptRuntimeException(5, "location: " + XPScriptMobileServices.UnavailableReason("location"));
        if (!location.IsAvailable) throw new XPScriptRuntimeException(5, "location: " + location.UnavailableReason);
        try
        {
            return location.GetCurrent(timeout);
        }
        catch (TimeoutException)
        {
            throw new XPScriptRuntimeException(5, "location: timed out while waiting for a position fix.");
        }
        catch (XPScriptLocationUnavailableException exception)
        {
            throw new XPScriptRuntimeException(5, "location: " + exception.Message);
        }
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
