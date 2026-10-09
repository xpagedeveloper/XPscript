namespace XPScript.Compiler;

internal static class MobileServiceRuntimeSource
{
    public const string Code = """
public interface IXPScriptMobileService
{
    string Capability { get; }
    bool IsAvailable { get; }
    string UnavailableReason { get; }
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

    public static bool IsAvailable(object? capability)
        => Services.TryGetValue(XPScriptRuntime.CStr(capability), out var service) && service.IsAvailable;

    public static string UnavailableReason(object? capability)
        => Services.TryGetValue(XPScriptRuntime.CStr(capability), out var service) ? service.UnavailableReason : "Mobile capability is not registered.";
}
""";
}
