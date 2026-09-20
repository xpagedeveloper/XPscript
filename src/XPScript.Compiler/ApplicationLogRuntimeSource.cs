namespace XPScript.Compiler;

internal static class ApplicationLogRuntimeSource
{
    public const string Code = """
internal static class XPScriptApplicationLogRuntime
{
    public static void Trace(object? eventName, object? message) => Write("trace", eventName, message, null, false);
    public static void Trace(object? eventName, object? message, object? attributes) => Write("trace", eventName, message, attributes, false);
    public static void Debug(object? eventName, object? message) => Write("debug", eventName, message, null, false);
    public static void Debug(object? eventName, object? message, object? attributes) => Write("debug", eventName, message, attributes, false);
    public static void Info(object? eventName, object? message) => Write("info", eventName, message, null, false);
    public static void Info(object? eventName, object? message, object? attributes) => Write("info", eventName, message, attributes, false);
    public static void Warning(object? eventName, object? message) => Write("warning", eventName, message, null, false);
    public static void Warning(object? eventName, object? message, object? attributes) => Write("warning", eventName, message, attributes, false);
    public static void Error(object? eventName, object? message) => Write("error", eventName, message, null, false);
    public static void Error(object? eventName, object? message, object? attributes) => Write("error", eventName, message, attributes, false);
    public static void Critical(object? eventName, object? message) => Write("critical", eventName, message, null, false);
    public static void Critical(object? eventName, object? message, object? attributes) => Write("critical", eventName, message, attributes, false);

    internal static void Write(string severity, object? eventName, object? message, object? attributes, bool audit)
    {
        var eventText = XPScriptRuntime.CStr(eventName);
        var messageText = XPScriptRuntime.CStr(message);
        var attributesJson = SerializeAttributes(attributes);
        var runtimeType = System.Type.GetType("XPScript.Web.Runtime.XpsWebRuntimeObjects, XPScript.Web.Runtime", throwOnError: false)
            ?? throw new XPScriptRuntimeException(5, "Application.Log requires the XPScript web runtime.");
        var method = runtimeType.GetMethod(
            "WriteApplicationLog",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(bool) },
            null) ?? throw new MissingMethodException(runtimeType.FullName, "WriteApplicationLog");
        try
        {
            method.Invoke(null, new object?[] { severity, eventText, messageText, attributesJson, audit });
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static string? SerializeAttributes(object? attributes)
    {
        if (attributes is null) return null;
        if (attributes is string text) return text;
        var method = attributes.GetType().GetMethod(
            "Stringify",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
            null,
            System.Type.EmptyTypes,
            null);
        if (method is null)
            throw new XPScriptRuntimeException(5, "Application.Log attributes must be an XPJson object.");
        try
        {
            return XPScriptRuntime.CStr(method.Invoke(attributes, null));
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}

internal static class XPScriptApplicationAuditRuntime
{
    public static void Write(object? eventName, object? message) =>
        XPScriptApplicationLogRuntime.Write("info", eventName, message, null, true);

    public static void Write(object? eventName, object? message, object? attributes) =>
        XPScriptApplicationLogRuntime.Write("info", eventName, message, attributes, true);
}
""";
}
