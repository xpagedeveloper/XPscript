using XPScript.Compiler;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

Require(ApplicationPackagePatchStore.IsCompatiblePatch("7.0.2", "7.0.2"), "baseline patch must be compatible");
Require(ApplicationPackagePatchStore.IsCompatiblePatch("7.0.2", "7.0.9"), "newer patch must be compatible");
Require(!ApplicationPackagePatchStore.IsCompatiblePatch("7.0.2", "7.0.1"), "downgrade must be rejected");
Require(!ApplicationPackagePatchStore.IsCompatiblePatch("7.0.2", "7.1.0"), "minor upgrade must be rejected");
Require(!ApplicationPackagePatchStore.IsCompatiblePatch("7.0.2", "8.0.0"), "major upgrade must be rejected");
Require(!ApplicationPackagePatchStore.IsCompatiblePatch("7.0.2", "7.0.3-preview.1"), "prerelease must be rejected");

var latest = ApplicationPackagePatchStore.SelectLatestCompatiblePatch(
    "10.0.11",
    ["9.9.99", "10.0.11", "10.0.12", "10.0.14", "10.1.0", "11.0.0", "10.0.15-preview.1"]);
Require(latest == "10.0.14", "latest compatible patch selection failed");

var avaloniaGroup = ApplicationDependencyCatalog.FindPatchGroup("Avalonia.Controls.WebView");
Require(avaloniaGroup is not null, "Avalonia WebView must belong to Avalonia patch group");
var avaloniaPackages = ApplicationDependencyCatalog.GetPatchGroupPackages(avaloniaGroup!);
Require(avaloniaPackages.Count == 4, "Avalonia patch group must contain four packages");

var available = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
{
    ["Avalonia"] = ["12.0.3", "12.0.5", "12.0.7"],
    ["Avalonia.Desktop"] = ["12.0.3", "12.0.5", "12.0.6"],
    ["Avalonia.Themes.Fluent"] = ["12.0.3", "12.0.5", "12.1.0"],
    ["Avalonia.Controls.WebView"] = ["12.0.1", "12.0.5", "12.0.8-preview.1"]
};
var common = ApplicationPackagePatchStore.SelectLatestCommonCompatiblePatch(avaloniaPackages, available);
Require(common == "12.0.5", "Avalonia group must select highest common stable 12.0.x version");

available["Avalonia.Controls.WebView"] = ["12.0.1", "12.0.4"];
common = ApplicationPackagePatchStore.SelectLatestCommonCompatiblePatch(avaloniaPackages, available);
Require(common is null, "group must not patch when no common compatible version exists");

var oldRoot = ApplicationPackagePatchStore.RootDirectoryForVersion("0.9.3-beta");
var newRoot = ApplicationPackagePatchStore.RootDirectoryForVersion("0.9.4-beta");
Require(!string.Equals(oldRoot, newRoot, StringComparison.OrdinalIgnoreCase), "different XPScript versions must use different patch roots");
Require(oldRoot.Contains("0.9.3-beta", StringComparison.Ordinal), "old patch root must include exact XPScript version");
Require(newRoot.Contains("0.9.4-beta", StringComparison.Ordinal), "new patch root must include exact XPScript version");

Console.WriteLine("Application package patch policy probe passed.");
