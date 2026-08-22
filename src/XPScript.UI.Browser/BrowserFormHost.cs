using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XPScript.UI.Browser;

public static partial class BrowserFormHost
{
    private const string NavigationStateStorageKey = "xpscript.request-state.navigation";
    private const int NavigationStateLifetimeMilliseconds = 60_000;

    public static string ShowDialog(string requestJson)
    {
        ArgumentNullException.ThrowIfNull(requestJson);
        var normalized = NormalizeStructuralElements(requestJson);
        ApplyApplicationMetadata(normalized);
        return RenderForm(normalized);
    }

    public static void StageRequestState(string stateJson)
    {
        ArgumentNullException.ThrowIfNull(stateJson);
        if (stateJson.Length > 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(stateJson), "Browser Request.State navigation payload exceeds 1 MiB.");

        StageRequestStateInBrowser(NavigationStateStorageKey, stateJson);
    }

    public static string ConsumeRequestState() =>
        ConsumeRequestStateInBrowser(NavigationStateStorageKey, NavigationStateLifetimeMilliseconds) ?? string.Empty;

    public static void Navigate(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("Browser navigation target is required.", nameof(target));

        var path = target.Trim().Replace('\\', '/');
        var extension = Path.GetExtension(path);
        if (path.Length > 512 || path.StartsWith('/') || path.Contains("..", StringComparison.Ordinal) ||
            Uri.TryCreate(path, UriKind.Absolute, out _) ||
            (extension.Length > 0 && !extension.Equals(".xps", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Browser navigation target must be a relative local XPS module path with an optional .xps extension.", nameof(target));

        NavigateInBrowser(path, NavigationStateStorageKey);
    }

    private static void ApplyApplicationMetadata(string requestJson)
    {
        var root = JsonNode.Parse(requestJson)?.AsObject();
        if (root is null) return;

        var title = root["applicationTitle"]?.GetValue<string>() ?? string.Empty;
        var icon = root["applicationIcon"]?.GetValue<string>() ?? string.Empty;
        ApplyApplicationMetadataInBrowser(title, icon);
    }

    private static string NormalizeStructuralElements(string requestJson)
    {
        var root = JsonNode.Parse(requestJson)?.AsObject()
            ?? throw new InvalidOperationException("Browser UIForm request is empty.");
        if (root["fields"] is not JsonArray fields) return requestJson;

        foreach (var node in fields)
        {
            if (node is not JsonObject field) continue;
            var type = field["type"]?.GetValue<string>();
            if (string.Equals(type, "Separator", StringComparison.Ordinal))
            {
                field["type"] = "RadioGroup";
                field["label"] = "────────────────────────────────────────";
                field["required"] = false;
                field["readOnly"] = true;
                field["options"] = new JsonArray();
            }
            else if (string.Equals(type, "Spacer", StringComparison.Ordinal))
            {
                field["type"] = "RadioGroup";
                field["label"] = string.Empty;
                field["required"] = false;
                field["readOnly"] = true;
                field["options"] = new JsonArray();
            }
        }

        return root.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    [JSImport("renderForm", "xpscript-browser")]
    private static partial string RenderForm(string requestJson);

    [JSImport("stageRequestState", "xpscript-browser")]
    private static partial void StageRequestStateInBrowser(string key, string stateJson);

    [JSImport("consumeRequestState", "xpscript-browser")]
    private static partial string? ConsumeRequestStateInBrowser(string key, int lifetimeMilliseconds);

    [JSImport("navigate", "xpscript-browser")]
    private static partial void NavigateInBrowser(string target, string key);

    [JSImport("applyApplicationMetadata", "xpscript-browser")]
    private static partial void ApplyApplicationMetadataInBrowser(string title, string icon);
}
