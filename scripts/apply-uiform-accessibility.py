from pathlib import Path


def replace_once(path, old, new):
    p = Path(path)
    text = p.read_text()
    if old not in text:
        raise SystemExit(f"marker not found in {path}: {old[:160]!r}")
    p.write_text(text.replace(old, new, 1))

# Hook accessibility after all existing UIForm post-processing.
replace_once(
    "src/XPScript.Compiler/UIExtensionDesktopPostProcessor.cs",
    "        replaced = new UIFormWebWindowLifecyclePostProcessor().Transform(replaced);\n        return HardenWebBridgeLookup(replaced);",
    "        replaced = new UIFormWebWindowLifecyclePostProcessor().Transform(replaced);\n        replaced = new UIFormAccessibilityPostProcessor().Transform(replaced);\n        return HardenWebBridgeLookup(replaced);")

# Desktop request contract gains accessibility metadata and focus/validation policy.
replace_once(
    "src/XPScript.UI.Desktop/DesktopBridgeContracts.cs",
    "    public IReadOnlyList<string> Values { get; init; } = Array.Empty<string>();\n}",
    """    public IReadOnlyList<string> Values { get; init; } = Array.Empty<string>();
    public string AccessibleName { get; init; } = string.Empty;
    public string AccessibleDescription { get; init; } = string.Empty;
    public string AccessibleHelpText { get; init; } = string.Empty;
    public string AccessibleLive { get; init; } = "Off";
    public bool AccessibilityHidden { get; init; }
    public bool Focusable { get; init; } = true;
    public bool IsTabStop { get; init; } = true;
    public int? TabIndex { get; init; }
    public string AccessKey { get; init; } = string.Empty;
    public string HotKey { get; init; } = string.Empty;
    public string ValidationError { get; init; } = string.Empty;
}
""")
replace_once(
    "src/XPScript.UI.Desktop/DesktopBridgeContracts.cs",
    "    public string ApplicationIcon { get; init; } = string.Empty;\n}",
    """    public string ApplicationIcon { get; init; } = string.Empty;
    public string InstanceId { get; init; } = string.Empty;
    public string InitialFocus { get; init; } = string.Empty;
    public bool ValidationSummary { get; init; } = true;
    public bool FocusFirstError { get; init; } = true;
    public bool AnnounceValidationErrors { get; init; } = true;
    public string Announcement { get; init; } = string.Empty;
    public string AnnouncementPriority { get; init; } = "Polite";
}
""")

# Desktop modal host: apply automation metadata, validation errors and initial focus.
replace_once(
    "src/XPScript.UI.Desktop/DesktopFormHost.cs",
    "using Avalonia;\nusing Avalonia.Controls;",
    "using Avalonia;\nusing Avalonia.Automation;\nusing Avalonia.Controls;")
replace_once(
    "src/XPScript.UI.Desktop/DesktopFormHost.cs",
    "            ApplyEditorState(field, editor);\n            ApplyFieldHints(field, editor);",
    "            ApplyEditorState(field, editor);\n            ApplyFieldHints(field, editor);\n            DesktopAccessibilityHost.ApplyField(field, editor);")
replace_once(
    "src/XPScript.UI.Desktop/DesktopFormHost.cs",
    "            foreach (var errorText in fieldValidationTexts.Values)\n            {",
    "            foreach (var editor in editors.Values) DesktopAccessibilityHost.SetValidationError(editor, null);\n            foreach (var errorText in fieldValidationTexts.Values)\n            {")
replace_once(
    "src/XPScript.UI.Desktop/DesktopFormHost.cs",
    "                    var validationError = ValidateEditorValue(field, editor, allowedOptions);",
    "                    var validationError = field.ValidationError.Length > 0 ? field.ValidationError : ValidateEditorValue(field, editor, allowedOptions);")
replace_once(
    "src/XPScript.UI.Desktop/DesktopFormHost.cs",
    "                        errorText.IsVisible = true;\n                    }\n                    firstInvalidEditor ??= editor;",
    "                        errorText.IsVisible = true;\n                    }\n                    DesktopAccessibilityHost.SetValidationError(editor, validationError);\n                    firstInvalidEditor ??= editor;")
replace_once(
    "src/XPScript.UI.Desktop/DesktopFormHost.cs",
    "                if (firstInvalidEditor is not null)\n                {\n                    firstInvalidEditor.Focus();\n                    return;\n                }",
    """                if (firstInvalidEditor is not null)
                {
                    if (request.ValidationSummary)
                    {
                        var count = editors.Count(pair => DataValidationErrors.GetHasErrors(pair.Value));
                        validationText.Text = count == 1 ? "There is 1 validation error." : $"There are {count} validation errors.";
                        validationText.IsVisible = true;
                        if (request.AnnounceValidationErrors)
                            AutomationProperties.SetLiveSetting(validationText, AutomationLiveSetting.Assertive);
                    }
                    if (request.FocusFirstError) firstInvalidEditor.Focus();
                    return;
                }""")
