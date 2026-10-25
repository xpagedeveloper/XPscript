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

var avaloniaGroup = ApplicationDependencyCatalog.FindPatchGroup("Avalonia");
Require(avaloniaGroup is not null, "Avalonia desktop packages must belong to Avalonia patch group");
Require(ApplicationDependencyCatalog.FindPatchGroup("Avalonia.Controls.WebView") is null,
    "Avalonia WebView must remain outside the desktop patch group because it has an independent compatible version");
var avaloniaPackages = ApplicationDependencyCatalog.GetPatchGroupPackages(avaloniaGroup!);
Require(avaloniaPackages.Count == 3, "Avalonia patch group must contain the three desktop packages");

var available = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
{
    ["Avalonia"] = ["12.1.4", "12.1.5", "12.2.0"],
    ["Avalonia.Desktop"] = ["12.1.4", "12.1.5", "12.2.0"],
    ["Avalonia.Themes.Fluent"] = ["12.1.4", "12.1.5", "12.2.0"],
    ["Avalonia.Controls.WebView"] = ["12.1.1", "12.1.3"]
};
var common = ApplicationPackagePatchStore.SelectLatestCommonCompatiblePatch(avaloniaPackages, available);
Require(common == "12.1.5", "Avalonia group must select highest common stable 12.1.x version");

available["Avalonia.Desktop"] = ["12.1.1", "12.1.3"];
common = ApplicationPackagePatchStore.SelectLatestCommonCompatiblePatch(avaloniaPackages, available);
Require(common is null, "group must not patch when no common compatible version exists");

var oldRoot = ApplicationPackagePatchStore.RootDirectoryForVersion("0.9.3-beta");
var newRoot = ApplicationPackagePatchStore.RootDirectoryForVersion("0.9.4-beta");
Require(!string.Equals(oldRoot, newRoot, StringComparison.OrdinalIgnoreCase), "different XPScript versions must use different patch roots");
Require(oldRoot.Contains("0.9.3-beta", StringComparison.Ordinal), "old patch root must include exact XPScript version");
Require(newRoot.Contains("0.9.4-beta", StringComparison.Ordinal), "new patch root must include exact XPScript version");

var vulnerable = new ApplicationPackageVersionSecurity(true, 1, "high");
var secure = new ApplicationPackageVersionSecurity(true, 0);
var unknown = new ApplicationPackageVersionSecurity(false, 0);
Require(ApplicationPackagePatchSecurity.Classify(vulnerable, secure, true) == ApplicationPackagePatchSecurityKind.SecurityPatch,
    "vulnerable baseline fixed by candidate must be a security patch");
Require(ApplicationPackagePatchSecurity.Classify(secure, secure, true) == ApplicationPackagePatchSecurityKind.MaintenancePatch,
    "secure baseline with newer secure candidate must be maintenance");
Require(ApplicationPackagePatchSecurity.Classify(vulnerable, vulnerable, true) == ApplicationPackagePatchSecurityKind.VulnerabilityRemains,
    "candidate that remains vulnerable must not be called a security patch");
Require(ApplicationPackagePatchSecurity.Classify(vulnerable, vulnerable, false) == ApplicationPackagePatchSecurityKind.VulnerableNoCompatiblePatch,
    "vulnerable baseline without a candidate must be reported");
Require(ApplicationPackagePatchSecurity.Classify(secure, secure, false) == ApplicationPackagePatchSecurityKind.AlreadySecure,
    "secure baseline without a candidate must be reported as already secure");
Require(ApplicationPackagePatchSecurity.Classify(unknown, secure, true) == ApplicationPackagePatchSecurityKind.SecurityUnknown,
    "unavailable security metadata must be reported as unknown");

Console.WriteLine("Application package patch policy probe passed.");
