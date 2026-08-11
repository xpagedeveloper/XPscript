using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

public sealed class XPScriptTranspiler
{
    public string Transpile(string source, string sourceName)
    {
        source = new LanguageExtensionsPreprocessor().Transform(source);
        source = new PropertyLetCompatibilityPreprocessor().Transform(source);
        source = new NativeHttpJsonPreprocessor().Transform(source);
        var moduleGlobals = new ModuleGlobalsPreprocessor();
        source = moduleGlobals.Transform(source);
        new SourceTypeValidator().Validate(source, sourceName);
        source = new TypeCoercionPreprocessor().Transform(source);
        source = new FileIoExtensionsPreprocessor().Transform(source);

        var operatorArray = new OperatorArrayCompatibilityPreprocessor();
        source = operatorArray.NormalizeSource(source);
        var protectedSource = ProtectStringLiterals(source, out var protectedStrings);
        protectedSource = RewriteListPresenceChecks(protectedSource);
        protectedSource = operatorArray.TransformProtectedSource(protectedSource);
        protectedSource = new TextIoCompatibilityPreprocessor().Transform(protectedSource);
        protectedSource = new ReferenceRuntimeExtensionsPreprocessor().Transform(protectedSource);
        protectedSource = new JsonHttpCompatibilityPreprocessor().Transform(protectedSource);
        protectedSource = new ExtendedCompatibilityTranspiler().Transform(protectedSource);
        var generated = new CoreCompatibilityTranspiler().Transpile(protectedSource, sourceName);
        generated = moduleGlobals.Inject(generated);

        generated = Regex.Replace(generated, @"(?<=\S)\s+\+\+\s+(?=\S)", " && ");

        generated += "\n\n" + CoreControlRuntimeSource.Code + "\n";
        generated += "\n\n" + ExtendedCompatibilityRuntimeSource.Code + "\n";
        generated += "\n\n" + JsonHttpCompatibilityRuntimeSource.Code + "\n";
        generated += "\n\n" + JsonNodesSerializerShimSource.Code + "\n";
        generated += "\n\n" + TextIoCompatibilityRuntimeSource.Code + "\n";
        generated += "\n\n" + FileIoExtensionsRuntimeSource.Code + "\n";
        generated += "\n\n" + ReferenceRuntimeExtensionsSource.Code + "\n";
        generated += "\n\n" + NativeHttpRuntimeSource.Code + "\n";
        generated += "\n\n" + NativeJsonRuntimeSource.Code + "\n";
        generated += "\n\n" + OperatorArrayCompatibilityRuntimeSource.Code + "\n";
        generated += "\n\n" + TypeCoercionRuntimeSource.Code + "\n";

        generated = generated.Replace(
            "XPScriptRuntime.SetArgs(args);",
            $"XPScriptRuntime.SetArgs(args);\n        LSOperatorArrayRuntime.SetCompareNoCase({operatorArray.CompareNoCase.ToString().ToLowerInvariant()});",
            StringComparison.Ordinal);

        generated = generated.Replace("text.StartsWith('/', StringComparison.Ordinal)", "text.StartsWith(\"/\", StringComparison.Ordinal)", StringComparison.Ordinal);
        generated = generated.Replace("byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),", "byte[] requestBytes => System.Text.Encoding.UTF8.GetString(requestBytes),", StringComparison.Ordinal);
        generated = generated.Replace("using System.Text.RegularExpressions;", "using System.Text.RegularExpressions;\nusing System.Runtime.InteropServices;", StringComparison.Ordinal);
        generated = Regex.Replace(generated, @"(?m)^\s*__lsErrCtx\.Statement\s*=\s*\d+;\s*\r?$\n?", "");
        generated = ScopeErrorProtection(generated);

        foreach (var item in protectedStrings) generated = generated.Replace(item.Key, item.Value, StringComparison.Ordinal);
        return generated.Replace(".Value!.IsNothing", ".IsNothing", StringComparison.Ordinal);
    }

    public string Transpile(string source) => Transpile(source, "input.xps");

    private static string RewriteListPresenceChecks(string source)
    {
        var listNames = Regex.Matches(source, @"(?im)^\s*Dim\s+([A-Za-z_]\w*)\s+List\s+As\s+[A-Za-z_]\w*\s*$")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Length)
            .ToArray();

        foreach (var listName in listNames)
        {
            source = Regex.Replace(
                source,
                $@"\bIsElement\s*\(\s*{Regex.Escape(listName)}\s*\((?<key>[^()]*)\)\s*\)",
                m => $"{listName}.ContainsTag({m.Groups["key"].Value})",
                RegexOptions.IgnoreCase);
        }
        return source;
    }

    private static string ProtectStringLiterals(string source, out Dictionary<string, string> replacements)
    {
        replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        var output = new StringBuilder(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] != '"') { output.Append(source[i]); continue; }
            output.Append('"');
            var inner = new StringBuilder(); i++;
            for (; i < source.Length; i++)
            {
                if (source[i] == '"')
                {
                    if (i + 1 < source.Length && source[i + 1] == '"') { inner.Append("\"\""); i++; continue; }
                    break;
                }
                inner.Append(source[i]);
            }
            if (i >= source.Length) throw new CompilerException("Unterminated string literal.");
            var marker = $"__XPSCRIPT_STRING_{replacements.Count:D6}__";
            replacements[marker] = EscapeForGeneratedCSharpString(inner.ToString());
            output.Append(marker).Append('"');
        }
        return output.ToString();
    }

    private static string EscapeForGeneratedCSharpString(string sourceInner)
    {
        var decoded = sourceInner.Replace("\"\"", "\"", StringComparison.Ordinal);
        return decoded.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private static string ScopeErrorProtection(string generated)
    {
        var activationIndexes = new[]
        {
            generated.IndexOf("LSControlRuntime.SetGoto(__lsErrCtx", StringComparison.Ordinal),
            generated.IndexOf("LSControlRuntime.SetResumeNext(__lsErrCtx", StringComparison.Ordinal)
        }.Where(x => x >= 0).ToArray();
        if (activationIndexes.Length == 0) return generated;

        var activation = activationIndexes.Min(); var prefix = generated[..activation]; var suffix = generated[activation..]; var removedIds = new HashSet<int>();
        var wrapperPattern = new Regex(@"(?m)^(?<indent>[ \t]*)__ls_stmt_before_(?<id>\d+):;\r?\n[ \t]*try \{ (?<statement>.*) \}\r?\n[ \t]*catch \(Exception __lsEx\) \{.*\}\r?\n[ \t]*__ls_stmt_after_\d+:;\r?\n?", RegexOptions.CultureInvariant);
        prefix = wrapperPattern.Replace(prefix, match =>
        {
            removedIds.Add(int.Parse(match.Groups["id"].Value));
            return match.Groups["indent"].Value + match.Groups["statement"].Value + Environment.NewLine;
        });
        generated = prefix + suffix;
        foreach (var id in removedIds)
            generated = Regex.Replace(generated, $@"case\s+{id}:\s+goto\s+__ls_stmt_(?:before|after)_{id};\s*", "", RegexOptions.CultureInvariant);
        return generated;
    }
}
