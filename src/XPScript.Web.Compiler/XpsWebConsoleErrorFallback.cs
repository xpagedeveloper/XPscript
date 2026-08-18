namespace XPScript.Web.Compiler;

internal static class XpsWebConsoleErrorFallback
{
    private const string EnvironmentVariable = "XPSCRIPT_WEB_CONSOLE_ERRORS";

    public static void Write(Exception exception, string? sourcePath, string? requestPath)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (!string.Equals(Environment.GetEnvironmentVariable(EnvironmentVariable), "1", StringComparison.Ordinal)) return;

        var source = SafeFileName(sourcePath);
        var request = string.IsNullOrWhiteSpace(requestPath) ? "/" : requestPath;
        Console.Error.WriteLine($"XPScript web error: request={request} source={source}");
        Console.Error.WriteLine(exception.Message);
        if (exception.InnerException is not null && !string.Equals(exception.InnerException.Message, exception.Message, StringComparison.Ordinal))
            Console.Error.WriteLine(exception.InnerException.Message);
    }

    private static string SafeFileName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "unknown.xps";
        try { return Path.GetFileName(path); }
        catch { return "unknown.xps"; }
    }
}
