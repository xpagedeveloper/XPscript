using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

public sealed partial class XPScriptTranspiler
{
    private static string GetSourceDirectory(string sourceName)
    {
        var fullSourcePath = Path.GetFullPath(sourceName);
        return Path.GetDirectoryName(fullSourcePath) ?? Environment.CurrentDirectory;
    }

    private static string EscapeCSharpString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string RewriteListPresenceChecks(string source)
    {
        var listNames = Regex.Matches(source, @"(?im)^\s*Dim\s+([A-Za-z_]\w*)\s+List\s+As\s+[A-Za-z_]\w*\s*$")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Length)
            .ToArray();
        foreach (var listName in listNames)
            source = Regex.Replace(source, $@"\bIsElement\s*\(\s*{Regex.Escape(listName)}\s*\((?<key>[^()]*)\)\s*\)", m => $"{listName}.ContainsTag({m.Groups["key"].Value})", RegexOptions.IgnoreCase);
        return source;
    }

    private static string ProtectStringLiterals(string source, out Dictionary<string, string> replacements, string sourceName)
    {
        replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        var output = new StringBuilder(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] != '"')
            {
                output.Append(source[i]);
                continue;
            }

            output.Append('"');
            var inner = new StringBuilder();
            i++;
            for (; i < source.Length; i++)
            {
                if (source[i] == '"')
                {
                    if (i + 1 < source.Length && source[i + 1] == '"')
                    {
                        inner.Append("\"\"");
                        i++;
                        continue;
                    }
                    break;
                }
                inner.Append(source[i]);
            }
            if (i >= source.Length)
            {
                var openingOffset = Math.Max(0, source.LastIndexOf('"', Math.Max(0, i - 1)));
                var prefix = source[..openingOffset];
                var line = 1 + prefix.Count(ch => ch == '\n');
                var lineStart = Math.Max(prefix.LastIndexOf('\n') + 1, 0);
                var column = openingOffset - lineStart + 1;
                var lineEnd = source.IndexOf('\n', openingOffset);
                if (lineEnd < 0) lineEnd = source.Length;
                var safeSource = CompilerDiagnosticRedaction.MaskStringLiterals(source[lineStart..lineEnd]).TrimEnd('\r');
                var diagnostic = new CompileDiagnostic
                {
                    File = Path.GetFileName(sourceName), Line = line, Position = column, EndLine = line,
                    EndColumn = column + 1, Description = "Unterminated string literal.",
                    DiagnosticCode = CompilerDiagnosticCodes.UnterminatedStringLiteral, Category = "syntax",
                    Properties = [new() { Name = "foundToken", Value = "\"" }, new() { Name = "expectedConstruct", Value = "closing string quote" }],
                    SourceCode = safeSource, MarkedCode = safeSource + Environment.NewLine + new string(' ', Math.Max(0, column - 1)) + "^"
                };
                throw new CompilerException("Unterminated string literal.", CompilerDiagnosticCodes.UnterminatedStringLiteral, "syntax", [diagnostic]);
            }
            var marker = $"__XPSCRIPT_STRING_{replacements.Count:D6}__";
            replacements[marker] = EscapeForGeneratedCSharpString(inner.ToString());
            output.Append(marker).Append('"');
        }
        return output.ToString();
    }

    private static string EscapeForGeneratedCSharpString(string sourceInner)
    {
        var decoded = sourceInner.Replace("\"\"", "\"", StringComparison.Ordinal);
        return decoded.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private static string ScopeErrorProtection(string generated)
    {
        var activationIndexes = new[]
        {
            generated.IndexOf("LSControlRuntime.SetGoto(__lsErrCtx", StringComparison.Ordinal),
            generated.IndexOf("LSControlRuntime.SetResumeNext(__lsErrCtx", StringComparison.Ordinal)
        }.Where(x => x >= 0).ToArray();
        if (activationIndexes.Length == 0) return generated;

        var activation = activationIndexes.Min();
        var prefix = generated[..activation];
        var suffix = generated[activation..];
        var removedIds = new HashSet<int>();
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
