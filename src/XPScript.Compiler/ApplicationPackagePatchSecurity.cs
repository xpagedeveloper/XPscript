namespace XPScript.Compiler;

public enum ApplicationPackagePatchSecurityKind
{
    AlreadySecure,
    SecurityPatch,
    MaintenancePatch,
    VulnerabilityRemains,
    VulnerableNoCompatiblePatch,
    SecurityUnknown
}

public sealed record ApplicationPackageVersionSecurity(
    bool Available,
    int VulnerabilityCount,
    string? HighestSeverity = null)
{
    public bool Vulnerable => VulnerabilityCount > 0;
}

public static class ApplicationPackagePatchSecurity
{
    public static ApplicationPackagePatchSecurityKind Classify(
        ApplicationPackageVersionSecurity baseline,
        ApplicationPackageVersionSecurity candidate,
        bool hasNewerCandidate)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(candidate);

        if (!baseline.Available || !candidate.Available)
            return ApplicationPackagePatchSecurityKind.SecurityUnknown;

        if (!hasNewerCandidate)
            return baseline.Vulnerable
                ? ApplicationPackagePatchSecurityKind.VulnerableNoCompatiblePatch
                : ApplicationPackagePatchSecurityKind.AlreadySecure;

        if (baseline.Vulnerable && !candidate.Vulnerable)
            return ApplicationPackagePatchSecurityKind.SecurityPatch;

        if (baseline.Vulnerable && candidate.Vulnerable)
            return ApplicationPackagePatchSecurityKind.VulnerabilityRemains;

        return ApplicationPackagePatchSecurityKind.MaintenancePatch;
    }

    public static string ToDisplayText(ApplicationPackagePatchSecurityKind kind) => kind switch
    {
        ApplicationPackagePatchSecurityKind.AlreadySecure => "already secure",
        ApplicationPackagePatchSecurityKind.SecurityPatch => "security patch",
        ApplicationPackagePatchSecurityKind.MaintenancePatch => "maintenance patch",
        ApplicationPackagePatchSecurityKind.VulnerabilityRemains => "vulnerability remains",
        ApplicationPackagePatchSecurityKind.VulnerableNoCompatiblePatch => "vulnerable; no compatible patch",
        _ => "security status unavailable"
    };
}
