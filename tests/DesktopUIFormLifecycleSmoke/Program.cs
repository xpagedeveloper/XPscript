using System.Text.Json;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XPScript.Compiler;
using XPScript.UI.Desktop;

if (args.Contains("--headless-host-probe", StringComparer.Ordinal))
{
    await RunHeadlessHostProbe();
    return;
}
if (args.Contains("--headless-ast-generated", StringComparer.Ordinal))
{
    await RunHeadlessAstGeneratedProbe();
    return;
}
if (args.Contains("--headless-uilistview", StringComparer.Ordinal))
{
    await RunHeadlessUIListViewProbe();
    return;
}

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

var desktopParitySource = await File.ReadAllTextAsync(Path.Combine(
    Directory.GetCurrentDirectory(),
    "tests/DesktopUIFormLifecycleSmoke/desktop-interaction-parity.xps"));
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
if (!Directory.Exists(astOutput) || !Directory.EnumerateFileSystemEntries(astOutput).Any())
    throw new InvalidOperationException("AST desktop fixture did not produce output.");
var astAssembly = Path.Combine(astOutput, "Generated.dll");
if (!File.Exists(astAssembly))
    throw new InvalidOperationException("Desktop parity AST output is missing Generated.dll.");
Console.WriteLine("DESKTOP_PARITY_AST_RUNTIME_START");
await ProbeDesktopRuntime("AST", "dotnet", astAssembly);
Console.WriteLine("DESKTOP_PARITY_AST_RUNTIME_OK");
Console.WriteLine("DESKTOP_AST_RUNTIME_LAUNCH_OK");

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

async Task RunHeadlessHostProbe()
{
    DesktopApplicationHost.TestConfigurator = builder =>
        builder.UseHeadless(new AvaloniaHeadlessPlatformOptions());

    const string instanceId = "headless-uiform-probe";
    var request = "{\"instanceId\":\"" + instanceId + "\",\"title\":\"Headless probe\",\"fields\":[{\"name\":\"name\",\"label\":\"Name\",\"type\":\"TextField\"}]}";
    DesktopFormLifecycleHost.Show(request);
    Dispatcher.UIThread.Invoke(() =>
    {
        var window = DesktopFormLifecycleHost.GetWindowForTests(instanceId)
            ?? throw new InvalidOperationException("Headless UIForm window was not created.");
        var text = window.GetVisualDescendants().OfType<TextBox>().Single();
        var close = window.GetVisualDescendants().OfType<Button>().Single();
        text.Focus();
        window.KeyTextInput("Alice");
        if (!string.Equals(text.Text, "Alice", StringComparison.Ordinal))
            throw new InvalidOperationException("Headless UIForm text input failed.");
        close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    });
    if (DesktopFormLifecycleHost.IsVisible(instanceId))
        throw new InvalidOperationException("Headless UIForm close action failed.");
    Console.WriteLine("HEADLESS_UIFORM_TEXT_OK");
    Console.WriteLine("HEADLESS_UIFORM_CLOSE_OK");
    await Task.CompletedTask;
}

async Task RunHeadlessAstGeneratedProbe()
{
    DesktopApplicationHost.TestConfigurator = builder =>
        builder.UseHeadless(new AvaloniaHeadlessPlatformOptions());

    var root = Path.Combine(Path.GetTempPath(), "xpscript-headless-ast-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var sourcePath = Path.Combine(root, "headless-ast.xps");
    await File.WriteAllTextAsync(sourcePath, "Option Declare\n\nSub Main()\n    Dim form As New UIForm(\"AST headless\", 480, 320, True)\n    Call form.AddTextField(\"name\", \"Name\")\n    Call form.AddButton(\"close\", \"Close\", \"CloseForm\")\n    Call form.Show(False)\nEnd Sub\n\nSub CloseForm()\nEnd Sub\n");
    var compilerPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll"));
    var output = Path.Combine(root, "ast");
    var compiler = Process.Start(new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = root,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        ArgumentList = { compilerPath, "ast-compile", sourcePath, "-o", output, "--single-file=false", "--runtime=false", "--debug" }
    }) ?? throw new InvalidOperationException("Could not start AST compiler.");
    var compilerStdout = compiler.StandardOutput.ReadToEndAsync();
    var compilerStderr = compiler.StandardError.ReadToEndAsync();
    await compiler.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));
    await Task.WhenAll(compilerStdout, compilerStderr);
    var compilerOutput = await compilerStdout + await compilerStderr;
    if (compiler.ExitCode != 0) throw new InvalidOperationException("AST headless fixture compilation failed: " + compilerOutput);

    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(output, "Generated.dll"));
    var entryPoint = assembly.GetType("Program", throwOnError: true)!.GetMethod("Main", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("AST generated Program.Main was not found.");
    var entryTask = Task.Run(() => entryPoint.Invoke(null, entryPoint.GetParameters().Length == 0 ? null : new object?[] { Array.Empty<string>() }));
    Window? window = null;
    for (var attempt = 0; attempt < 100 && window is null; attempt++)
    {
        await Task.Delay(50);
        window = DesktopFormLifecycleHost.GetAnyWindowForTests();
    }
    if (window is null)
    {
        DesktopApplicationHost.SetProcessKeepAlive(false);
        throw new InvalidOperationException("AST generated headless window was not created.");
    }
    Console.WriteLine("HEADLESS_AST_GENERATED_WINDOW_OK");
    var astWindowVisibleAfterClick = Dispatcher.UIThread.Invoke(() =>
    {
        var text = window.GetVisualDescendants().OfType<TextBox>().Single();
        var close = window.GetVisualDescendants().OfType<Button>().Single();
        text.Focus();
        window.KeyTextInput("Alice");
        if (!string.Equals(text.Text, "Alice", StringComparison.Ordinal))
            throw new InvalidOperationException("AST generated UIForm text input failed.");
        close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        return window.IsVisible;
    });
    if (astWindowVisibleAfterClick)
        throw new InvalidOperationException("AST generated UIForm close action failed.");
    Console.WriteLine("HEADLESS_AST_GENERATED_TEXT_OK");
    Console.WriteLine("HEADLESS_AST_GENERATED_CLOSE_OK");
    try
    {
        await entryTask.WaitAsync(TimeSpan.FromSeconds(5));
    }
    finally
    {
        DesktopApplicationHost.SetProcessKeepAlive(false);
    }
    Console.WriteLine("HEADLESS_AST_GENERATED_OK");
}