replace_once(
    "src/XPScript.UI.Desktop/DesktopFormHost.cs",
    "        window.Closed += (_, _) => loop.Continue = false;\n        window.Show();",
    """        window.Closed += (_, _) => { DesktopAccessibilityHost.Unregister(request.InstanceId); loop.Continue = false; };
        window.Opened += (_, _) =>
        {
            DesktopAccessibilityHost.Register(request.InstanceId, window, editors);
            if (request.Announcement.Length > 0) DesktopAccessibilityHost.Announce(window, request.Announcement, request.AnnouncementPriority);
            if (request.InitialFocus.Length > 0 && editors.TryGetValue(request.InitialFocus, out var initial)) initial.Focus();
            else editors.Values.FirstOrDefault(editor => editor.IsVisible && editor.IsEnabled && editor.Focusable && editor.IsTabStop)?.Focus();
        };
        window.Show();""")

# Modeless host gets the same automation and focus metadata.
replace_once(
    "src/XPScript.UI.Desktop/DesktopFormLifecycleHost.cs",
    "using System.Text.Json;\nusing Avalonia.Controls;",
    "using System.Text.Json;\nusing Avalonia.Controls;")
replace_once(
    "src/XPScript.UI.Desktop/DesktopFormLifecycleHost.cs",
    "            var editor = CreateEditor(field, type, name, label);\n            editors[name] = editor;",
    "            var editor = CreateEditor(field, type, name, label);\n            DesktopAccessibilityHost.ApplyField(field, editor, label);\n            editors[name] = editor;")
replace_once(
    "src/XPScript.UI.Desktop/DesktopFormLifecycleHost.cs",
    "            Windows.TryRemove(instanceId, out _);\n            if (Windows.IsEmpty)",
    "            Windows.TryRemove(instanceId, out _);\n            DesktopAccessibilityHost.Unregister(instanceId);\n            if (Windows.IsEmpty)")
replace_once(
    "src/XPScript.UI.Desktop/DesktopFormLifecycleHost.cs",
    "        window.Show();\n        DesktopApplicationHost.SetProcessKeepAlive(true);",
    """        window.Opened += (_, _) =>
        {
            DesktopAccessibilityHost.Register(instanceId, window, editors);
            var initialFocus = Read(root, "initialFocus", string.Empty);
            if (initialFocus.Length > 0 && editors.TryGetValue(initialFocus, out var initial)) initial.Focus();
            else editors.Values.FirstOrDefault(editor => editor.IsVisible && editor.IsEnabled && editor.Focusable && editor.IsTabStop)?.Focus();
            var announcement = Read(root, "announcement", string.Empty);
            if (announcement.Length > 0) DesktopAccessibilityHost.Announce(window, announcement, Read(root, "announcementPriority", "Polite"));
        };
        window.Show();
        DesktopApplicationHost.SetProcessKeepAlive(true);""")

# Extend desktop lifecycle smoke with generated accessibility contract checks.
replace_once(
    "tests/DesktopUIFormLifecycleSmoke/Program.cs",
    'Console.WriteLine("DESKTOP_UIFORM_MODAL_CHILD_TRANSPILE_OK");',
    '''var accessibilitySource = """
Option Declare

Sub Main()
    Dim form As New UIForm("Accessible customer")
    Dim name As Object
    Set name = form.AddTextField("name", "Name")
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

Console.WriteLine("DESKTOP_UIFORM_MODAL_CHILD_TRANSPILE_OK");
Console.WriteLine("DESKTOP_UIFORM_ACCESSIBILITY_TRANSPILE_OK");''')

# Documentation entry.
with Path("docs/uiform-accessibility.md").open("w") as f:
    f.write("""# UIForm accessibility\n\nUIForm accessibility is enabled by default for desktop and browser rendering.\n\nField properties: `AccessibleName`, `AccessibleDescription`, `AccessibleHelpText`, `AccessibleLive`, `AccessibilityHidden`, `Focusable`, `IsTabStop`, `TabIndex`, `HasFocus`, `AccessKey`, `HotKey`.\n\nField function: `Focus()`.\n\nForm API: `InitialFocus`, `FocusedField`, `Focus(name)`, `FocusFirst()`, `FocusFirstInvalid()`, `FocusNext()`, `FocusPrevious()`, `ValidationErrors`, `HasValidationErrors`, `SetValidationError`, `ClearValidationError`, `GetValidationErrors`, `ValidationSummary`, `FocusFirstError`, `AnnounceValidationErrors`, and `Announce(message[, priority])`.\n\nDesktop maps metadata to Avalonia Automation properties, native focus/tab navigation and `DataValidationErrors`. Browser rendering emits native labels, `aria-invalid`, `aria-describedby`, `aria-live`, tabindex and autofocus metadata.\n\nBuilt-in labels remain the accessible-name default. Use `AccessibleName` only when the visible label is insufficient.\n""")
