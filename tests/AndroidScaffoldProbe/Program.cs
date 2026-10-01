var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var scaffoldSource = File.ReadAllText(Path.Combine(root, "src", "XPScript.Cli", "XpsScaffolder.cs"));

foreach (var expected in new[]
{
    "\"android\"",
    "AndroidTemplate",
    "AndroidProjectConfig",
    "xpscript android run",
    "\"target\": \"android\"",
    "\"applicationType\": \"headless\""
})
{
    if (!scaffoldSource.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Android scaffold regression is missing: " + expected);
}

var cliProject = Path.Combine(root, "src", "XPScript.Cli", "XPScript.Cli.csproj");
if (!File.Exists(cliProject))
    throw new Exception("CLI project is missing.");

Console.WriteLine("ANDROID-SCAFFOLD-PROBE=OK");
