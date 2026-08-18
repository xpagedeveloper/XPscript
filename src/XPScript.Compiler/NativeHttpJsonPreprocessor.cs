using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NativeHttpJsonPreprocessor
{
    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 8);
        var nativeVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();

            var dimNew = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+(HttpClient|JsonDocument|JsonObject|JsonArray|JsonElement)\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNew.Success)
            {
                var name = dimNew.Groups[1].Value;
                var type = dimNew.Groups[2].Value;
                nativeVariables.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                output.Add(indent + $"{name} = {CreateExpression(type, dimNew.Groups[3].Value)}");
                continue;
            }

            var dim = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+(HttpClient|HttpResponse|JsonDocument|JsonObject|JsonArray|JsonElement)\s*$", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                nativeVariables.Add(dim.Groups[1].Value);
                output.Add(indent + $"Dim {dim.Groups[1].Value} As Variant");
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

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && (nativeVariables.Contains(set.Groups[1].Value) || set.Groups[2].Value.Contains("XPScriptNative", StringComparison.Ordinal)))
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
        throw new CompilerException("Unsupported native runtime type: " + type);
    }
}
