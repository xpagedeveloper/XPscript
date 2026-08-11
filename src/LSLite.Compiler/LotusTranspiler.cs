using System.Text;
using System.Text.RegularExpressions;

namespace LSLite.Compiler;

public sealed class LotusTranspiler
{
    public string Transpile(string source, string sourceName)
    {
        var protectedSource = ProtectStringLiterals(source, out var protectedStrings);
        protectedSource = new JsonHttpCompatibilityPreprocessor().Transform(protectedSource);
        protectedSource = new ExtendedCompatibilityTranspiler().Transform(protectedSource);
        var generated = new CoreCompatibilityTranspiler().Transpile(protectedSource, sourceName);
        generated += "\n\n" + CoreControlRuntimeSource.Code + "\n";
        generated += "\n\n" + ExtendedCompatibilityRuntimeSource.Code + "\n";
        generated += "\n\n" + JsonHttpCompatibilityRuntimeSource.Code + "\n";
        generated += "\n\n" + JsonNodesSerializerShimSource.Code + "\n";

        // Normalize a couple of generated runtime snippets to valid C# without
        // exposing those implementation details at the LS Lite language surface.
        generated = generated.Replace(
            "text.StartsWith('/', StringComparison.Ordinal)",
            "text.StartsWith(\"/\", StringComparison.Ordinal)",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),",
            "byte[] requestBytes => System.Text.Encoding.UTF8.GetString(requestBytes),",
            StringComparison.Ordinal);

        // Extended standalone compatibility includes optional Windows COM binding.
        // Keep the generated source self-contained even when GetObject is not called.
        generated = generated.Replace(
            "using System.Text.RegularExpressions;",
            "using System.Text.RegularExpressions;\nusing System.Runtime.InteropServices;",
            StringComparison.Ordinal);

        // The active error statement is recorded by LSControlRuntime.Capture when an
        // exception actually occurs. Statements executed by an error handler must not
        // replace that position before Resume or Resume Next executes.
        generated = Regex.Replace(
            generated,
            @"(?m)^\s*__lsErrCtx\.Statement\s*=\s*\d+;\s*\r?$\n?",
            "");

        // The core preprocessor emits protection markers for all executable statements
        // in a procedure that contains On Error. LotusScript only enables trapping after
        // the On Error statement has executed. Strip the generated try/catch wrappers
        // before the first activation point. This also keeps Resume labels out of C#
        // nested scopes that can never be valid error-resume targets at that time.
        generated = ScopeErrorProtection(generated);

        // Restore LotusScript string contents only after all keyword rewriting is done.
        // This prevents identifiers such as Err, Error, Loc, Seek and FreeFile from being
        // interpreted when they occur as plain text inside a string literal.
        foreach (var item in protectedStrings)
            generated = generated.Replace(item.Key, item.Value, StringComparison.Ordinal);

        // Object-reference checks such as "q Is Nothing" are emitted through the
        // shared LSRef<T> wrapper. Prevent a later member-access rewrite from
        // turning q.IsNothing into q.Value!.IsNothing.
        return generated.Replace(".Value!.IsNothing", ".IsNothing", StringComparison.Ordinal);
    }

    public string Transpile(string source) =>
        Transpile(source, "input.ls");

    private static string ProtectStringLiterals(string source, out Dictionary<string, string> replacements)
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
                throw new CompilerException("Unterminated string literal.");

            var marker = $"__LSLITE_STRING_{replacements.Count:D6}__";
            replacements[marker] = EscapeForGeneratedCSharpString(inner.ToString());
            output.Append(marker);
            output.Append('"');
        }

        return output.ToString();
    }

    private static string EscapeForGeneratedCSharpString(string lotusInner)
    {
        var decoded = lotusInner.Replace("\"\"", "\"", StringComparison.Ordinal);
        return decoded
            .Replace("\\", "\\\\", StringComparison.Ordinal)
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
        }
        .Where(x => x >= 0)
        .ToArray();

        if (activationIndexes.Length == 0)
            return generated;

        var activation = activationIndexes.Min();
        var prefix = generated[..activation];
        var suffix = generated[activation..];
        var removedIds = new HashSet<int>();

        var wrapperPattern = new Regex(
            @"(?m)^(?<indent>[ \t]*)__ls_stmt_before_(?<id>\d+):;\r?\n[ \t]*try \{ (?<statement>.*) \}\r?\n[ \t]*catch \(Exception __lsEx\) \{.*\}\r?\n[ \t]*__ls_stmt_after_\d+:;\r?\n?",
            RegexOptions.CultureInvariant);

        prefix = wrapperPattern.Replace(prefix, match =>
        {
            removedIds.Add(int.Parse(match.Groups["id"].Value));
            return match.Groups["indent"].Value + match.Groups["statement"].Value + Environment.NewLine;
        });

        generated = prefix + suffix;

        foreach (var id in removedIds)
        {
            generated = Regex.Replace(
                generated,
                $@"case\s+{id}:\s+goto\s+__ls_stmt_(?:before|after)_{id};\s*",
                "",
                RegexOptions.CultureInvariant);
        }

        return generated;
    }
}
