using XPScript.Cli;

var root = Path.Combine(Path.GetTempPath(), "xpscript-android-metadata-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var source = Path.Combine(root, "main.xps");
    File.WriteAllText(source, "Print \"test\"");

    var defaults = AndroidProjectMetadata.LoadForSource(source);
    Require(defaults.Target == "android", "default target");
    Require(defaults.ApplicationType == "headless", "default application type");
    Require(defaults.ApplicationTitle == "XPScript", "default application title");

    File.WriteAllText(Path.Combine(root, "xpscript.json"), """
{
  "target": "android",
  "applicationType": "headless",
  "applicationTitle": "My Android App"
}
""");
    var explicitConfig = AndroidProjectMetadata.LoadForSource(source);
    Require(explicitConfig.Target == "android", "explicit target");
    Require(explicitConfig.ApplicationType == "headless", "explicit application type");
    Require(explicitConfig.ApplicationTitle == "My Android App", "explicit application title");

    File.WriteAllText(Path.Combine(root, "xpscript.json"), """{"target":"desktop","applicationType":"headless"}""");
    ExpectFailure(source, "target to be 'android'");

    File.WriteAllText(Path.Combine(root, "xpscript.json"), """{"target":"android","applicationType":"ui"}""");
    var uiConfig = AndroidProjectMetadata.LoadForSource(source);
    Require(uiConfig.Target == "android", "UI target");
    Require(uiConfig.ApplicationType == "ui", "UI application type");

    File.WriteAllText(Path.Combine(root, "xpscript.json"), """{"target":"android","applicationType":"uiform"}""");
    ExpectFailure(source, "Supported applicationTypes: headless, ui");

    File.WriteAllText(Path.Combine(root, "xpscript.json"), """{"target":"android","applicationTitle":false}""");
    ExpectFailure(source, "applicationTitle must be a string");

    File.WriteAllText(Path.Combine(root, "xpscript.json"), """{"target":"android","applicationType":false}""");
    ExpectFailure(source, "applicationType must be a string");

    Console.WriteLine("ANDROID-PROJECT-METADATA-PROBE=OK");
}
finally
{
    Directory.Delete(root, recursive: true);
}

static void ExpectFailure(string source, string expected)
{
    try
    {
        _ = AndroidProjectMetadata.LoadForSource(source);
        throw new Exception("Expected metadata failure containing: " + expected);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains(expected, StringComparison.Ordinal))
    {
    }
}

static void Require(bool condition, string name)
{
    if (!condition) throw new Exception("Android project metadata regression failed: " + name);
}
