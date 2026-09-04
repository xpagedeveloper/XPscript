using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NativeHttpJsonPreprocessor
{
    private const string NativeTypePattern = "XPHttpClient|XPHttpResponse|XPJsonDocument|XPJsonObject|XPJsonArray|XPJsonElement|XPHttpDbSupabase|XPDbSupabase|XPHttpDbDominoRest|XPDBSQLite|XPDbMsSql|XPDbMySql|XPAi|XPAiResponse|AITool";
    private static readonly string[] FeatureMarkers =
    [
        "XPHttpClient", "XPHttpResponse", "XPJsonDocument", "XPJsonObject", "XPJsonArray", "XPJsonElement",
        "JsonParse", "JsonStringify", "JsonEncode", "JsonDecode", "XPHttpDbSupabase", "XPDbSupabase",
        "XPHttpDbDominoRest", "XPDBSQLite", "XPDbMsSql", "XPDbMySql", "XPAi", "AITool"
    ];

    public string Transform(string source)
    {
        if (!PreprocessorFeatureGate.ContainsAny(source, FeatureMarkers))
            return new UIExtensionPreprocessor().Transform(source);

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 8);
        var nativeVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nativeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();

            var dimNew = Regex.Match(line, $@"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+({NativeTypePattern})\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNew.Success)
            {
                var name = dimNew.Groups[1].Value;
                var type = dimNew.Groups[2].Value;
                nativeVariables.Add(name);
                nativeTypes[name] = type;
                output.Add(indent + $"Dim {name} As Variant");
                output.Add(indent + $"{name} = {CreateExpression(type, dimNew.Groups[3].Value)}");
                continue;
            }

            var dim = Regex.Match(line, $@"^Dim\s+([A-Za-z_]\w*)\s+As\s+({NativeTypePattern})\s*$", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                var name = dim.Groups[1].Value;
                var type = dim.Groups[2].Value;
                nativeVariables.Add(name);
                nativeTypes[name] = type;
                output.Add(indent + $"Dim {name} As Variant");
                continue;
            }

            var rewritten = line;
            rewritten = Regex.Replace(rewritten, @"\bXPJsonDocument\.Parse\s*\(", "XPScriptNativeJson.Parse(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bJsonParse\s*\(", "XPScriptNativeJson.Parse(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bJsonStringify\s*\(", "XPScriptNativeJson.Stringify(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bJsonEncode\s*\(", "XPScriptNativeJson.Stringify(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bJsonDecode\s*\(", "XPScriptNativeJson.Parse(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+XPHttpClient\s*(?:\(\s*\))?", "XPScriptNativeHttp.CreateClient()", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+XPJsonDocument\s*(?:\(\s*\))?", "XPScriptNativeJson.CreateDocument()", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+XPJsonObject\s*(?:\(\s*\))?", "XPScriptNativeJson.CreateObject()", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+XPJsonArray\s*(?:\(\s*\))?", "XPScriptNativeJson.CreateArray()", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+XPJsonElement\s*(?:\(\s*\))?", "XPScriptNativeJson.CreateElement()", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+XPHttpDbSupabase\s*\((.*)\)", "new XPScriptHttpDbSupabase($1)", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+XPDbSupabase\s*\((.*)\)", "new XPScriptDbSupabase($1)", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+XPHttpDbDominoRest\s*\((.*)\)", "new XPScriptHttpDbDominoRest($1)", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+XPDBSQLite\s*\((.*)\)", "new XPScriptDbSqlite($1)", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+XPDbMsSql\s*\((.*)\)", "new XPScriptDbMsSql($1)", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+XPDbMySql\s*\((.*)\)", "new XPScriptDbMySql($1)", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+XPAi\s*\((.*)\)", "new XPScriptAi($1)", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+AITool\s*\((.*)\)", "new XPScriptAiTool($1)", RegexOptions.IgnoreCase);

            foreach (var pair in nativeTypes)
            {
                var escapedName = Regex.Escape(pair.Key);
                if (pair.Value.Equals("XPHttpClient", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var method in new[] { "GetJson", "PostJson", "PutJson", "PatchJson", "PostForm", "AddQuery", "LoadForm", "SaveForm", "PutForm" })
                    {
                        rewritten = Regex.Replace(
                            rewritten,
                            $@"\b{escapedName}\.{method}\s*\(",
                            $"XPScriptHttpUiFormHelpers.{method}({pair.Key}, ",
                            RegexOptions.IgnoreCase);
                    }

                    foreach (var method in new[] { "GetAsync", "DeleteAsync", "PostAsync", "PutAsync", "PatchAsync" })
                    {
                        rewritten = Regex.Replace(
                            rewritten,
                            $@"\b{escapedName}\.{method}\s*\(",
                            $"XPScriptAsyncHttp.{method}({pair.Key}, ",
                            RegexOptions.IgnoreCase);
                    }
                }
                else if (pair.Value.Equals("XPHttpResponse", StringComparison.OrdinalIgnoreCase))
                {
                    rewritten = Regex.Replace(
                        rewritten,
                        $@"\b{escapedName}\.Json\s*\(\s*\)",
                        $"XPScriptHttpUiFormHelpers.ResponseJson({pair.Key})",
                        RegexOptions.IgnoreCase);
                }
                else if (pair.Value.Equals("XPDBSQLite", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var method in new[] { "QueryArray", "GetRow", "SaveRow" })
                    {
                        rewritten = Regex.Replace(
                            rewritten,
                            $@"\b{escapedName}\.{method}\s*\(",
                            $"XPScriptSqliteDataSourceExtensions.{method}({pair.Key}, ",
                            RegexOptions.IgnoreCase);
                    }
                    rewritten = Regex.Replace(
                        rewritten,
                        $@"\b{escapedName}\.Attachments\s*\(",
                        $"XPScriptDatabaseAttachmentRuntime.ForSqlite({pair.Key}, ",
                        RegexOptions.IgnoreCase);
                }
                else if (pair.Value.Equals("XPDbMsSql", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var method in new[] { "QueryArray", "GetRow", "SaveRow" })
                    {
                        rewritten = Regex.Replace(
                            rewritten,
                            $@"\b{escapedName}\.{method}\s*\(",
                            $"XPScriptMsSqlDataSourceExtensions.{method}({pair.Key}, ",
                            RegexOptions.IgnoreCase);
                    }
                    rewritten = Regex.Replace(
                        rewritten,
                        $@"\b{escapedName}\.Attachments\s*\(",
                        $"XPScriptDatabaseAttachmentRuntime.ForMsSql({pair.Key}, ",
                        RegexOptions.IgnoreCase);
                }
                else if (pair.Value.Equals("XPHttpDbSupabase", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var method in new[] { "QueryArray", "GetRow", "SaveRow" })
                    {
                        rewritten = Regex.Replace(
                            rewritten,
                            $@"\b{escapedName}\.{method}\s*\(",
                            $"XPScriptHttpDatabaseDataSourceExtensions.{method}({pair.Key}, ",
                            RegexOptions.IgnoreCase);
                    }
                    rewritten = Regex.Replace(
                        rewritten,
                        $@"\b{escapedName}\.Attachments\s*\(",
                        $"XPScriptDatabaseAttachmentRuntime.ForSupabase({pair.Key}, ",
                        RegexOptions.IgnoreCase);
                    rewritten = Regex.Replace(
                        rewritten,
                        $@"\b{escapedName}\.SetAttachmentBucket\s*\(",
                        $"XPScriptDatabaseAttachmentRuntime.SetSupabaseBucket({pair.Key}, ",
                        RegexOptions.IgnoreCase);
                }
                else if (pair.Value.Equals("XPHttpDbDominoRest", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var method in new[] { "GetViewArray", "QueryArray", "GetRow", "SaveRow" })
                    {
                        rewritten = Regex.Replace(
                            rewritten,
                            $@"\b{escapedName}\.{method}\s*\(",
                            $"XPScriptHttpDatabaseDataSourceExtensions.{method}({pair.Key}, ",
                            RegexOptions.IgnoreCase);
                    }
                    rewritten = Regex.Replace(
                        rewritten,
                        $@"\b{escapedName}\.Attachments\s*\(",
                        $"XPScriptDatabaseAttachmentRuntime.ForDomino({pair.Key}, ",
                        RegexOptions.IgnoreCase);
                }
                else if (pair.Value.Equals("XPAi", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var method in new[] { "AddTool", "RemoveTool", "HasTool", "GetTool", "ClearTools", "GetToolNames", "ToolCount" })
                    {
                        rewritten = Regex.Replace(
                            rewritten,
                            $@"\b{escapedName}\.{method}\s*\(",
                            $"XPScriptAiToolRegistry.{method}({pair.Key}, ",
                            RegexOptions.IgnoreCase);
                    }
                    rewritten = rewritten
                        .Replace($"XPScriptAiToolRegistry.ClearTools({pair.Key}, )", $"XPScriptAiToolRegistry.ClearTools({pair.Key})", StringComparison.OrdinalIgnoreCase)
                        .Replace($"XPScriptAiToolRegistry.GetToolNames({pair.Key}, )", $"XPScriptAiToolRegistry.GetToolNames({pair.Key})", StringComparison.OrdinalIgnoreCase)
                        .Replace($"XPScriptAiToolRegistry.ToolCount({pair.Key}, )", $"XPScriptAiToolRegistry.ToolCount({pair.Key})", StringComparison.OrdinalIgnoreCase);
                }
            }

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && (nativeVariables.Contains(set.Groups[1].Value) || set.Groups[2].Value.Contains("XPScriptNative", StringComparison.Ordinal) || set.Groups[2].Value.Contains("XPScriptHttpUiFormHelpers", StringComparison.Ordinal) || set.Groups[2].Value.Contains("XPScriptAsyncHttp", StringComparison.Ordinal) || set.Groups[2].Value.Contains("XPScriptHttpDb", StringComparison.Ordinal) || set.Groups[2].Value.Contains("XPScriptDbSupabase", StringComparison.Ordinal) || set.Groups[2].Value.Contains("XPScriptDbSqlite", StringComparison.Ordinal) || set.Groups[2].Value.Contains("XPScriptDbMsSql", StringComparison.Ordinal) || set.Groups[2].Value.Contains("XPScriptDbMySql", StringComparison.Ordinal) || set.Groups[2].Value.Contains("XPScriptSqliteDataSourceExtensions", StringComparison.Ordinal) || set.Groups[2].Value.Contains("XPScriptMsSqlDataSourceExtensions", StringComparison.Ordinal) || set.Groups[2].Value.Contains("XPScriptHttpDatabaseDataSourceExtensions", StringComparison.Ordinal) || set.Groups[2].Value.Contains("XPScriptDatabaseAttachmentRuntime", StringComparison.Ordinal) || set.Groups[2].Value.Contains("XPScriptAi", StringComparison.Ordinal)))
                rewritten = set.Groups[1].Value + " = " + set.Groups[2].Value;

            output.Add(indent + rewritten);
        }

        return new UIExtensionPreprocessor().Transform(string.Join(Environment.NewLine, output));
    }

    private static string CreateExpression(string type, string rawArguments)
    {
        var args = rawArguments.Trim();
        if (type.Equals("XPHttpClient", StringComparison.OrdinalIgnoreCase)) return "XPScriptNativeHttp.CreateClient()";
        if (type.Equals("XPJsonDocument", StringComparison.OrdinalIgnoreCase)) return string.IsNullOrWhiteSpace(args) ? "XPScriptNativeJson.CreateDocument()" : $"XPScriptNativeJson.Parse({args})";
        if (type.Equals("XPJsonObject", StringComparison.OrdinalIgnoreCase)) return "XPScriptNativeJson.CreateObject()";
        if (type.Equals("XPJsonArray", StringComparison.OrdinalIgnoreCase)) return "XPScriptNativeJson.CreateArray()";
        if (type.Equals("XPJsonElement", StringComparison.OrdinalIgnoreCase)) return "XPScriptNativeJson.CreateElement()";
        if (type.Equals("XPHttpDbSupabase", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(args)) throw new CompilerException("XPHttpDbSupabase requires baseUrl and apiKey arguments.");
            return $"new XPScriptHttpDbSupabase({args})";
        }
        if (type.Equals("XPDbSupabase", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(args)) throw new CompilerException("XPDbSupabase requires either a PostgreSQL connection string or REST baseUrl and apiKey arguments.");
            return $"new XPScriptDbSupabase({args})";
        }
        if (type.Equals("XPHttpDbDominoRest", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(args)) throw new CompilerException("XPHttpDbDominoRest requires baseUrl, bearerToken and dataSource arguments.");
            return $"new XPScriptHttpDbDominoRest({args})";
        }
        if (type.Equals("XPDBSQLite", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(args)) throw new CompilerException("XPDBSQLite requires a database path argument.");
            return $"new XPScriptDbSqlite({args})";
        }
        if (type.Equals("XPDbMsSql", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(args)) throw new CompilerException("XPDbMsSql requires a connection string argument.");
            return $"new XPScriptDbMsSql({args})";
        }
        if (type.Equals("XPDbMySql", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(args)) throw new CompilerException("XPDbMySql requires a connection string argument.");
            return $"new XPScriptDbMySql({args})";
        }
        if (type.Equals("XPAi", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(args)) throw new CompilerException("XPAi requires an endpoint argument.");
            return $"new XPScriptAi({args})";
        }
        if (type.Equals("AITool", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(args)) throw new CompilerException("AITool requires a name argument.");
            return $"new XPScriptAiTool({args})";
        }
        throw new CompilerException("Unsupported native runtime type: " + type);
    }
}
