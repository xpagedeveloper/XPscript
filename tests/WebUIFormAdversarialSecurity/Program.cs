using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using XPScript.Web.Compiler;
using XPScript.Web.Kestrel;
using XPScript.Web.Runtime;

Environment.SetEnvironmentVariable("XPSCRIPT_WEB_CONSOLE_ERRORS", "1");

const string adversarial = "\"\\ </script><script>alert('x')</script> åäö 漢字 😀 {\"nested\":[1,true,null]}";
const string optionPayload = "\"\\ <img src=x onerror=alert(1)> åäö 漢字 😀 {\"role\":\"admin\"}";
const string emailPayload = "qa+\"\\åäö@example.test";
const string urlPayload = "https://example.test/a/%22%5C?q=%7B%22x%22%3A1%7D";

var parent = Path.Combine(Path.GetTempPath(), "xps-uiform-adversarial-" + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "site");
Directory.CreateDirectory(root);
var scriptPath = Path.Combine(root, "form.xps");

var script = string.Join(Environment.NewLine,
[
    "[Anonymous]",
    "[Get]",
    "[Post]",
    "Sub Index()",
    "    Dim data As New XPJsonObject",
    "    Dim form As New UIForm(" + XpsString("Security <script>alert(\"title\")</script> \" \\ åäö 漢字 😀") + ")",
    "    Dim result As String",
    "",
    "    Call data.Set(\"text\", \"seed\")",
    "    Call data.Set(\"textarea\", \"seed\")",
    "    Call data.Set(\"hidden\", \"seed\")",
    "    Call data.Set(\"email\", \"seed@example.test\")",
    "    Call data.Set(\"url\", \"https://example.test/\")",
    "    Call data.Set(\"select\", \"safe\")",
    "    Call data.Set(\"listbox\", \"safe\")",
    "    Call data.Set(\"radio\", \"safe\")",
    "    Call data.Set(\"multi\", \"safe\")",
    "    Call data.Set(\"multiselect\", \"safe\")",
    "    Call data.Set(\"checkboxgroup\", \"safe\")",
    "    Call form.BindData(data)",
    "",
    "    Call form.AddTextField(\"text\", " + XpsString("Text <script>alert(\"label\")</script>") + ")",
    "    Call form.AddTextArea(\"textarea\", " + XpsString("Area <img src=x onerror=alert(1)>") + ")",
    "    Call form.AddPasswordField(\"password\", " + XpsString("Password <script>alert(\"pw\")</script>") + ")",
    "    Call form.AddEmailField(\"email\", " + XpsString("Email <script>alert(\"email\")</script>") + ")",
    "    Call form.AddUrlField(\"url\", " + XpsString("URL <script>alert(\"url\")</script>") + ")",
    "    Call form.AddHiddenField(\"hidden\")",
    "    Call form.AddCheckBox(\"checkbox\", " + XpsString("Check <script>alert(\"check\")</script>") + ")",
    "    Call form.AddSelect(\"select\", " + XpsString("Select <script>alert(\"select\")</script>") + ")",
    "    Call form.AddOption(\"select\", \"safe\")",
    "    Call form.AddOption(\"select\", " + XpsString(optionPayload) + ")",
    "    Call form.AddListBox(\"listbox\", " + XpsString("List <script>alert(\"list\")</script>") + ")",
    "    Call form.AddOption(\"listbox\", \"safe\")",
    "    Call form.AddOption(\"listbox\", " + XpsString(optionPayload) + ")",
    "    Call form.AddRadioGroup(\"radio\", " + XpsString("Radio <script>alert(\"radio\")</script>") + ")",
    "    Call form.AddOption(\"radio\", \"safe\")",
    "    Call form.AddOption(\"radio\", " + XpsString(optionPayload) + ")",
    "    Call form.AddMultiListBox(\"multi\", " + XpsString("Multi <script>alert(\"multi\")</script>") + ")",
    "    Call form.AddOption(\"multi\", \"safe\")",
    "    Call form.AddOption(\"multi\", " + XpsString(optionPayload) + ")",
    "    Call form.AddMultiSelect(\"multiselect\", " + XpsString("MultiSelect <script>alert(\"ms\")</script>") + ")",
    "    Call form.AddOption(\"multiselect\", \"safe\")",
    "    Call form.AddOption(\"multiselect\", " + XpsString(optionPayload) + ")",
    "    Call form.AddCheckBoxGroup(\"checkboxgroup\", " + XpsString("Group <script>alert(\"group\")</script>") + ")",
    "    Call form.AddOption(\"checkboxgroup\", \"safe\")",
    "    Call form.AddOption(\"checkboxgroup\", " + XpsString(optionPayload) + ")",
    "",
    "    result = form.ShowDialog()",
    "    If result = \"OK\" Then",
    "        Response.ContentType = \"application/json; charset=utf-8\"",
    "        Response.Write(data.Stringify())",
    "    End If",
    "End Sub"
]);

await File.WriteAllTextAsync(scriptPath, script);

