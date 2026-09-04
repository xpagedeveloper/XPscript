namespace XPScript.Compiler;

internal readonly record struct RuntimeFeatures(
    bool Http,
    bool Json,
    bool Xml,
    bool Csv,
    bool Database,
    bool HttpDatabase,
    bool Sqlite,
    bool MsSql,
    bool Attachments,
    bool Ui)
{
    public bool RequiresHttp => Http || HttpDatabase || Attachments || Ui;
    public bool RequiresJson => Json || RequiresHttp || Database || Attachments || Ui;
    public bool RequiresHttpDatabaseTypes => HttpDatabase || Attachments;

    public static RuntimeFeatures Detect(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var code = PreprocessorFeatureGate.CodeOnly(source);

        var httpDatabase = PreprocessorFeatureGate.ContainsTypePrefixReference(code, "HTTPDB") ||
                           PreprocessorFeatureGate.ContainsTypeReference(code, "XPDbSupabase");
        // XPDB is the common, case-insensitive prefix for every database backend.
        // Keep detection open-ended so new backends do not silently miss the shared
        // database runtime when they are added.
        var database = httpDatabase || PreprocessorFeatureGate.ContainsTypePrefixReference(code, "XPDB");
        var attachments = database && PreprocessorFeatureGate.ContainsCall(code, "Attachments");

        return new RuntimeFeatures(
            Http: PreprocessorFeatureGate.ContainsTypeReference(code, "HttpClient", "HttpResponse", "NotesHTTPRequest"),
            Json: PreprocessorFeatureGate.ContainsTypeReference(
                  code, "JsonDocument", "JsonObject", "JsonArray", "JsonElement", "NotesJSONNavigator",
                      "NotesJSONObject", "NotesJSONArray", "NotesJSONElement") ||
                  PreprocessorFeatureGate.ContainsCall(
                      code, "JsonDocument.Parse", "JsonParse", "JsonStringify", "JsonEncode", "JsonDecode"),
            Xml: PreprocessorFeatureGate.ContainsTypePrefixReference(code, "Xml") ||
                 PreprocessorFeatureGate.ContainsCall(code, "XmlDocument.Parse", "XmlParse", "XmlStringify", "XmlEscape"),
            Csv: PreprocessorFeatureGate.ContainsTypePrefixReference(code, "Csv") ||
                 PreprocessorFeatureGate.ContainsCall(
                     code, "CsvDocument.Parse", "CsvDocument.ParseBytes", "CsvParse", "CsvParseBytes",
                     "CsvStringify", "CsvEscape", "CsvSave", "CsvWriteFile"),
            Database: database,
            HttpDatabase: httpDatabase,
            Sqlite: PreprocessorFeatureGate.ContainsTypeReference(code, "XPDBSQLite"),
            MsSql: PreprocessorFeatureGate.ContainsTypeReference(code, "XPDbMsSql"),
            Attachments: attachments,
            Ui: PreprocessorFeatureGate.ContainsTypeReference(code, "UIForm", "UIListView"));
    }
}
