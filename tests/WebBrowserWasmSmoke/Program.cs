using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-browser-wasm-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var sourcePath = Path.Combine(root, "app.xps");
    await File.WriteAllTextAsync(sourcePath, """
[Platform:browser-wasm]
Sub Main()
    Dim form As New UIForm("Browser Smoke")
    Dim data As New JsonObject
    Dim markets As New JsonArray
    Dim grid As Variant
    Call markets.Add("SE")
    Call markets.Add("DK")
    Call data.Set("markets", markets)
    Call form.BindData(data)
    Call form.AddTextField("name", "Name")
    Call form.AddPasswordField("password", "Password")
    Call form.AddDateField("birthday", "Birthday")
    Call form.AddTimeField("start_time", "Start time")
    Call form.AddDateTimeField("meeting", "Meeting")
    Call form.AddMonthField("billing_month", "Billing month")
    Call form.AddSeparator("identity_separator")
    Call form.AddSelect("country", "Country")
    Call form.AddOption("country", "SE")
    Call form.AddOption("country", "NO")
    Call form.AddListBox("office", "Office")
    Call form.AddOption("office", "Stockholm")
    Call form.AddOption("office", "Oslo")
    Call form.AddMultiListBox("markets", "Markets")
    Call form.AddOption("markets", "SE")
    Call form.AddOption("markets", "NO")
    Call form.AddOption("markets", "DK")
    Call form.AddSpacer("actions_spacer")
    Call form.SetRequired("name", True)
    Call form.SetLength("password", 8, 128)
    Call form.SetRegexValidation("name", "^[A-Za-z ]+$")
    Call form.SetDateRange("birthday", "2020-01-01", "2030-12-31")
    Call form.SetTimeRange("start_time", "08:00", "18:00")
    Call form.SetDateTimeRange("meeting", "2026-01-01T08:00", "2026-12-31T18:00")
    Call form.SetMonthRange("billing_month", "2026-01", "2026-12")
    Call form.SetFieldPlaceholder("name", "Enter your name")
    Call form.SetFieldTooltip("name", "Customer display name")
    Call form.SetFieldTooltip("country", "Select a country")
    Set grid = form.AddGridColumns(12)
    Call grid.SetFieldPosition("name", 6)
    Call grid.SetFieldPosition("password", 6)
    Call grid.SetFieldPosition("identity_separator", 12)
    Call grid.SetFieldPosition("country", 4)
    Call grid.SetFieldPosition("office", 4)
    Call grid.SetFieldPosition("markets", 4)
    Call grid.SetFieldPosition("actions_spacer", 12)
    Call form.Navigate("page2")
    Call form.ShowDialog()
End Sub
""");

    var parser = new XpsWebRouteMetadataParser().Parse(await File.ReadAllTextAsync(sourcePath));
    if (!string.Equals(parser.Platform, "browser-wasm", StringComparison.Ordinal)) throw new Exception("Platform metadata was not detected.");

    var resolver = new XpsWebPathResolver(root);
    var asset = resolver.Resolve("/app.xps/_framework/dotnet.js");
    if (!asset.Found || asset.RouteFunction != XpsWebPathResolver.BrowserWasmAssetRoute) throw new Exception("WASM asset route was not resolved.");

    var compiler = new XpsWebCompiler();
    await using var unit = await compiler.CompileAsync(sourcePath, root);
    if (!unit.Routes.ContainsKey("Index") || !unit.Routes.ContainsKey(XpsWebPathResolver.BrowserWasmAssetRoute)) throw new Exception("Synthetic WASM routes are missing.");

    var cacheRoot = Path.Combine(root, ".xpscript-cache", "wasm");
    var index = Directory.EnumerateFiles(cacheRoot, "index.html", SearchOption.AllDirectories).FirstOrDefault();
    var dotnetJs = Directory.EnumerateFiles(cacheRoot, "dotnet.js", SearchOption.AllDirectories).FirstOrDefault();
    var browserJs = Directory.EnumerateFiles(cacheRoot, "xpscript-browser.js", SearchOption.AllDirectories).FirstOrDefault();
    var mainJs = Directory.EnumerateFiles(cacheRoot, "main.js", SearchOption.AllDirectories).FirstOrDefault();
    if (index is null || dotnetJs is null || browserJs is null || mainJs is null) throw new Exception("WASM publish output was not cached.");

    var frameworkRoot = Directory.GetParent(Path.GetDirectoryName(dotnetJs)!)?.FullName
        ?? throw new Exception("Unable to determine the published browser-WASM application root.");
    if (!File.Exists(Path.Combine(frameworkRoot, "index.html")) ||
        !File.Exists(Path.Combine(frameworkRoot, "main.js")) ||
        !File.Exists(Path.Combine(frameworkRoot, "xpscript-browser.js")))
        throw new Exception("Browser-WASM bootstrap assets are not colocated with the published _framework directory.");

    var bootstrap = await File.ReadAllTextAsync(Path.Combine(frameworkRoot, "index.html"));
    if (!bootstrap.Contains("<base href=\"app.xps/\">", StringComparison.Ordinal))
        throw new Exception("Browser WASM bootstrap does not anchor relative assets to its owning .xps route.");

    var browserModule = await File.ReadAllTextAsync(Path.Combine(frameworkRoot, "xpscript-browser.js"));
    foreach (var requiredMarker in new[]
    {
        "gridTemplateColumns", "form-select", "readOnly", "request.buttons", "xpscript:form-result",
        "multilistbox", "selectedOptions", "select.multiple", "field.placeholder", "field.regexPattern", "field.dateMinimum", "field.dateMaximum", "field.timeMinimum", "field.timeMaximum", "field.dateTimeMinimum", "field.dateTimeMaximum", "field.monthMinimum", "field.monthMaximum", "field.tooltip",
        "type === 'separator'", "type === 'spacer'"
    })
    {
        if (!browserModule.Contains(requiredMarker, StringComparison.Ordinal))
            throw new Exception($"Browser UIForm renderer is missing parity marker '{requiredMarker}'.");
    }

    Console.WriteLine("browser-wasm smoke passed");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}
