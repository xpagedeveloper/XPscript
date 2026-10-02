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

var uiHostType = type.Assembly.GetType("XPScript.Compiler.AndroidUIHostSource", throwOnError: true)!;
var uiHostCode = (string)(uiHostType.GetField("Code", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetRawConstantValue()
    ?? throw new Exception("AndroidUIHostSource.Code was not found."));
foreach (var expected in new[]
{
    "public sealed class App : Avalonia.Application",
    "public class AndroidApp : AvaloniaAndroidApplication<App>",
    "global::Android.Runtime.JniHandleOwnership",
    "new Avalonia.Controls.CheckBox()",
    "Avalonia.Layout.Orientation.Horizontal",
    "new Avalonia.Controls.Button",
    "panel.Children.Add(new TextBlock { Text = label });",
    "Control editor = type switch",
    "actions.Children.Add(ok);",
    "request.TryGetProperty(\"buttons\"",
    "eventCallback(\"button:\" + buttonName, submittedValues);",
    "UIForm button '\" + buttonName + \"' callback failed:",
    "catch (Exception exception)",
    "actions.Children.Add(actionButton);",
    "field.TryGetProperty(\"validationError\"",
    "field.TryGetProperty(\"schemaValidationError\"",
    "panel.Children.Add(new TextBlock { Text = validationError });",
    "request.TryGetProperty(\"initialFocus\"",
    "initialEditor.Focus();",
    "editor.IsVisible && editor.IsEnabled && editor.Focusable && editor.IsTabStop)?.Focus();",
    "panel.Children.Add(actions);",
    "Theme = \"@style/Theme.AppCompat.DayNight.NoActionBar\"",
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
if (!desktopRuntimeSource.Contains("buttons = form.Buttons.Select", StringComparison.Ordinal))
    throw new Exception("Android UIForm requests must include the shared UIForm button model.");

var eventDispatcherSourcePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "XPScript.Compiler", "UIFormEventDispatcherPostProcessor.cs");
var eventDispatcherSource = File.ReadAllText(eventDispatcherSourcePath);
foreach (var expected in new[]
{
    "ApplySubmittedStateJson(submittedValue);",
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
foreach (var expected in new[] { "UIForm(\"Android UIForm Regression\")", "AddTextField", "AddTextArea", "AddCheckBox", "Application.Debug.Info" })
{
    if (!uiFormRegressionSample.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android UIForm regression sample is incomplete: " + expected);
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
    "XPSCRIPT-LIFECYCLE"
})
{
    if (!manualUiFormSample.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android manual UIForm verification sample is incomplete: " + expected);
}

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
