using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XPScript.UI.Browser;

public static partial class BrowserFormHost
{
    public static string ShowDialog(string requestJson)
    {
        ArgumentNullException.ThrowIfNull(requestJson);
        return RenderForm(NormalizeStructuralElements(requestJson));
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
}
