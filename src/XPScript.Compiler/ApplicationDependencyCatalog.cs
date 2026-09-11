namespace XPScript.Compiler;

public sealed record ApplicationPackageReference(string Name, string Version, string Reason);

public static class ApplicationDependencyCatalog
{
    public const string AvaloniaVersion = "12.0.3";
    public const string AvaloniaWebViewVersion = "12.0.1";
    public const string MicrosoftDataSqliteVersion = "10.0.11";
    public const string MicrosoftDataSqlClientVersion = "7.0.2";
    public const string MySqlConnectorVersion = "2.6.2";
    public const string NpgsqlVersion = "10.0.3";
    public const string MimeKitVersion = "4.17.0";

    public static IReadOnlyList<ApplicationPackageReference> Detect(string generatedSource)
    {
        ArgumentNullException.ThrowIfNull(generatedSource);
        var result = new List<ApplicationPackageReference>();
        var usesUi = generatedSource.Contains("XPScriptUI.CreateForm(", StringComparison.Ordinal) ||
                     generatedSource.Contains("XPScriptUIList.CreateListView(", StringComparison.Ordinal) ||
                     generatedSource.Contains("XPScriptUIDialogRuntime.", StringComparison.Ordinal);
        if (usesUi)
        {
            result.Add(new("Avalonia", AvaloniaVersion, "Desktop UI"));
            result.Add(new("Avalonia.Desktop", AvaloniaVersion, "Desktop UI"));
            result.Add(new("Avalonia.Themes.Fluent", AvaloniaVersion, "Desktop UI"));
            result.Add(new("Avalonia.Controls.WebView", AvaloniaWebViewVersion, "Desktop UI WebView"));
        }
        if (generatedSource.Contains("internal sealed class XPScriptDbSqlite", StringComparison.Ordinal))
            result.Add(new("Microsoft.Data.Sqlite", MicrosoftDataSqliteVersion, "SQLite database"));
        if (generatedSource.Contains("internal sealed class XPScriptDbMsSql", StringComparison.Ordinal))
            result.Add(new("Microsoft.Data.SqlClient", MicrosoftDataSqlClientVersion, "SQL Server database"));
        if (generatedSource.Contains("XPScriptDbMySql", StringComparison.Ordinal))
            result.Add(new("MySqlConnector", MySqlConnectorVersion, "MySQL database"));
        if (generatedSource.Contains("XPScriptDbSupabase", StringComparison.Ordinal))
            result.Add(new("Npgsql", NpgsqlVersion, "PostgreSQL/Supabase database"));
        if (generatedSource.Contains("MimeKit.", StringComparison.Ordinal) || generatedSource.Contains("NotesMIMEEntity", StringComparison.Ordinal))
            result.Add(new("MimeKit", MimeKitVersion, "Notes MIME support"));
        return result;
    }
}
