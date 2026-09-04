using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ReservedIdentifierPreprocessor
{
    private static readonly HashSet<string> ReservedTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Program", "Script",
        "XPScriptRuntime", "XPScriptErrorRuntime", "XPScriptReferenceRuntime", "XPScriptTextIO", "XPScriptFileIO", "XPScriptFileSystemRuntime", "XPScriptApplicationRuntime",
        "XPScriptEvaluateRuntime", "XPScriptEvaluateCollectionRuntime", "XPScriptEvaluateSemanticsRuntime", "XPScriptEvaluateFunctionArityRuntime", "XPCrossPlatformRuntime", "XPDateRuntime", "XPModuleArrayRuntime", "XPTypeArrayRuntime",
        "XPModuleObjectRuntime", "XPSourceLineRuntime", "LSOperatorArrayRuntime", "LSArrayRuntime", "LSControlRuntime", "LSCoreMarker", "LSObjectIdentityRuntime",
        "LSExtendedRuntime", "LSExtendedErrorRuntime", "LSByRefRuntime",
        "XPHttpClient", "XPHttpResponse",
        "XPJsonDocument", "XPJsonObject", "XPJsonArray", "XPJsonElement",
        "XPCsvDocument", "XPCsvHeaderCollection", "XPCsvRowCollection", "XPCsvRow", "XPCsvColumnCollection", "XPCsvColumn",
        "XPXmlDocument", "XPXmlElement", "XPXmlNode", "XPXmlNodeCollection", "XPXmlAttribute", "XPXmlAttributeCollection",
        "XPXmlValidationResult", "XPXmlValidationError", "XPXmlValidationErrorCollection",
        "XPHttpDbSupabase", "XPHttpDbDominoRest",
        "XPAi", "XPAiResponse",
        "UIForm", "UIData", "UIItem", "UIFieldValue"
    };

    private static readonly HashSet<string> ReservedValueNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Application",
        "Body"
    };

    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var code = StripComment(lines[index]).Trim();
            if (code.Length == 0) continue;

            CheckVariableDeclarations(code, index + 1);
            CheckNamedDeclaration(code, index + 1);
            CheckParameters(code, index + 1);
        }

        new NothingComparisonValidator().Validate(source, "input.xps");
        return new GeneralSyntaxPreprocessor().Transform(source);
    }

    private static void CheckVariableDeclarations(string code, int line)
    {
        string? declarationList = null;

        var explicitDeclaration = Regex.Match(code,
            @"^(?:(?:Public|Private)\s+)?(?:Dim|Static)\s+(.+)$",
            RegexOptions.IgnoreCase);
        if (explicitDeclaration.Success)
        {
            declarationList = explicitDeclaration.Groups[1].Value;
        }
        else
        {
            var moduleDeclaration = Regex.Match(code,
                @"^(?:Public|Private)\s+(?!(?:Sub|Function|Class|Type|Enum|Property)\b)(.+)$",
                RegexOptions.IgnoreCase);
            if (moduleDeclaration.Success)
                declarationList = moduleDeclaration.Groups[1].Value;
        }

        if (declarationList is null) return;

        foreach (var declaration in SplitTopLevelCommaSeparated(declarationList))
        {
            var match = Regex.Match(declaration.Trim(), @"^([A-Za-z_]\w*)\b");
            if (match.Success)
                EnsureAllowed(match.Groups[1].Value, line, false);
        }
    }

    private static IEnumerable<string> SplitTopLevelCommaSeparated(string text)
    {
        var start = 0;
        var depth = 0;
        var inString = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (inString && i + 1 < text.Length && text[i + 1] == '"')
                {
                    i++;
                    continue;
                }
                inString = !inString;
                continue;
            }

            if (inString) continue;

            if (c == '(') depth++;
            else if (c == ')' && depth > 0) depth--;
            else if (c == ',' && depth == 0)
            {
                yield return text[start..i];
                start = i + 1;
            }
        }

        yield return text[start..];
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
            throw new CompilerException($"input.xps({line},1): Identifier is reserved for XPScript compiler-generated state.");

        if (ReservedValueNames.Contains(name))
            throw new CompilerException($"input.xps({line},1): Identifier is reserved by the XPScript runtime.");

        if (typeDeclaration && ReservedTypeNames.Contains(name))
            throw new CompilerException($"input.xps({line},1): Type name is reserved by the XPScript runtime.");
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
