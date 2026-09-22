namespace XPScript.Compiler;

public readonly record struct RuntimeFeatures(
    bool Http,
    bool Json,
    bool JsonSchema,
    bool Xml,
    bool Csv,
    bool Database,
    bool HttpDatabase,
    bool Sqlite,
    bool MsSql,
    bool Attachments,
    bool Ui,
    bool Ai,
    bool Archive = false,
    bool Spreadsheet = false,
    bool NetworkTools = false)
{
    public bool RequiresHttp => Http || HttpDatabase || Attachments || Ui;
    public bool RequiresJson => Json || JsonSchema || RequiresHttp || Database || Attachments || Ui;
    public bool RequiresHttpDatabaseTypes => HttpDatabase || Attachments;

    public IEnumerable<(string Symbol, string AllowedTargets, string? Detail)> UnavailableFor(string runtimeIdentifier)
    {
        if (!runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase))
            yield break;
        if (Ai) yield return ("XPAi", "server target", "Keep AI credentials and requests on the server.");
        if (Sqlite) yield return ("XPDBSQLite", "server or desktop target", null);
        if (MsSql) yield return ("XPDbMsSql", "server or desktop target", null);
        if (Archive) yield return ("Archive", "server or desktop target", "Archive file-path operations are not available for browser-wasm targets yet.");
        if (Spreadsheet) yield return ("XPSpreadsheet", "server or desktop target", null);
        if (NetworkTools) yield return ("NetworkTools", "server or desktop target", "Browser sandboxes do not expose native ICMP, sockets, TLS streams, or local network interface APIs.");
    }

    public static RuntimeFeatures Detect(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var code = PreprocessorFeatureGate.CodeOnly(source);

        var httpDatabase = PreprocessorFeatureGate.ContainsTypePrefixReference(code, "XPHttpDb") ||
                           PreprocessorFeatureGate.ContainsTypeReference(code, "XPDbSupabase");
        // XPDB is the common, case-insensitive prefix for every database backend.
        // Keep detection open-ended so new backends do not silently miss the shared
        // database runtime when they are added.
        var database = httpDatabase || PreprocessorFeatureGate.ContainsTypePrefixReference(code, "XPDB");
        var attachments = database && PreprocessorFeatureGate.ContainsCall(code, "Attachments");
        var jsonSchema = PreprocessorFeatureGate.ContainsTypeReference(code, "XPJsonSchema", "XPJsonValidationResult") ||
                         PreprocessorFeatureGate.ContainsCall(code, "XPJsonSchema.Parse", "XPJsonSchema.FromJson");

        return new RuntimeFeatures(
            Http: PreprocessorFeatureGate.ContainsTypeReference(code, "XPHttpClient", "XPHttpResponse", "NotesHTTPRequest"),
            Json: PreprocessorFeatureGate.ContainsTypeReference(
                  code, "XPJsonDocument", "XPJsonObject", "XPJsonArray", "XPJsonElement", "NotesJSONNavigator",
                      "NotesJSONObject", "NotesJSONArray", "NotesJSONElement") ||
                  PreprocessorFeatureGate.ContainsCall(
                      code, "XPJsonDocument.Parse", "JsonParse", "JsonStringify", "JsonEncode", "JsonDecode"),
            JsonSchema: jsonSchema || PreprocessorFeatureGate.ContainsTypeReference(code, "UIForm"),
            Xml: PreprocessorFeatureGate.ContainsTypePrefixReference(code, "XPXml") ||
                 PreprocessorFeatureGate.ContainsCall(code, "XPXmlDocument.Parse", "XmlParse", "XmlStringify", "XmlEscape"),
            Csv: PreprocessorFeatureGate.ContainsTypePrefixReference(code, "XPCsv") ||
                 PreprocessorFeatureGate.ContainsCall(
                     code, "XPCsvDocument.Parse", "XPCsvDocument.ParseBytes", "XPCsvDocument.Load", "CsvParse", "CsvParseBytes",
                     "CsvStringify", "CsvEscape", "ToCsv"),
            Database: database,
            HttpDatabase: httpDatabase,
            Sqlite: PreprocessorFeatureGate.ContainsTypeReference(code, "XPDBSQLite"),
            MsSql: PreprocessorFeatureGate.ContainsTypeReference(code, "XPDbMsSql"),
            Attachments: attachments,
            Ui: PreprocessorFeatureGate.ContainsTypeReference(code, "UIForm", "UIListView"),
            Ai: PreprocessorFeatureGate.ContainsAny(code, "XPAi", "XPAiResponse", "AITool"),
            Archive: PreprocessorFeatureGate.ContainsTypeReference(code, "Archive", "ArchiveEntry"),
            Spreadsheet: PreprocessorFeatureGate.ContainsTypeReference(code, "XPSpreadsheet", "XPWorksheet", "XPCell"),
            NetworkTools: PreprocessorFeatureGate.ContainsTypeReference(code, "NetworkTools", "NetworkPingResult", "NetworkTraceHop", "NetworkDnsResult", "NetworkPortResult", "NetworkUdpResult", "NetworkHttpResult", "NetworkTlsResult", "NetworkInterfaceInfo", "NetworkEndpointInfo"));
    }
}
