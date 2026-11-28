var root = Path.Combine(Path.GetTempPath(), "xpscript-android-scaffold-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    var exitCode = XpsScaffolder.Run(["android", root]);
    if (exitCode != 0)
        throw new Exception("Android scaffold returned exit code " + exitCode);

    var source = Path.Combine(root, "main.xps");
    var config = Path.Combine(root, "xpscript.json");

    if (!File.Exists(source))
        throw new Exception("Android scaffold did not create main.xps.");
    if (!File.Exists(config))
        throw new Exception("Android scaffold did not create xpscript.json.");

    var sourceText = File.ReadAllText(source);
    var configText = File.ReadAllText(config);

    if (!sourceText.Contains("Hello from XPScript on Android", StringComparison.Ordinal))
        throw new Exception("Android scaffold template content is incorrect.");
    if (!configText.Contains("\"target\": \"android\"", StringComparison.Ordinal))
        throw new Exception("Android scaffold target metadata is missing.");
    if (!configText.Contains("\"applicationType\": \"headless\"", StringComparison.Ordinal))
        throw new Exception("Android scaffold application metadata is missing.");

    try
    {
        _ = XpsScaffolder.Run(["android", root]);
        throw new Exception("Android scaffold overwrote an existing project.");
    }
    catch (IOException)
    {
    }

    Console.WriteLine("ANDROID-SCAFFOLD-PROBE=OK");
}
finally
{
    Directory.Delete(root, recursive: true);
}
