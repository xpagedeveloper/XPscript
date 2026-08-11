using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ReservedIdentifierPreprocessor
{
    private static readonly HashSet<string> ReservedTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Program", "Script",
        "XPScriptRuntime", "XPScriptErrorRuntime", "XPScriptReferenceRuntime", "XPScriptTextIO", "XPScriptFileIO", "XPScriptFileSystemRuntime",
        "XPScriptEvaluateRuntime", "XPScriptEvaluateCollectionRuntime", "XPCrossPlatformRuntime", "XPDateRuntime", "XPModuleArrayRuntime", "XPTypeArrayRuntime",
        "XPModuleObjectRuntime", "XPSourceLineRuntime", "LSOperatorArrayRuntime", "LSArrayRuntime", "LSControlRuntime", "LSCoreMarker",
        "LSExtendedRuntime", "LSExtendedErrorRuntime", "LSByRefRuntime",
        "HttpClient", "HttpResponse", "JsonDocument", "JsonObject", "JsonArray", "JsonElement",
        "UIForm", "UIData", "UIItem", "UIFieldValue"
    };

    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var code = StripComment(lines[index]).Trim();
            if (code.Length == 0) continue;

            CheckNamedDeclaration(code, index + 1);
            CheckParameters(code, index + 1);
        }
        return source;
    }

    private static void CheckNamedDeclaration(string code, int line)
    {
        var declaration = Regex.Match(code,
            @"^(?:(?:Public|Private|Static)\s+)?(?:Dim\s+|Static\s+|Class\s+|Type\s+|Enum\s+|Sub\s+|Function\s+|Property\s+(?:Get|Let|Set)\s+)([A-Za-z_]\w*)",
            RegexOptions.IgnoreCase);
        if (!declaration.Success)
        {
            declaration = Regex.Match(code, @"^(?:Public|Private)\s+([A-Za-z_]\w*)\s+(?:\([^)]*\)\s+)?As\s+", RegexOptions.IgnoreCase);
        }
        if (!declaration.Success) return;

        var name = declaration.Groups[1].Value;
        EnsureAllowed(name, line, IsTypeDeclaration(code));
    }

    private static void CheckParameters(string code, int line)
    {
        var header = Regex.Match(code,
            @"^(?:(?:Public|Private|Static)\s+)?(?:Sub|Function|Property\s+(?:Get|Let|Set))\s+[A-Za-z_]\w*\s*\((.*)\)",
            RegexOptions.IgnoreCase);
        if (!header.Success) return;

        foreach (Match match in Regex.Matches(header.Groups[1].Value,
                     @"(?:^|,)\s*(?:(?:Optional|ByVal|ByRef)\s+)*([A-Za-z_]\w*)\b",
                     RegexOptions.IgnoreCase))
            EnsureAllowed(match.Groups[1].Value, line, false);
    }

    private static bool IsTypeDeclaration(string code) =>
        Regex.IsMatch(code, @"^(?:(?:Public|Private)\s+)?(?:Class|Type|Enum)\b", RegexOptions.IgnoreCase);

    private static void EnsureAllowed(string name, int line, bool typeDeclaration)
    {
        if (name.StartsWith("__", StringComparison.OrdinalIgnoreCase))
            throw new CompilerException($"input.xps({line},1): Identifier '{name}' is reserved for XPScript compiler-generated state.");

        if (typeDeclaration && ReservedTypeNames.Contains(name))
            throw new CompilerException($"input.xps({line},1): Type name '{name}' is reserved by the XPScript runtime.");
    }

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                inString = !inString;
            }
            else if (!inString && line[i] == '\'') return line[..i];
        }
        return line;
    }
}