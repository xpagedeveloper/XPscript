using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XPScript.UI.Browser;

public static partial class BrowserFormHost
{
    public static string ShowDialog(string requestJson)
    {
        ArgumentNullException.ThrowIfNull(requestJson);
        var normalized = NormalizeStructuralElements(requestJson);
        ApplyApplicationMetadata(normalized);
        return RenderForm(normalized);
    }

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

        var encoded = JsonSerializer.Serialize(path);
        Eval("(() => { const target = " + encoded + "; const current = window.location.pathname; const slash = current.lastIndexOf('/'); const basePath = slash >= 0 ? current.substring(0, slash + 1) : '/'; window.location.href = basePath + target; })();");
    }

    private static void ApplyApplicationMetadata(string requestJson)
    {
        var root = JsonNode.Parse(requestJson)?.AsObject();
        if (root is null) return;

        var title = root["applicationTitle"]?.GetValue<string>() ?? string.Empty;
        if (title.Length > 0)
            Eval("document.title = " + JsonSerializer.Serialize(title) + ";");

        var icon = root["applicationIcon"]?.GetValue<string>() ?? string.Empty;
        if (icon.Length > 0)
        {
            var encoded = JsonSerializer.Serialize(icon);
            Eval("(() => { let link = document.querySelector('link[rel~=\"icon\"]'); if (!link) { link = document.createElement('link'); link.rel = 'icon'; document.head.appendChild(link); } link.href = " + encoded + "; })();");
        }
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

    [JSImport("eval", "globalThis")]
    private static partial void Eval(string script);
}
