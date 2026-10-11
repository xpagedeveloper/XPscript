using System.Text.Json;

namespace XPScript.Cli;

internal sealed record AndroidProjectMetadata(string Target, string ApplicationType)
{
    public const string DefaultTarget = "android";
    public const string DefaultApplicationType = "headless";
    public const string UiApplicationType = "ui";

    public static AndroidProjectMetadata LoadForSource(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourcePath))
            ?? throw new InvalidOperationException("Unable to determine Android source directory.");
        var configPath = Path.Combine(sourceDirectory, "xpscript.json");
        if (!File.Exists(configPath))
            return new AndroidProjectMetadata(
                DefaultTarget,
                UsesUIForm(sourcePath) ? UiApplicationType : DefaultApplicationType);

        try
        {
            using var document = JsonDocument.Parse(
                File.ReadAllText(configPath),
                new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });

            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("xpscript.json root must be a JSON object.");

            var target = ReadString(root, "target") ?? DefaultTarget;
            var applicationType = ReadString(root, "applicationType") ??
                (UsesUIForm(sourcePath) ? UiApplicationType : DefaultApplicationType);

            target = target.Trim().ToLowerInvariant();
            applicationType = applicationType.Trim().ToLowerInvariant();

            if (target != DefaultTarget)
                throw new InvalidOperationException("Android run requires xpscript.json target to be 'android'.");
            if (applicationType is not (DefaultApplicationType or UiApplicationType))
                throw new InvalidOperationException(
                    "Android applicationType '" + applicationType +
                    "' is not supported. Supported applicationTypes: headless, ui.");

            return new AndroidProjectMetadata(target, applicationType);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Invalid Android project config '" + configPath + "': " + ex.Message, ex);
        }
    }

    private static bool UsesUIForm(string sourcePath)
    {
        if (!File.Exists(sourcePath)) return false;
        var source = File.ReadAllText(sourcePath);
        return source.Contains("UIForm", StringComparison.OrdinalIgnoreCase) ||
               source.Contains("AddImage", StringComparison.OrdinalIgnoreCase) ||
               source.Contains("AddWebView", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("xpscript.json " + propertyName + " must be a string.");
        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("xpscript.json " + propertyName + " cannot be empty.");
        return text;
    }
}
