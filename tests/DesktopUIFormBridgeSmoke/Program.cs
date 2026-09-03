using System.Text.Json;
using XPScript.Compiler;
using XPScript.UI.Desktop;

var requestJson = """
{
  "title": "Bridge smoke",
  "width": 640,
  "height": 480,
  "resizable": true,
  "fields": [
    {
      "name": "name",
      "label": "Name",
      "type": "TextField",
      "required": true,
      "value": "Kalle",
      "minLength": 2,
      "maxLength": 100,
      "minimum": null,
      "maximum": null,
      "options": []
    },
    {
      "name": "age",
      "label": "Age",
      "type": "NumberField",
      "required": false,
      "value": "42",
      "minLength": null,
      "maxLength": null,
      "minimum": 0,
      "maximum": 150,
      "options": []
    },
    {
      "name": "country",
      "label": "Country",
      "type": "Select",
      "required": true,
      "value": "SE",
      "minLength": null,
      "maxLength": null,
      "minimum": null,
      "maximum": null,
      "options": ["SE", "NO", "DK"]
    }
  ]
}
""";

var request = XpsUIDesktopRuntimeBridge.ParseRequest(requestJson);
if (request.Title != "Bridge smoke" || request.Width != 640 || request.Height != 480 || !request.Resizable)
    throw new InvalidOperationException("Desktop UIForm request metadata did not round-trip.");
if (request.Fields.Count != 3)
    throw new InvalidOperationException("Desktop UIForm field count mismatch.");
if (request.Fields[0].Name != "name" || request.Fields[0].MinLength != 2 || request.Fields[0].MaxLength != 100)
    throw new InvalidOperationException("Desktop UIForm text field metadata mismatch.");
if (request.Fields[1].Minimum != 0 || request.Fields[1].Maximum != 150)
    throw new InvalidOperationException("Desktop UIForm numeric range metadata mismatch.");
if (!request.Fields[2].Options.SequenceEqual(["SE", "NO", "DK"]))
    throw new InvalidOperationException("Desktop UIForm options metadata mismatch.");

var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
{
    ["name"] = JsonSerializer.SerializeToElement("Sven"),
    ["age"] = JsonSerializer.SerializeToElement(43m),
    ["enabled"] = JsonSerializer.SerializeToElement(true)
};
var resultJson = XpsUIDesktopRuntimeBridge.SerializeResult(new DesktopFormResult("OK", values));
using var result = JsonDocument.Parse(resultJson);
var root = result.RootElement;
if (root.GetProperty("result").GetString() != "OK")
    throw new InvalidOperationException("Desktop UIForm result mismatch.");
if (root.GetProperty("values").GetProperty("name").GetString() != "Sven")
    throw new InvalidOperationException("Desktop UIForm string result mismatch.");
if (root.GetProperty("values").GetProperty("age").ValueKind != JsonValueKind.Number || root.GetProperty("values").GetProperty("age").GetDecimal() != 43m)
    throw new InvalidOperationException("Desktop UIForm numeric result mismatch.");
if (root.GetProperty("values").GetProperty("enabled").ValueKind != JsonValueKind.True)
    throw new InvalidOperationException("Desktop UIForm Boolean result mismatch.");

var source = """
Sub Main()
    Dim answer As String
    Dim fileName As String
    answer = ShowDialog("Save?", "Confirm", "YesNo")
    answer = ShowDialog("Environment", "Choose", "List", "Dev|Test|Prod")
    fileName = LoadFileDialog("Load", "", "JSON|*.json")
    fileName = OpenFileDialog("Open")
    fileName = SaveFileDialog("Save", "report.json", "JSON|*.json")
End Sub
""";
var generated = new XPScriptTranspiler().Transpile(source, "ui-dialog-smoke.xps", "win-x64");
foreach (var expected in new[]
{
    "XPScriptUIDialogRuntime.ShowDialog(",
    "XPScriptUIDialogRuntime.LoadFileDialog(",
    "XPScriptUIDialogRuntime.OpenFileDialog(",
    "XPScriptUIDialogRuntime.SaveFileDialog(",
    "internal static class XPScriptUIDialogRuntime",
    "internal static class XPScriptUIDesktopAdapter"
})
{
    if (!generated.Contains(expected, StringComparison.Ordinal))
        throw new InvalidOperationException("Generated desktop dialog runtime is missing: " + expected);
}
if (!generated.Contains("public string ShowDialog()", StringComparison.Ordinal))
    throw new InvalidOperationException("UIForm.ShowDialog method declaration was incorrectly rewritten.");

var multiWindowSource = """
Sub Main()
    Dim first As New UIForm("First", 480, 320, True)
    Dim second As New UIForm("Second", 480, 320, True)
    Call first.Show(False)
    Call second.Show(False)
End Sub
""";
var generatedMultiWindow = new XPScriptTranspiler().Transpile(multiWindowSource, "uiform-multiwindow-smoke.xps", "win-x64");
foreach (var expected in new[]
{
    "public void Show() => Show(false);",
    "public void Show(object? modalValue)",
    "public void Close()",
    "private readonly string _instanceId = Guid.NewGuid().ToString(\"N\")",
    "XPScriptUIDesktopAdapter.Show(this, _fields, _data, ApplyDesktopValue, ApplyDesktopValues);",
    "XPScriptUIDesktopAdapter.Close(_instanceId);"
})
{
    if (!generatedMultiWindow.Contains(expected, StringComparison.Ordinal))
        throw new InvalidOperationException("Generated modeless UIForm lifecycle is missing: " + expected);
}

var applicationHostType = typeof(DesktopFormHost).Assembly.GetType("XPScript.UI.Desktop.DesktopApplicationHost", throwOnError: false);
if (applicationHostType is null ||
    applicationHostType.GetMethod("EnsureStarted", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static) is null ||
    applicationHostType.GetMethod("SetProcessKeepAlive", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static) is null)
    throw new InvalidOperationException("Desktop modeless UI dispatcher host is missing its lifecycle API.");

var listSource = """
Sub Main()
    Dim rows As New JsonArray
    Dim row As New JsonObject
    Dim list As New UIListView("Customers")
    Call row.Set("id", "1001")
    Call row.Set("name", "Kalle")
    Call rows.Add(row)
    Call list.BindData(rows)
    Call list.AddColumn("name", "Name")
    Call list.SetKeyField("id")
End Sub
""";
var generatedList = new XPScriptTranspiler().Transpile(listSource, "ui-list-smoke.xps", "win-x64");
foreach (var expected in new[]
{
    "internal static class XPScriptUIList",
    "internal sealed class XPScriptUIListView",
    "XPScriptUIList.CreateListView("
})
{
    if (!generatedList.Contains(expected, StringComparison.Ordinal))
        throw new InvalidOperationException("Generated UIListView runtime is missing: " + expected);
}

Console.WriteLine("DESKTOP_UIFORM_BRIDGE_OK");
Console.WriteLine("DESKTOP_DIALOG_TRANSPILE_OK");
Console.WriteLine("DESKTOP_UIFORM_MULTIWINDOW_TRANSPILE_OK");
Console.WriteLine("DESKTOP_UILISTVIEW_TRANSPILE_OK");
