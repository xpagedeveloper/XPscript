using System.Text.RegularExpressions;

namespace XPScript.Compiler;

/// <summary>
/// AST-specific knowledge about XP objects that are not yet represented by the
/// shared symbol/runtime catalog. Keep this registry separate so adding an XP
/// object does not require editing the compilation pipeline itself.
/// </summary>
internal static class AstObjectSupportRegistry
{
    public enum CompilationRoute
    {
        Ast,
        LegacyFallback,
        Unsupported
    }

    public sealed record ObjectDefinition(string Name, CompilationRoute Route, string? Message);

    private static readonly IReadOnlySet<string> AstObjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Application",
        "XPImage",
        "XPSpreadsheet", "XPWorksheet", "XPCell", "XPRange",
        "XPHttpClient", "XPHttpRequest", "XPHttpResponse", "XPHttpPart", "XPHttpFile", "XPHttpPartCollection", "XPHttpFileCollection",
        "XPXmlDocument", "XPXmlElement", "XPXmlNode", "XPXmlNodeCollection", "XPXmlAttribute", "XPXmlAttributeCollection",
        "XPXmlValidationResult", "XPXmlValidationError", "XPXmlValidationErrorCollection",
        "XPCsvDocument", "XPCsvHeaderCollection", "XPCsvRowCollection", "XPCsvRow", "XPCsvColumnCollection", "XPCsvColumn",
        "List",
        "Array",
        "Variant",
        "XPJson",
        "XPJsonDocument",
        "XPJsonSchema",
        "XPJsonArray",
        "XPJsonObject",
        "XPJsonElement", "XPAi", "XPAiResponse", "AITool", "AIToolFunction", "AIToolCall"
    };

    private static readonly IReadOnlySet<string> LegacyObjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Archive", "ArchiveEntry",
        "NetworkTools", "NetworkPingResult", "NetworkTraceHop", "NetworkDnsResult", "NetworkPortResult",
        "NetworkUdpResult", "NetworkHttpResult", "NetworkTlsResult", "NetworkInterfaceInfo", "NetworkEndpointInfo",
        "NetworkHostResult", "NetworkSubnetInfo", "NetworkTlsVersionResult", "NetworkCertificateResult",
        "NetworkHttpHeadersResult", "NetworkHttpSecurityHeadersResult", "NetworkRedirectResult", "NetworkBannerResult",
        "NetworkPortExposureInfo",
        "NotesEmbeddedObject",
        "XPSpreadsheet", "XPWorksheet", "XPCell", "XPRange",
        "SystemInventory", "SystemInventorySnapshot", "SystemInventorySystemInfo", "SystemInventoryCpuInfo",
        "SystemInventoryMemoryInfo", "SystemInventoryPhysicalDiskInfo", "SystemInventoryVolumeInfo",
        "SystemInventoryGraphicsAdapterInfo", "SystemInventoryNetworkAdapterInfo", "InstalledSoftwareInfo"
        ,"UIForm", "UIListView"
        ,"NotesSession", "NotesMail", "NotesDatabase", "NotesDatabaseAccess", "NotesView", "NotesDocumentCollection",
        "NotesNoteCollection", "NotesDocument", "NotesItem", "NotesRichTextItem", "NotesRichTextNavigator",
        "NotesRichTextParagraphStyle", "NotesRichTextRange", "NotesRichTextSection", "NotesRichTextStyle",
        "NotesRichTextTab", "NotesRichTextTable", "NotesRichTextDocLink", "NotesName", "NotesDateTime",
        "NotesAgentResult", "NotesAgent", "NotesStream", "NotesDXLImporter", "NotesDXLExporter",
        "NotesMIMEEntity", "NotesMIMEHeader", "NotesEmbeddedObject", "NotesViewEntry"
        ,"NotesHTTPRequest", "NotesJSONNavigator", "NotesJSONObject", "NotesJSONArray", "NotesJSONElement"
        ,"XPCsvDocument", "XPCsvHeaderCollection", "XPCsvRowCollection", "XPCsvRow", "XPCsvColumnCollection", "XPCsvColumn"
        ,"XPXmlDocument", "XPXmlElement", "XPXmlNode", "XPXmlNodeCollection", "XPXmlAttribute", "XPXmlAttributeCollection",
        "XPXmlValidationResult", "XPXmlValidationError", "XPXmlValidationErrorCollection"
        ,"XPDBSQLite", "XPDBMySQL", "XPDBMsSQL", "XPDbSupabase", "XPDbMySql", "XPDbMsSql", "XPSupabaseDB", "XPHttpDbSupabase", "XPHttpDbDominoRest", "AITool", "XPJsonValidationResult", "XPAiResponse"
        ,"XPHttpPart", "XPHttpFile", "XPPathValue", "XPFileInfoValue", "XPShellResult"
    };

    public static readonly IReadOnlyList<ObjectDefinition> Objects = BuildObjectDefinitions();

    public static readonly IReadOnlySet<string> VariantTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "XPJson", "XPJsonDocument", "XPJsonSchema", "XPJsonArray", "XPJsonObject"
    };

    public static readonly IReadOnlySet<string> CompatibilityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "With", "GoSub", "Worker", "AfterWorker", "SkipLine", "ErrorHandler", "ErrorDone",
        "RetryHandler", "RetryDone", "ResumeTarget", "LabelHandler", "LabelDone",
        "ProviderError", "InvalidConnection", "System"
    };

    private static IReadOnlyList<ObjectDefinition> BuildObjectDefinitions()
    {
        var names = CompilerSymbolCatalog.All
            .Where(symbol => symbol.Kind.Equals("class", StringComparison.OrdinalIgnoreCase))
            .Select(symbol => symbol.Name)
            .Concat(LegacyObjectNames)
            .Concat(["XPImage"])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

        return names
            .Select(name => AstObjectNames.Contains(name)
                ? new ObjectDefinition(name, CompilationRoute.Ast, null)
                : new ObjectDefinition(name, CompilationRoute.LegacyFallback, null))
            .ToArray();
    }

    public static bool UsesUnsupportedObject(string source, out ObjectDefinition? definition)
    {
        if (AstHttpSupport.RequiresLegacy(source))
        {
            definition = new ObjectDefinition("XPHttpClient callbacks/UIForm", CompilationRoute.LegacyFallback, null);
            return true;
        }
        var code = RemoveStringsAndComments(source);
        foreach (var candidate in Objects.Where(item => item.Route != CompilationRoute.Ast))
        {
            if (Regex.IsMatch(code, $@"(?i)\b{Regex.Escape(candidate.Name)}\b"))
            {
                definition = candidate;
                return true;
            }
        }

        definition = null;
        return false;
    }

    internal static bool UsesObject(string source, string name) =>
        Regex.IsMatch(RemoveStringsAndComments(source), $@"(?i)\b{Regex.Escape(name)}\b");

    internal static string RemoveStringsAndComments(string source)
    {
        source = Regex.Replace(source, @"(?im)^[ \t]*Rem\b[^\r\n]*", match => new string(' ', match.Length));
        var result = new System.Text.StringBuilder(source.Length);
        var inString = false;
        var inComment = false;
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (inComment)
            {
                if (c is '\r' or '\n')
                {
                    inComment = false;
                    result.Append(c);
                }
                else result.Append(' ');
                continue;
            }

            if (inString)
            {
                if (c == '"')
                {
                    if (i + 1 < source.Length && source[i + 1] == '"')
                    {
                        result.Append("  ");
                        i++;
                    }
                    else
                    {
                        inString = false;
                        result.Append(' ');
                    }
                }
                else if (c is '\r' or '\n')
                {
                    inString = false;
                    result.Append(c);
                }
                else result.Append(' ');
                continue;
            }

            if (c == '"')
            {
                inString = true;
                result.Append(' ');
            }
            else if (c == '\'')
            {
                inComment = true;
                result.Append(' ');
            }
            else result.Append(c);
        }
        return result.ToString();
    }
}
