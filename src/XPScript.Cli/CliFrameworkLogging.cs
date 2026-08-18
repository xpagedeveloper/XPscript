using System.Runtime.CompilerServices;

internal static class CliFrameworkLogging
{
    [ModuleInitializer]
    internal static void Configure()
    {
        // The xpscript CLI owns its console output. Suppress ASP.NET Core / hosting
        // framework categories so users see XPScript startup information and
        // XPScript diagnostics instead of Microsoft.Hosting/Microsoft.AspNetCore logs.
        Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft", "None");
    }
}
