using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NativeHttpJsonPreprocessor
{
    private const string NativeTypePattern = "HttpClient|HttpResponse|JsonDocument|JsonObject|JsonArray|JsonElement|HTTPDBSupabase|HTTPDBDominoRest";

    public string Transform(string source)
    {
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
            rewritten = Regex.Replace(rewritten, @"\bJsonDocument\.Parse\s*\(", "XPScriptNativeJson.Parse(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bJsonParse\s*\(", "XPScriptNativeJson.Parse(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bJsonStringify\s*\(", "XPScriptNativeJson.Stringify(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bJsonEncode\s*\(", "XPScriptNativeJson.Stringify(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bJsonDecode\s*\(", "XPScriptNativeJson.Parse(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+HttpClient\s*(?:\(\s*\))?", "XPScriptNativeHttp.CreateClient()", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+JsonDocument\s*(?:\(\s*\))?", "XPScriptNativeJson.CreateDocument()", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+JsonObject\s*(?:\(\s*\))?", "XPScriptNativeJson.CreateObject()", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+JsonArray\s*(?:\(\s*\))?", "XPScriptNativeJson.CreateArray()", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+JsonElement\s*(?:\(\s*\))?", "XPScriptNativeJson.CreateElement()", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+HTTPDBSupabase\s*\((.*)\)", "new XPScriptHttpDbSupabase($1)", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+HTTPDBDominoRest\s*\((.*)\)", "new XPScriptHttpDbDominoRest($1)", RegexOptions.IgnoreCase);

            foreach (var pair in nativeTypes)
            {
                var escapedName = Regex.Escape(pair.Key);
                if (pair.Value.Equals("HttpClient", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var method in new[] { "GetJson", "PostJson", "PutJson", "PatchJson", "PostForm", "AddQuery", "LoadForm", "SaveForm", "PutForm" })
                    {
                        rewritten = Regex.Replace(
                            rewritten,
                            $@"\b{escapedName}\.{method}\s*\(",
                            $"XPScriptHttpUiFormHelpers.{method}({pair.Key}, ",
                            RegexOptions.IgnoreCase);
                    }
                }
                else if (pair.Value.Equals("HttpResponse", StringComparison.OrdinalIgnoreCase))
                {
                    rewritten = Regex.Replace(
                        rewritten,
                        $@"\b{escapedName}\.Json\s*\(\s*\)",
                        $"XPScriptHttpUiFormHelpers.ResponseJson({pair.Key})",
                        RegexOptions.IgnoreCase);
                }
            }

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && (nativeVariables.Contains(set.Groups[1].Value) || set.Groups[2].Value.Contains("XPScriptNative", StringComparison.Ordinal) || set.Groups[2].Value.Contains("XPScriptHttpUiFormHelpers", StringComparison.Ordinal) || set.Groups[2].Value.Contains("XPScriptHttpDb", StringComparison.Ordinal)))
                rewritten = set.Groups[1].Value + " = " + set.Groups[2].Value;

            output.Add(indent + rewritten);
        }

        return new UIExtensionPreprocessor().Transform(string.Join(Environment.NewLine, output));
    }

    private static string CreateExpression(string type, string rawArguments)
    {
        var args = rawArguments.Trim();
        if (type.Equals("HttpClient", StringComparison.OrdinalIgnoreCase)) return "XPScriptNativeHttp.CreateClient()";
        if (type.Equals("JsonDocument", StringComparison.OrdinalIgnoreCase)) return string.IsNullOrWhiteSpace(args) ? "XPScriptNativeJson.CreateDocument()" : $"XPScriptNativeJson.Parse({args})";
        if (type.Equals("JsonObject", StringComparison.OrdinalIgnoreCase)) return "XPScriptNativeJson.CreateObject()";
        if (type.Equals("JsonArray", StringComparison.OrdinalIgnoreCase)) return "XPScriptNativeJson.CreateArray()";
        if (type.Equals("JsonElement", StringComparison.OrdinalIgnoreCase)) return "XPScriptNativeJson.CreateElement()";
        if (type.Equals("HTTPDBSupabase", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(args)) throw new CompilerException("HTTPDBSupabase requires baseUrl and apiKey arguments.");
            return $"new XPScriptHttpDbSupabase({args})";
        }
        if (type.Equals("HTTPDBDominoRest", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(args)) throw new CompilerException("HTTPDBDominoRest requires baseUrl, bearerToken and dataSource arguments.");
            return $"new XPScriptHttpDbDominoRest({args})";
        }
        throw new CompilerException("Unsupported native runtime type: " + type);
    }
}
