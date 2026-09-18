namespace XPScript.Compiler;

internal readonly record struct RuntimeFeatures(
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
    bool Ui)
{
    public bool RequiresHttp => Http || HttpDatabase || Attachments || Ui;
    public bool RequiresJson => Json || JsonSchema || RequiresHttp || Database || Attachments || Ui;
    public bool RequiresHttpDatabaseTypes => HttpDatabase || Attachments;

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
            Ui: PreprocessorFeatureGate.ContainsTypeReference(code, "UIForm", "UIListView"));
    }
}
