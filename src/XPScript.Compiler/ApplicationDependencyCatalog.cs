namespace XPScript.Compiler;

public sealed record ApplicationPackageReference(string Name, string Version, string Reason);
public sealed record ApplicationPackagePatchGroup(string Name, IReadOnlyList<string> Packages);

public static class ApplicationDependencyCatalog
{
    public const string XPScriptReleaseVersion = "0.9.3-beta";
    public const string AvaloniaVersion = "12.0.3";
    public const string AvaloniaWebViewVersion = "12.0.1";
    public const string MicrosoftDataSqliteVersion = "10.0.11";
    public const string MicrosoftDataSqlClientVersion = "7.0.2";
    public const string MySqlConnectorVersion = "2.6.2";
    public const string NpgsqlVersion = "10.0.3";
    public const string MimeKitVersion = "4.17.0";

    public static IReadOnlyList<ApplicationPackageReference> Defaults { get; } =
    [
        new("Avalonia", AvaloniaVersion, "Desktop UI"),
        new("Avalonia.Desktop", AvaloniaVersion, "Desktop UI"),
        new("Avalonia.Themes.Fluent", AvaloniaVersion, "Desktop UI"),
        new("Avalonia.Controls.WebView", AvaloniaWebViewVersion, "Desktop UI WebView"),
        new("Microsoft.Data.Sqlite", MicrosoftDataSqliteVersion, "SQLite database"),
        new("Microsoft.Data.SqlClient", MicrosoftDataSqlClientVersion, "SQL Server database"),
        new("MySqlConnector", MySqlConnectorVersion, "MySQL database"),
        new("Npgsql", NpgsqlVersion, "PostgreSQL/Supabase database"),
        new("MimeKit", MimeKitVersion, "Notes MIME support")
    ];

    public static IReadOnlyList<ApplicationPackagePatchGroup> PatchGroups { get; } =
    [
        new("Avalonia", ["Avalonia", "Avalonia.Desktop", "Avalonia.Themes.Fluent", "Avalonia.Controls.WebView"])
    ];

    public static ApplicationPackagePatchGroup? FindPatchGroup(string packageName) =>
        PatchGroups.FirstOrDefault(group => group.Packages.Contains(packageName, StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyList<ApplicationPackageReference> GetPatchGroupPackages(ApplicationPackagePatchGroup group) =>
        group.Packages
            .Select(name => Defaults.First(package => package.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

    public static string ResolveVersion(string packageName, string baselineVersion) =>
        ApplicationPackagePatchStore.ResolveVersion(packageName, baselineVersion);

    public static IReadOnlyList<ApplicationPackageReference> Detect(string generatedSource)
    {
        ArgumentNullException.ThrowIfNull(generatedSource);
        var result = new List<ApplicationPackageReference>();
        var usesUi = generatedSource.Contains("XPScriptUI.CreateForm(", StringComparison.Ordinal) ||
                     generatedSource.Contains("XPScriptUIList.CreateListView(", StringComparison.Ordinal) ||
                     generatedSource.Contains("XPScriptUIDialogRuntime.", StringComparison.Ordinal);
        if (usesUi)
        {
            Add(result, "Avalonia", AvaloniaVersion, "Desktop UI");
            Add(result, "Avalonia.Desktop", AvaloniaVersion, "Desktop UI");
            Add(result, "Avalonia.Themes.Fluent", AvaloniaVersion, "Desktop UI");
            Add(result, "Avalonia.Controls.WebView", AvaloniaWebViewVersion, "Desktop UI WebView");
        }
        if (generatedSource.Contains("internal sealed class XPScriptDbSqlite", StringComparison.Ordinal))
            Add(result, "Microsoft.Data.Sqlite", MicrosoftDataSqliteVersion, "SQLite database");
        if (generatedSource.Contains("internal sealed class XPScriptDbMsSql", StringComparison.Ordinal))
            Add(result, "Microsoft.Data.SqlClient", MicrosoftDataSqlClientVersion, "SQL Server database");
        if (generatedSource.Contains("XPScriptDbMySql", StringComparison.Ordinal))
            Add(result, "MySqlConnector", MySqlConnectorVersion, "MySQL database");
        if (generatedSource.Contains("XPScriptDbSupabase", StringComparison.Ordinal))
            Add(result, "Npgsql", NpgsqlVersion, "PostgreSQL/Supabase database");
        if (generatedSource.Contains("MimeKit.", StringComparison.Ordinal) || generatedSource.Contains("NotesMIMEEntity", StringComparison.Ordinal))
            Add(result, "MimeKit", MimeKitVersion, "Notes MIME support");
        return result;
    }

    private static void Add(List<ApplicationPackageReference> result, string name, string baselineVersion, string reason) =>
        result.Add(new(name, ResolveVersion(name, baselineVersion), reason));
}
