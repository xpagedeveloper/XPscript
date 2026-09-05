using System.Text.Json;
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
    "accessibleName = field.AccessibleName",
    "validationError = field.ValidationError",
    "aria-invalid",
    "aria-describedby",
    "aria-live"
})
{
    if (!generatedAccessibility.Contains(expected, StringComparison.Ordinal))
        throw new InvalidOperationException("Generated UIForm accessibility surface is missing: " + expected);
}

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
