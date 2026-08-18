using System.Runtime.CompilerServices;
using System.Text.Json;

internal static class CliConsoleErrorPolicy
{
    private const string EnvironmentVariable = "XPSCRIPT_WEB_CONSOLE_ERRORS";

    [ModuleInitializer]
    internal static void Configure()
    {
        try
        {
            Environment.SetEnvironmentVariable(EnvironmentVariable, HasConfiguredWebLog() ? "0" : "1");
        }
        catch
        {
            // Fail safe for diagnostics. If settings cannot be inspected, keep console errors enabled.
            Environment.SetEnvironmentVariable(EnvironmentVariable, "1");
        }
    }

    private static bool HasConfiguredWebLog()
    {
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--structured-log", StringComparison.OrdinalIgnoreCase)) return true;
        }

        string? configPath = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals("--config", StringComparison.OrdinalIgnoreCase)) continue;
            if (i + 1 < args.Length) configPath = args[i + 1];
            break;
        }

        configPath ??= Path.Combine(AppContext.BaseDirectory, "web.cfg");
        if (!File.Exists(configPath)) return false;

        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        if (!document.RootElement.TryGetProperty("web", out var web) || web.ValueKind != JsonValueKind.Object) return false;
        foreach (var property in web.EnumerateObject())
        {
            if (!property.Name.Equals("structuredLog", StringComparison.OrdinalIgnoreCase)) continue;
            return property.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.Value.GetString());
        }
        return false;
    }
}
