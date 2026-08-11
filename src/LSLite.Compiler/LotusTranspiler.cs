using System.Text.RegularExpressions;

namespace LSLite.Compiler;

public sealed class LotusTranspiler
{
    public string Transpile(string source, string sourceName)
    {
        var generated = new CoreCompatibilityTranspiler().Transpile(source, sourceName);
        generated += "\n\n" + CoreControlRuntimeSource.Code + "\n";

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

        // Object-reference checks such as "q Is Nothing" are emitted through the
        // shared LSRef<T> wrapper. Prevent a later member-access rewrite from
        // turning q.IsNothing into q.Value!.IsNothing.
        return generated.Replace(".Value!.IsNothing", ".IsNothing", StringComparison.Ordinal);
    }

    public string Transpile(string source) =>
        Transpile(source, "input.ls");

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