await using var cache = new XpsWebCompilationCache(new XpsWebCompiler());
await using var dispatcher = new XpsWebDispatcher(root, cache);
var options = new XpsKestrelOptions
{
    Port = 0,
    MaxRequestBodySize = 1024 * 1024,
    AllowedHosts = ["localhost", "127.0.0.1", "::1"]
};
var serverInfo = new XpsServerInfo("uiform-adversarial-security", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test");
var app = XpsKestrelAdapter.Build(options, serverInfo, dispatcher, new SmokeApplicationState());
var stopped = false;

try
{
    await app.StartAsync();
    var server = app.Services.GetRequiredService<IServer>();
    var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.Single()
        ?? throw new Exception("Kestrel did not expose a listener address.");
    using var client = new HttpClient { BaseAddress = new Uri(address) };

    using (var response = await client.GetAsync("/form.xps"))
    {
        if ((int)response.StatusCode != 200)
            throw new Exception($"UIForm adversarial GET expected 200, got {(int)response.StatusCode}.");

        var html = await response.Content.ReadAsStringAsync();
        AssertEncoded(html, "<script>alert(\"title\")</script>");
        AssertEncoded(html, "<script>alert(\"label\")</script>");
        AssertEncoded(html, "<img src=x onerror=alert(1)>");
        AssertEncoded(html, optionPayload);

        if (html.Contains("<script>alert(\"title\")</script>", StringComparison.Ordinal)
            || html.Contains("<script>alert(\"label\")</script>", StringComparison.Ordinal)
            || html.Contains("<img src=x onerror=alert(1)>", StringComparison.Ordinal))
        {
            throw new Exception("UIForm adversarial GET emitted unencoded attacker-controlled HTML.");
        }
    }

    using var content = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["text"] = adversarial,
        ["textarea"] = adversarial,
        ["password"] = adversarial,
        ["email"] = emailPayload,
        ["url"] = urlPayload,
        ["hidden"] = adversarial,
        ["checkbox"] = "on",
        ["select"] = optionPayload,
        ["listbox"] = optionPayload,
        ["radio"] = optionPayload,
        ["multi"] = optionPayload,
        ["multiselect"] = optionPayload,
        ["checkboxgroup"] = optionPayload
    });

    using (var response = await client.PostAsync("/form.xps", content))
    {
        if ((int)response.StatusCode != 200)
            throw new Exception($"UIForm adversarial POST expected 200, got {(int)response.StatusCode}.");

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var rootJson = json.RootElement;

        AssertJsonString(rootJson, "text", adversarial);
        AssertJsonString(rootJson, "textarea", adversarial);
        AssertJsonString(rootJson, "password", adversarial);
        AssertJsonString(rootJson, "email", emailPayload);
        AssertJsonString(rootJson, "url", urlPayload);
        AssertJsonString(rootJson, "hidden", adversarial);

        if (!rootJson.GetProperty("checkbox").GetBoolean())
            throw new Exception("UIForm adversarial checkbox did not preserve Boolean typing.");

        AssertContains(rootJson, "select", optionPayload);
        AssertContains(rootJson, "listbox", optionPayload);
        AssertContains(rootJson, "radio", optionPayload);
        AssertContains(rootJson, "multi", optionPayload);
        AssertContains(rootJson, "multiselect", optionPayload);
        AssertContains(rootJson, "checkboxgroup", optionPayload);
    }

    Console.WriteLine("WEB-UIFORM-ADVERSARIAL=OK");
    await app.StopAsync();
    stopped = true;
}
finally
{
    if (!stopped) await app.StopAsync();
    await app.DisposeAsync();
    if (Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
}

static string XpsString(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

static void AssertEncoded(string html, string raw)
{
    var encoded = WebUtility.HtmlEncode(raw);
    if (!html.Contains(encoded, StringComparison.Ordinal))
        throw new Exception("Expected HTML-encoded UIForm content was not rendered: " + encoded);
}

static void AssertJsonString(JsonElement root, string name, string expected)
{
    var value = root.GetProperty(name);
    if (value.ValueKind != JsonValueKind.String || value.GetString() != expected)
        throw new Exception($"UIForm JSON field '{name}' did not round-trip adversarial text exactly.");
}

static void AssertContains(JsonElement root, string name, string expected)
{
    var value = root.GetProperty(name);
    if (value.ValueKind == JsonValueKind.String)
    {
        if (value.GetString() != expected)
            throw new Exception($"UIForm option field '{name}' did not preserve adversarial text.");
        return;
    }

    if (value.ValueKind == JsonValueKind.Array
        && value.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && item.GetString() == expected))
    {
        return;
    }

    throw new Exception($"UIForm option field '{name}' did not contain the adversarial option.");
}

sealed class SmokeApplicationState : IXpsApplicationState
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
    public object? Get(string name) => _values.TryGetValue(name, out var value) ? value : null;
    public void Set(string name, object? value) => _values[name] = value;
    public bool Remove(string name) => _values.Remove(name);
    public void Clear() => _values.Clear();
}