async Task RunHeadlessUIListViewProbe()
{
    DesktopApplicationHost.TestConfigurator = builder =>
        builder.UseHeadless(new AvaloniaHeadlessPlatformOptions());

    var callbackCount = 0;
    var callbackContext = string.Empty;
    var request = JsonSerializer.Serialize(new
    {
        title = "Headless list probe",
        selectedIndex = -1,
        sortable = false,
        filterEnabled = false,
        hasRowAction = false,
        hasOnSelect = true,
        hasOnDoubleClick = false,
        rowActions = Array.Empty<object>(),
        columns = new[]
        {
            new { name = "id", label = "ID", width = 120 },
            new { name = "name", label = "Name", width = 180 }
        },
        rows = new[]
        {
            new { index = 0, values = new Dictionary<string, string> { ["id"] = "1001", ["name"] = "Kalle" } },
            new { index = 1, values = new Dictionary<string, string> { ["id"] = "1002", ["name"] = "Anna" } }
        }
    });

    var liveState = JsonSerializer.Serialize(new
    {
        selectedIndex = 1,
        sortable = false,
        filterEnabled = false,
        columns = new[]
        {
            new { name = "id", label = "Selected ID", width = 120 },
            new { name = "name", label = "Name", width = 180 }
        },
        rows = new[]
        {
            new { index = 0, href = (string?)null, values = new[] { "1001", "Kalle" }, actions = (object?)null },
            new { index = 1, href = (string?)null, values = new[] { "1002", "Anna" }, actions = (object?)null }
        }
    });

    DesktopApplicationHost.EnsureStarted();
    var dialogTask = Task.Run(() => DesktopListViewHost.ShowDialog(request, (eventType, context) =>
    {
        callbackCount++;
        callbackContext = eventType + ":" + context;
        return liveState;
    }));

    Window? window = null;
    for (var attempt = 0; attempt < 100 && window is null; attempt++)
    {
        await Task.Delay(50);
        window = DesktopListViewHost.GetWindowForTests();
    }
    if (window is null)
        throw new InvalidOperationException("Headless UIListView window was not created.");

    Console.WriteLine("HEADLESS_UILISTVIEW_WINDOW_OK");
    Dispatcher.UIThread.Invoke(() =>
    {
        var list = window.GetVisualDescendants().OfType<ListBox>().Single();
        list.SelectedIndex = 1;
        if (list.SelectedItem is null)
            throw new InvalidOperationException("Headless UIListView row selection failed.");
        var headers = window.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToArray();
        if (!headers.Contains("Selected ID", StringComparer.Ordinal))
            throw new InvalidOperationException("Headless UIListView live update failed.");
        var ok = window.GetVisualDescendants().OfType<Button>().Single(button => string.Equals(button.Content?.ToString(), "OK", StringComparison.Ordinal));
        ok.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    });

    var result = await dialogTask.WaitAsync(TimeSpan.FromSeconds(5));
    if (callbackCount != 1 || callbackContext != "select:1")
        throw new InvalidOperationException($"Headless UIListView callback failed: {callbackContext} ({callbackCount}).");
    if (!result.Contains("\"Result\":\"OK\"", StringComparison.OrdinalIgnoreCase) ||
        !result.Contains("\"SelectedIndex\":1", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Headless UIListView result did not preserve selection.");

    DesktopApplicationHost.SetProcessKeepAlive(false);
    Console.WriteLine("HEADLESS_UILISTVIEW_SELECT_OK");
    Console.WriteLine("HEADLESS_UILISTVIEW_OK");
}
