using System.Reflection;
using XPScript.Compiler;

foreach (var runtime in new[] { "android-arm64", "android-x64" })
    if (!CompilerDriver.SupportedRuntimes.Contains(runtime, StringComparer.OrdinalIgnoreCase))
        throw new Exception(runtime + " is not a supported compiler target.");

var type = typeof(CompilerDriver);
var method = type.GetMethod("BuildGeneratedProject", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new Exception("BuildGeneratedProject was not found.");
var stagedType = type.GetNestedType("StagedManagedReference", BindingFlags.NonPublic)
    ?? throw new Exception("StagedManagedReference was not found.");
var emptyReferences = Array.CreateInstance(stagedType, 0);
var project = (string)(method.Invoke(null, new object?[] { "android-arm64", false, emptyReferences, false, false, "AndroidSmoke" })
    ?? throw new Exception("Android project generation returned null."));

if (!project.Contains("<NoWarn>CA1416;$(NoWarn)</NoWarn>", StringComparison.Ordinal))
    throw new Exception("Generated projects must suppress CA1416 for the built-in cross-platform runtime compatibility layer.");

foreach (var expected in new[] { "<TargetFramework>net10.0-android</TargetFramework>", "<SupportedOSPlatformVersion>30.0</SupportedOSPlatformVersion>", "<RuntimeIdentifier>android-arm64</RuntimeIdentifier>", "<AndroidPackageFormat>apk</AndroidPackageFormat>", "<ApplicationId>com.xpscript.debugapp</ApplicationId>" })
    if (!project.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android generated project is missing: " + expected);


var addAndroidUiDependencies = type.GetMethod("AddAndroidUIFormDependencies", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new Exception("AddAndroidUIFormDependencies was not found.");
var uiProject = (string)(addAndroidUiDependencies.Invoke(null, new object?[] { project })
    ?? throw new Exception("Android UIForm project dependency generation returned null."));
if (!uiProject.Contains("<CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>", StringComparison.Ordinal))
    throw new Exception("Android UIForm generated project must disable CopyLocalLockFileAssemblies to avoid duplicate AndroidX bindings.");
if (uiProject.Contains("<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>", StringComparison.Ordinal))
    throw new Exception("Android UIForm generated project still enables CopyLocalLockFileAssemblies.");
if (!uiProject.Contains("<PackageReference Include=\"Avalonia\" Version=\"12.0.3\" />", StringComparison.Ordinal))
    throw new Exception("Android UIForm generated project must match the working Avalonia Android dependency graph.");

if (uiProject.Contains("<SelfContained>", StringComparison.Ordinal))
    throw new Exception("Android UIForm generated project must not override Avalonia Android SelfContained behavior.");
if (uiProject.Contains("<UseAppHost>", StringComparison.Ordinal))
    throw new Exception("Android UIForm generated project must not enable desktop app-host generation.");

foreach (var expected in new[]
{
    "<PackageReference Include=\"Avalonia.Android\" Version=\"12.0.3\" />",
    "<PackageReference Include=\"Avalonia.Themes.Fluent\" Version=\"12.0.3\" />"
})
{
    if (!uiProject.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android UIForm generated project is missing: " + expected);
}

var androidHostType = type.Assembly.GetType("XPScript.Compiler.AndroidHostSource", throwOnError: true)!;
var androidHostCode = (string)(androidHostType.GetField("Code", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetRawConstantValue()
    ?? throw new Exception("AndroidHostSource.Code was not found."));
foreach (var expected in new[] { "Intent?.GetBooleanExtra(\"xpscript.appdebug\", false)", "System.Environment.SetEnvironmentVariable(\"XPSCRIPT_APPDEBUG\", \"1\")" })
    if (!androidHostCode.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android headless Application.Debug launch routing is missing: " + expected);

var uiHostType = type.Assembly.GetType("XPScript.Compiler.AndroidUIHostSource", throwOnError: true)!;
var uiHostCode = (string)(uiHostType.GetField("Code", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetRawConstantValue()
    ?? throw new Exception("AndroidUIHostSource.Code was not found."));
foreach (var expected in new[]
{
    "Intent?.GetBooleanExtra(\"xpscript.appdebug\", false)",
    "System.Environment.SetEnvironmentVariable(\"XPSCRIPT_APPDEBUG\", \"1\")",
    "public sealed class App : Avalonia.Application",
    "public const bool Enabled = __XPSCRIPT_ANDROID_DEBUG__;",
    "XPSCRIPT-DEBUG-DUMP=",
    "if (AndroidDebugMode.Enabled)",
    "public class AndroidApp : AvaloniaAndroidApplication<App>",
    "global::Android.Runtime.JniHandleOwnership",
    "new Avalonia.Controls.CheckBox()",
    "Avalonia.Layout.Orientation.Horizontal",
    "new Avalonia.Controls.Button",
    "var labelBlock = new TextBlock { Text = label };",
    "fieldContainer.Children.Add(labelBlock);",
    "Control editor = type switch",
    "actions.Children.Add(ok);",
    "request.TryGetProperty(\"buttons\"",
    "var actionState = eventCallback(\"button:\" + buttonName, submittedValues);",
    "ApplyActionState(actionState, editors, validationErrors, fieldContainers, fieldLabels, actionButtons, tabControl);",
    "private static bool ApplyActionState",
    "var validationErrors = new Dictionary<string, TextBlock>",
    "validationErrors[name] = validationBlock;",
    "Text = string.Empty,",
    "IsVisible = false,",
    "Foreground = Brushes.Red",
    "var bootText = request.TryGetProperty(\"bootText\"",
    "var bootImage = request.TryGetProperty(\"bootImage\"",
    "private static Control HomeContent() => new Grid();",
    "CornerRadius = new CornerRadius(cornerRadius)",
    "CornerRadius = new CornerRadius(fieldCornerRadius)",
    "\"Select\" => new ComboBox",
    "\"MultiListBox\" => new ListBox",
    "\"RadioGroup\" => CreateRadioGroup(options)",
    "\"RangeField\" => CreateRangeField(field)",
    "\"Separator\" => new Separator()",
    "\"Image\" => CreateImage(field)",
    "\"DateField\" => new DatePicker()",
    "\"TimeField\" => new TimePicker()",
    "\"DateTimeField\" => new AndroidDateTimeFieldEditor()",
    "\"MonthField\" => new AndroidMonthFieldEditor()",
    "\"ColorField\" => new AndroidColorFieldEditor()",
    "private sealed class AndroidDateTimeFieldEditor",
    "private sealed class AndroidMonthFieldEditor",
    "private sealed class AndroidColorFieldEditor",
    "SetTemporalEditorValue(editor, text)",
    "GetTemporalEditorValue(editor)",
    "var tabPanels = new Dictionary<string, StackPanel>",
    "tabControl = new TabControl",
    "root.TryGetProperty(\"activeTab\"",
    "var namedGrids = new Dictionary<string, Grid>",
    "AddFieldContainer(field, fieldContainer, targetPanel, targetGrid)",
    "validationBlock.IsVisible = validationError.Length > 0;",
    "if (validationError.Length == 0)",
    "field.TryGetProperty(\"values\"",
    "field.TryGetProperty(\"value\"",
    "UIForm button '\" + buttonName + \"' callback failed:",
    "catch (Exception exception)",
    "actions.Children.Add(actionButton);",
    "field.TryGetProperty(\"validationError\"",
    "validationErrors.TryGetValue(name, out var validationBlock)",
    "fieldContainer.Children.Add(validationBlock);",
    "request.TryGetProperty(\"initialFocus\"",
    "initialEditor.Focus();",
    "editor.IsVisible && editor.IsEnabled && editor.Focusable && editor.IsTabStop)?.Focus();",
    "panel.Children.Add(actions);",
    "Theme = \"@style/Theme.AppCompat.DayNight.NoActionBar\"",
    "Label = \"XPScript\"",
    "MainActivity.Current.Title = formTitle;",
    "request.TryGetProperty(\"title\"",
    "ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode",
    "new StackPanel { Spacing = 12, Margin = new Thickness(16), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Stretch }",
    "var actions = new WrapPanel",
    "protected override void OnStart()",
    "protected override void OnResume()",
    "protected override void OnPause()",
    "protected override void OnStop()",
    "XPSCRIPT-LIFECYCLE=start",
    "XPSCRIPT-LIFECYCLE=resume",
    "XPSCRIPT-LIFECYCLE=pause",
    "XPSCRIPT-LIFECYCLE=stop",
    "Interlocked.Exchange(ref _runtimeStarted, 1)",
    "AvaloniaMainActivity",
    "AvaloniaAndroidApplication<App>",
    "AndroidFormHost",
    "MainView.Current",
    "Program.Main(Array.Empty<string>())",
    "Console.AndroidLog",
    "XPSCRIPT-EXIT=0",
    "XPSCRIPT-EXIT=1"
})
{
    if (!uiHostCode.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android generated UIForm host is missing: " + expected);
}

if (System.Text.RegularExpressions.Regex.IsMatch(uiHostCode, @"(?<!Avalonia\.Controls\.)\bRadioButton\b"))
    throw new Exception("Android UIForm host must fully qualify Avalonia RadioButton references to avoid Android.Widget ambiguity.");

var structuralBranchIndex = uiHostCode.IndexOf("if (type is \"Separator\" or \"Spacer\" or \"Image\")", StringComparison.Ordinal);
var editorRegistrationIndex = uiHostCode.IndexOf("editors[name] = editor;", StringComparison.Ordinal);
if (structuralBranchIndex < 0 || editorRegistrationIndex < 0 || editorRegistrationIndex < structuralBranchIndex)
    throw new Exception("Android structural/media controls must bypass editor-state registration so they cannot overwrite bound data during submission.");

var defaultOkStart = uiHostCode.IndexOf("ok.Click += (_, _) =>", StringComparison.Ordinal);
var defaultOkEnd = defaultOkStart >= 0 ? uiHostCode.IndexOf("};", defaultOkStart, StringComparison.Ordinal) : -1;
var defaultOkBlock = defaultOkStart >= 0 && defaultOkEnd > defaultOkStart ? uiHostCode[defaultOkStart..defaultOkEnd] : string.Empty;
if (!defaultOkBlock.Contains("ApplyActionState(", StringComparison.Ordinal) ||
    !defaultOkBlock.Contains("return;", StringComparison.Ordinal))
    throw new Exception("Android default OK must apply callback action-state and keep the form open when validation fails.");

foreach (var expected in new[]
{
    "var fieldContainers = new Dictionary<string, Control>",
    "var fieldLabels = new Dictionary<string, TextBlock>",
    "var actionButtons = new Dictionary<string, Avalonia.Controls.Button>",
    "fieldContainer.IsVisible =",
    "ApplyEditorReadOnly(editor,",
    "ApplyEditorOptions(editor,",
    "fieldLabels.TryGetValue(name, out var labelBlock)",
    "fieldContainers.TryGetValue(name, out var fieldContainer)",
    "actionButtons.TryGetValue(buttonName, out var actionButton)",
    "button.TryGetProperty(\"label\"",
    "button.TryGetProperty(\"visible\"",
    "button.TryGetProperty(\"enabled\""
})
{
    if (!uiHostCode.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android dynamic UIForm action-state parity is missing: " + expected);
}

foreach (var forbidden in new[] { "PointerPressed", "PointerReleased", "MouseButton", "MouseDevice" })
{
    if (uiHostCode.Contains(forbidden, StringComparison.Ordinal))
        throw new Exception("Android UIForm host must leave touch/pointer translation to Avalonia Android instead of desktop-specific input handling: " + forbidden);
}

var appDebugSourcePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "XPScript.Compiler", "ApplicationDebugRuntimeSource.cs");
var appDebugSource = File.ReadAllText(appDebugSourcePath);
foreach (var expected in new[] { "Android.Util.Log, Mono.Android", "\"XPScript\"", "\"ERROR\" => \"Error\"", "\"WARN\" => \"Warn\"", "_ => \"Info\"" })
{
    if (!appDebugSource.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Application.Debug Android logcat routing is missing: " + expected);
}

var desktopRuntimeSourcePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "XPScript.Compiler", "UIExtensionDesktopRuntimeSource.cs");
var desktopRuntimeSource = File.ReadAllText(desktopRuntimeSourcePath);
foreach (var expected in new[]
{
    "theme = form.Theme",
    "showValidationErrors = form.ShowValidationErrors",
    "gridColumns = form.GridColumns",
    "placeholder = field.Placeholder",
    "schemaValidationError = form.GetValidationError(field.Name)",
    "layoutRow = button.LayoutRow",
    "bootText = form.BootText",
    "tabs = form.Tabs.Select",
    "grids = form.Grids.Select",
    "activeTab = form.ActiveTab"
})
{
    if (!desktopRuntimeSource.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Base UIForm desktop request metadata is missing: " + expected);
}

if (!desktopRuntimeSource.Contains("buttons = form.Buttons.Select", StringComparison.Ordinal))
    throw new Exception("Android UIForm requests must include the shared UIForm button model.");

var eventDispatcherSourcePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "XPScript.Compiler", "UIFormEventDispatcherPostProcessor.cs");
var eventDispatcherSource = File.ReadAllText(eventDispatcherSourcePath);
foreach (var expected in new[]
{
    "ApplySubmittedStateJson(submittedValue);",
    "catch (XPScriptRuntimeException)",
    "return SerializeActionState();",
    "if (!IsDataValid)",
    "field.ValidationError = exception.Message;",
    "ApplySubmittedValue(field, submitted);",
    "ApplySubmittedValues(field, submittedValues);"
})
{
    if (!eventDispatcherSource.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android UIForm submitted data is not routed through the shared bound-data update path: " + expected);
}

var emulatorProject = (string)(method.Invoke(null, new object?[] { "android-x64", false, emptyReferences, false, false, "AndroidEmulatorSmoke" })
    ?? throw new Exception("Android emulator project generation returned null."));
if (!emulatorProject.Contains("<TargetFramework>net10.0-android</TargetFramework>", StringComparison.Ordinal) ||
    !emulatorProject.Contains("<RuntimeIdentifier>android-x64</RuntimeIdentifier>", StringComparison.Ordinal))
    throw new Exception("Android x64 emulator project generation is incorrect.");

if (project.Contains("<AndroidSupportedAbis>", StringComparison.Ordinal))
    throw new Exception("Android generated project still uses obsolete AndroidSupportedAbis.");
if (project.Contains("<StartupObject>", StringComparison.Ordinal))
    throw new Exception("Android generated project must not specify StartupObject.");

var hostType = type.Assembly.GetType("XPScript.Compiler.AndroidHostSource", throwOnError: true)!;
var code = (string)(hostType.GetField("Code", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetRawConstantValue()
    ?? throw new Exception("AndroidHostSource.Code was not found."));
var findPublishedExecutable = type.GetMethod("FindPublishedExecutable", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new Exception("FindPublishedExecutable was not found.");
var apkRoot = Path.Combine(Path.GetTempPath(), "XPScript-AndroidCompilerProbe-" + Guid.NewGuid().ToString("N"));
var publishDir = Path.Combine(apkRoot, "publish");
var androidOutputDir = Path.Combine(apkRoot, "bin", "Release", "net10.0-android", "android-arm64");
Directory.CreateDirectory(publishDir);
Directory.CreateDirectory(androidOutputDir);
var expectedApk = Path.Combine(androidOutputDir, "AndroidSmoke-Signed.apk");
File.WriteAllText(expectedApk, "probe");
try
{
    var foundApk = (string?)findPublishedExecutable.Invoke(null, new object?[] { apkRoot, publishDir, "android-arm64", "AndroidSmoke" });
    if (!string.Equals(foundApk, expectedApk, StringComparison.OrdinalIgnoreCase))
        throw new Exception("Android APK discovery did not search the complete build tree.");
}
finally
{
    Directory.Delete(apkRoot, recursive: true);
}

foreach (var expected in new[] { "AndroidEntryActivity", "MainLauncher = true", "Console.AndroidLog", "\"XPScript\"", "Log.Error", "Program.Main(Array.Empty<string>())", "XPSCRIPT-EXIT=0", "XPSCRIPT-EXIT=1" })
    if (!code.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android host is missing: " + expected);

var findPublished = type.GetMethod("FindPublishedExecutable", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new Exception("FindPublishedExecutable was not found.");
var artifactRoot = Path.Combine(Path.GetTempPath(), "xpscript-android-probe-" + Guid.NewGuid().ToString("N"));
var artifactBin = Path.Combine(artifactRoot, "bin");
var artifactPublish = Path.Combine(artifactRoot, "publish");
Directory.CreateDirectory(artifactBin);
Directory.CreateDirectory(artifactPublish);
try
{
    var signedPackage = Path.Combine(artifactPublish, "com.xpscript.debugapp-Signed.apk");
    File.WriteAllText(signedPackage, "probe");
    File.WriteAllText(Path.Combine(artifactPublish, "com.xpscript.debugapp.apk"), "probe");
    File.WriteAllText(Path.Combine(artifactBin, "com.xpscript.debugapp-Signed.apk"), "probe");
    File.WriteAllText(Path.Combine(artifactBin, "other-Signed.apk"), "probe");
    var discovered = (string?)findPublished.Invoke(null, new object?[] { artifactRoot, artifactPublish, "android-arm64", "android-debug-print" });
    if (!string.Equals(discovered, signedPackage, StringComparison.OrdinalIgnoreCase))
        throw new Exception("Android APK discovery did not prefer the signed package from the publish directory.");
}
finally
{
    Directory.Delete(artifactRoot, recursive: true);
}

var compilerProjectSource = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "XPScript.Compiler", "XPScript.Compiler.csproj"));
if (compilerProjectSource.Contains("PackageReference Include=\"Avalonia", StringComparison.Ordinal) ||
    compilerProjectSource.Contains("ProjectReference Include=\"../XPScript.UI.Android", StringComparison.Ordinal))
    throw new Exception("Non-UI XPScript compiler/runtime must remain independent from Avalonia Android.");

var compilerSource = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "XPScript.Compiler", "CompilerDriver.cs"));
if (!compilerSource.Contains("outputPath += \".apk\";", StringComparison.Ordinal))
    throw new Exception("Android compiler output is not normalized to an .apk path.");

var buildEnvironmentSource = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "XPScript.Compiler", "CompilerBuildEnvironment.cs"));
if (!buildEnvironmentSource.Contains("&& !usesAndroidUi", StringComparison.Ordinal))
    throw new Exception("Android UIForm builds must not inherit desktop Avalonia package references.");
if (!buildEnvironmentSource.Contains("var writeDirectoryProps = !usesAndroidUi;", StringComparison.Ordinal))
    throw new Exception("Android UIForm builds must not duplicate generated project metadata through Directory.Build.props.");
if (!buildEnvironmentSource.Contains("var isolateNuGetPackages = !IsAndroidUiFormPublish(startInfo, root);", StringComparison.Ordinal))
    throw new Exception("Android UIForm publish must use the standard NuGet package cache used by the known-good direct Avalonia Android publish.");


foreach (var expected in new[]
{
    "System.Environment.ExitCode = 0;",
    "if (System.Environment.ExitCode == 0)",
    "\"XPSCRIPT-EXIT=\" + System.Environment.ExitCode"
})
{
    if (!code.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android host exit-code regression is missing: " + expected);
}

var runtimeDiagnosticsSourcePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "XPScript.Compiler", "AdvancedXPScriptTranspiler.cs");
var runtimeDiagnosticsSource = File.ReadAllText(runtimeDiagnosticsSourcePath);
if (!runtimeDiagnosticsSource.Contains("#if ANDROID", StringComparison.Ordinal) ||
    !runtimeDiagnosticsSource.Contains("Console.WriteLine(\"at \" + runtimeSource + \":", StringComparison.Ordinal) ||
    !runtimeDiagnosticsSource.Contains("Environment.ExitCode = 1;", StringComparison.Ordinal))
    throw new Exception("Android runtime diagnostics regression is missing.");

var uiFormRegressionSample = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "android-uiform-regression.xps"));
foreach (var expected in new[] { "UIForm(\"Android UIForm Regression\")", "AddTextField", "AddTextArea", "AddCheckBox", "AddTab(\"details\"", "AddGrid(\"detailsGrid\", 2)", "detailsGrid.SetTab(\"details\")", "detailsGrid.SetFieldPosition(\"email\", 2)", "Application.Debug.Info" })
{
    if (!uiFormRegressionSample.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android UIForm regression sample is incomplete: " + expected);
}

var layoutRuntimeSource = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "XPScript.Compiler", "UIFormLayoutReactivePostProcessor.cs"));
foreach (var expected in new[]
{
    "UIForm grid name is invalid.",
    "already exists.",
    "UIForm grid column count must be between 1 and 64.",
    "UIForm tab '{tab}' does not exist.",
    "UIForm grid '{name}' does not exist."
})
{
    if (!layoutRuntimeSource.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Named-grid validation regression is missing: " + expected);
}

var manualUiFormSample = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "android-uiform-manual-test.xps"));
foreach (var expected in new[]
{
    "Android UIForm Manual Test",
    "TEST STARTUP: PASS",
    "Verify Input",
    "TEST BUTTON HANDLER: PASS",
    "TEST TOUCH: PASS",
    "TEST LAYOUT/SCROLL: PASS",
    "TEST FOCUS/KEYBOARD: PASS",
    "TEST VALIDATION: PASS",
    "form.BootText = \"Loading Android UIForm manual test\"",
    "form.BootImage = \"Android/test-image.png\"",
    "AddTab(\"basic\", \"Basic\")",
    "AddTab(\"more\", \"More\")",
    "AddGrid(\"detailsGrid\", 2)",
    "detailsGrid.SetFieldPosition(\"numberValue\", 2)",
    "AddImage(\"testImage\", \"Android/test-image.png\"",
    "SetFieldCornerRadius(\"name\", 12)",
    "SetButtonCornerRadius(\"verify\", 12)",
    "XPSCRIPT-LIFECYCLE"
})
{
    if (!manualUiFormSample.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android manual UIForm verification sample is incomplete: " + expected);
}

foreach (var callbackHeader in new[]
{
    "Sub VerifyInput(evt As Variant, contextName As String)",
    "Sub TouchPass(evt As Variant, contextName As String)",
    "Sub LayoutPass(evt As Variant, contextName As String)",
    "Sub KeyboardPass(evt As Variant, contextName As String)"
})
{
    if (!manualUiFormSample.Contains(callbackHeader, StringComparison.Ordinal))
        throw new Exception("Android UIForm callback regression must exercise default parameter passing without explicit ByVal: " + callbackHeader);
}

if (manualUiFormSample.Contains("ByVal evt As Variant", StringComparison.Ordinal) ||
    manualUiFormSample.Contains("ByVal contextName As String", StringComparison.Ordinal))
    throw new Exception("Android UIForm manual callback regression still uses the explicit ByVal workaround.");

var outputRegressionSample = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "android-debug-output-regression.xps"));
foreach (var expected in new[]
{
    "Print \"ANDROID-LINE=first\"",
    "Print \"ANDROID-VALUE=\" & 42",
    "Print \"ANDROID-BOOL=\" & True",
    "Print \"ANDROID-LINE=last\""
})
{
    if (!outputRegressionSample.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android multi-line output regression sample is incomplete: " + expected);
}

Console.WriteLine("ANDROID-COMPILER-PROBE=OK");
