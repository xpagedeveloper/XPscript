using System.Text.Json;
using System.Diagnostics;
using XPScript.Compiler;
using XPScript.UI.Desktop;

var parseDetachedRequest = typeof(DesktopFormLifecycleHost).GetMethod(
    "ParseDetachedRequest",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
    ?? throw new InvalidOperationException("Desktop modeless UI request detachment helper is missing.");

var detachedArguments = new object?[]
{
    "{\"instanceId\":\"detached-smoke\",\"title\":\"Detached request\",\"fields\":[]}",
    null
};

var detachedRoot = (JsonElement)(parseDetachedRequest.Invoke(null, detachedArguments)
    ?? throw new InvalidOperationException("Desktop modeless UI request was not parsed."));

if (!string.Equals(detachedArguments[1] as string, "detached-smoke", StringComparison.Ordinal))
    throw new InvalidOperationException("Desktop modeless UI request instance id was not preserved.");
if (detachedRoot.GetProperty("title").GetString() != "Detached request")
    throw new InvalidOperationException("Desktop modeless UI request remained tied to a disposed JsonDocument.");

var desktopParitySource = """
Sub Main()
    Dim form As New UIForm("Desktop parity", 480, 320, True)
    Call form.AddTextField("name", "Name")
    Call form.Show(False)
    Call form.Hide()
End Sub
""";
var parityRoot = Path.Combine(
    Directory.GetCurrentDirectory(),
    "out",
    "xpscript-desktop-parity-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(parityRoot);
var paritySourcePath = Path.Combine(parityRoot, "desktop-parity.xps");
await File.WriteAllTextAsync(paritySourcePath, desktopParitySource);
var compilerPath = Path.GetFullPath(Path.Combine(
    Directory.GetCurrentDirectory(),
    "src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll"));
if (!File.Exists(compilerPath))
    throw new InvalidOperationException("Built XPScript compiler was not found: " + compilerPath);
var astOutput = Path.Combine(parityRoot, "ast");
var legacyOutput = Path.Combine(parityRoot, "legacy");
if (args.Contains("--legacy-only", StringComparer.Ordinal))
{
    Console.WriteLine("DESKTOP_PARITY_LEGACY_COMPILE_START");
    await RunCompiler("legacy-compile", paritySourcePath, "-o", legacyOutput, "--single-file=false", "--runtime=false");
    Console.WriteLine("DESKTOP_PARITY_LEGACY_COMPILE_OK");
    Console.WriteLine("DESKTOP_PARITY_LEGACY_RUNTIME_START");
    await ProbeDesktopRuntime("legacy", legacyOutput);
    Console.WriteLine("DESKTOP_PARITY_LEGACY_RUNTIME_OK");
    return;
}
if (args.Contains("--ast-only", StringComparer.Ordinal))
{
    Console.WriteLine("DESKTOP_PARITY_AST_COMPILE_START");
    await RunCompiler("ast-compile", paritySourcePath, "-o", astOutput, "--single-file=false", "--runtime=false");
    Console.WriteLine("DESKTOP_PARITY_AST_COMPILE_OK");
    Console.WriteLine("DESKTOP_PARITY_AST_RUNTIME_START");
    await ProbeDesktopRuntime("AST", "dotnet", Path.Combine(astOutput, "Generated.dll"));
    Console.WriteLine("DESKTOP_PARITY_AST_RUNTIME_OK");
    return;
}
await RunCompiler("ast-compile", paritySourcePath, "-o", astOutput, "--single-file=false", "--runtime=false");
await RunCompiler("legacy-compile", paritySourcePath, "-o", legacyOutput, "--single-file=false", "--runtime=false");
if (!Directory.Exists(astOutput) || !Directory.EnumerateFileSystemEntries(astOutput).Any() ||
    !File.Exists(legacyOutput) && !Directory.Exists(legacyOutput))
    throw new InvalidOperationException("Desktop parity fixture did not produce both AST and legacy outputs.");
var astAssembly = Path.Combine(astOutput, "Generated.dll");
if (!File.Exists(astAssembly))
    throw new InvalidOperationException("Desktop parity AST output is missing Generated.dll.");
Console.WriteLine("DESKTOP_PARITY_AST_RUNTIME_START");
await ProbeDesktopRuntime("AST", "dotnet", astAssembly);
Console.WriteLine("DESKTOP_PARITY_AST_RUNTIME_OK");
Console.WriteLine("DESKTOP_PARITY_LEGACY_RUNTIME_START");
await ProbeDesktopRuntime("legacy", legacyOutput);
Console.WriteLine("DESKTOP_PARITY_LEGACY_RUNTIME_OK");
Console.WriteLine("DESKTOP_COMPILE_PARITY_FIXTURE_OK");
Console.WriteLine("DESKTOP_RUNTIME_LAUNCH_PARITY_OK");

var modalChildSource = """
Option Declare

Sub Main()
    Dim first As New UIForm("First form", 480, 320, True)
    Call first.AddTextField("name", "Name")
    Call first.AddButtonCallback("hello", "Klicka här", "OpenSecondForm")
    Call first.Show(True)
End Sub

Sub OpenSecondForm(evt As Variant)
    Dim secondForm As New UIForm("Second form", 480, 320, True)
    Call secondForm.AddTextField("value", "Value")
    Call secondForm.AddButtonCallback("close", "Close", "CloseSecondForm")
    Call secondForm.Show(False)
End Sub

Sub CloseSecondForm(evt As Variant)
    Call evt.Form.Close()
End Sub
""";

var generated = new XPScriptTranspiler().Transpile(modalChildSource, "uiform-modal-child-smoke.xps", "win-x64");
foreach (var expected in new[]
{
    "public void Show(object? modalValue)",
    "XPScriptCallbackRuntime.Invoke(",
    "XPScriptUIDesktopAdapter.Show(this, _fields, _data, ApplyDesktopValue, ApplyDesktopValues);",
    "new XPScriptUIFormEvent(this, \"button\"",
    "public string AccessibleName",
    "public string InitialFocus",
    "BuildAccessibilityAttributes(",
    "aria-describedby"
})
{
    if (!generated.Contains(expected, StringComparison.Ordinal))
        throw new InvalidOperationException("Generated baseline UIForm surface is missing: " + expected);
}

Console.WriteLine("DESKTOP_UIFORM_DETACHED_REQUEST_OK");
Console.WriteLine("DESKTOP_UIFORM_ACCESSIBILITY_BASELINE_OK");

var accessibilitySource = """
Option Declare

Sub Main()
    Dim form As New UIForm("Accessible customer")
    Dim name As Variant
    name = form.AddTextField("name", "Name")
    name.AccessibleName = "Customer name"
    name.AccessibleDescription = "Enter the full name"
    name.TabIndex = 10
    name.IsTabStop = True
    name.Focusable = True
    form.InitialFocus = "name"
    form.ValidationSummary = True
    form.FocusFirstError = True
    form.AnnounceValidationErrors = True
    Call form.SetValidationError("name", "Name is required")
    Call form.Announce("Validation failed", "Assertive")
End Sub
""";

var generatedAccessibility = new XPScriptTranspiler().Transpile(accessibilitySource, "uiform-accessibility-smoke.xps", "linux-x64");
foreach (var expected in new[]
{
    "public string AccessibleName",
    "public int TabIndex",
    "public string InitialFocus",
    "public void FocusFirstInvalid()",
    "public void SetValidationError(object? name, object? message)",
    "public string ValidationError",
    "aria-invalid",
    "aria-describedby",
    "aria-live"
})
{
    if (!generatedAccessibility.Contains(expected, StringComparison.Ordinal))
        throw new InvalidOperationException("Generated UIForm accessibility surface is missing: " + expected);
}

var schemaAccessibilitySource = string.Join('\n', new[]
{
    "Option Declare",
    "",
    "Sub Main()",
    "    Dim form As New UIForm(\"Schema accessibility\")",
    "    Dim data As XPJsonDocument",
    "    Dim schema As XPJsonSchema",
    "    Set data = XPJson.Parse(\"{\"\"customer\"\":{\"\"address\"\":{\"\"city\"\":\"\"\"\"}}}\")",
    "    Call form.BindData(data)",
    "    Call form.AddTextField(\"customer\", \"Customer\")",
    "    Set schema = XPJsonSchema.Parse(\"{\"\"type\"\": \"\"object\"\", \"\"properties\"\": {\"\"customer\"\": {\"\"type\"\": \"\"object\"\", \"\"properties\"\": {\"\"address\"\": {\"\"type\"\": \"\"object\"\", \"\"properties\"\": {\"\"city\"\": {\"\"minLength\"\": 2}}}}}}}}\")",
    "    Call form.SetValidationSchema(schema)",
    "End Sub"
});

var generatedSchemaAccessibility = new XPScriptTranspiler().Transpile(schemaAccessibilitySource, "uiform-schema-accessibility-smoke.xps", "linux-x64");
foreach (var expected in new[]
{
    "var validationError = field.ValidationError.Length > 0 ? field.ValidationError : GetValidationError(field.Name);",
    "BuildAccessibilityAttributes(field, name, validationError)",
    "if (validationError.Length > 0) describedBy.Add",
    "if (validationError.Length > 0) html.Append",
    "HtmlEncode(validationError)"
})
{
    if (!generatedSchemaAccessibility.Contains(expected, StringComparison.Ordinal))
        throw new InvalidOperationException("Generated server UIForm schema validation rendering is missing: " + expected);
}

Console.WriteLine("DESKTOP_UIFORM_SCHEMA_ACCESSIBILITY_TRANSPILE_OK");

var webViewSource = """
Option Declare

Sub Main()
    Dim form As New UIForm("WebView", 960, 720, True)
    Dim browser As Variant
    browser = form.AddWebView("browser", "Browser")
    browser.Source = "https://example.com/"
    browser.UserAgent = "XPscript-WebView-Smoke"
    Call form.Show(False)
End Sub
""";

var generatedWebView = new XPScriptTranspiler().Transpile(webViewSource, "uiform-webview-smoke.xps", "linux-x64");
foreach (var expected in new[]
{
    "public XPScriptUIField AddWebView(object? name)",
    "public string Source",
    "public string InvokeScript(object? script)",
    "public string GetCookies()",
    "XPScriptUIDesktopAdapter.WebViewCommand(_owner.InstanceId, Name, command, argument)",
    "WebViewSource",
    "WebViewUserAgent"
})
{
    if (!generatedWebView.Contains(expected, StringComparison.Ordinal))
        throw new InvalidOperationException("Generated UIForm WebView surface is missing: " + expected);
}

Console.WriteLine("DESKTOP_UIFORM_MODAL_CHILD_TRANSPILE_OK");
Console.WriteLine("DESKTOP_UIFORM_ACCESSIBILITY_TRANSPILE_OK");
Console.WriteLine("DESKTOP_UIFORM_WEBVIEW_TRANSPILE_OK");

async Task RunCompiler(params string[] arguments)
{
    var start = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = parityRoot,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    start.ArgumentList.Add(compilerPath);
    foreach (var argument in arguments)
        start.ArgumentList.Add(argument);
    if (arguments.Length > 0 && arguments[0].Equals("ast-compile", StringComparison.OrdinalIgnoreCase))
        start.ArgumentList.Add("--debug");
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start compiler.");
    var output = await process.StandardOutput.ReadToEndAsync() + await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0)
        throw new InvalidOperationException($"Desktop parity compiler invocation failed: {output}");
}

async Task ProbeDesktopRuntime(string label, string file, params string[] arguments)
{
    var launchFile = file;
    var launchArguments = arguments.ToList();
    if (File.Exists(file) && file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
    {
        launchFile = "dotnet";
        launchArguments.Insert(0, file);
    }
    if (Directory.Exists(file))
    {
        var appHost = Directory.EnumerateFiles(file)
            .FirstOrDefault(path => !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                                    !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                                    !path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase));
        var generatedAssembly = Path.Combine(file, "Generated.dll");
        if (!string.IsNullOrWhiteSpace(appHost) && OperatingSystem.IsWindows())
            launchFile = appHost;
        else if (File.Exists(generatedAssembly))
        {
            launchFile = "dotnet";
            launchArguments.Insert(0, generatedAssembly);
        }
    }

    var start = new ProcessStartInfo(launchFile)
    {
        WorkingDirectory = parityRoot,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    foreach (var argument in launchArguments)
        start.ArgumentList.Add(argument);
    using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {label} desktop runtime.");
    var outputTask = process.StandardOutput.ReadToEndAsync();
    var errorTask = process.StandardError.ReadToEndAsync();
    var waitTask = process.WaitForExitAsync();
    var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(5)));
    if (completed != waitTask && !process.HasExited)
    {
        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();
    }
    var output = await outputTask + await errorTask;
    if (process.ExitCode != 0 && completed == waitTask)
        throw new InvalidOperationException($"{label} desktop runtime exited unexpectedly: {output}");
}
