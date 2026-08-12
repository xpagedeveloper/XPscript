from pathlib import Path
import re

path = Path("src/XPScript.Compiler/CoreCompatibilityTranspiler.cs")
text = path.read_text(encoding="utf-8")

old_call = "finalLine = RewriteByRefCalls(finalLine, className);"
new_call = "finalLine = RewriteByRefCalls(finalLine, className, scalarTypes);"
if old_call not in text:
    raise SystemExit("ByRef call site not found")
text = text.replace(old_call, new_call)

pattern = re.compile(
    r"    private string RewriteByRefCalls\(string line, string\? className\)\n    \{.*?\n    \}\n\n    private string RewriteErrorExpressions",
    re.S,
)
replacement = '''    private string RewriteByRefCalls(string line, string? className, IReadOnlyDictionary<string, string> scalarTypes)
    {
        foreach (var proc in _procedures.Where(x => x.Parameters.Any(p => p.ByRef && !p.IsArray && !p.IsList)))
        {
            if (proc.ClassName is null)
            {
                line = ReplaceCall(line, proc.Name, argsRaw =>
                {
                    var args = SplitArguments(argsRaw);
                    return TryWrapByRefArguments(proc, args, scalarTypes)
                        ? proc.Name + "(" + string.Join(", ", args) + ")"
                        : proc.Name + "(" + argsRaw + ")";
                });
                continue;
            }

            var memberPattern = new Regex($@"(?<target>[A-Za-z_]\\w*|Me)\\.{Regex.Escape(proc.Name)}\\s*\\(", RegexOptions.IgnoreCase);
            var offset = 0;
            while (true)
            {
                var match = memberPattern.Match(line, offset);
                if (!match.Success) break;
                var target = match.Groups["target"].Value;
                string? targetType = target.Equals("Me", StringComparison.OrdinalIgnoreCase)
                    ? className
                    : scalarTypes.TryGetValue(target, out var knownType) ? knownType : null;
                if (!proc.ClassName.Equals(targetType, StringComparison.OrdinalIgnoreCase))
                {
                    offset = match.Index + match.Length;
                    continue;
                }

                var open = line.IndexOf('(', match.Index);
                var close = FindMatchingParen(line, open);
                if (close < 0) break;
                var argsRaw = line[(open + 1)..close];
                var args = SplitArguments(argsRaw);
                if (!TryWrapByRefArguments(proc, args, scalarTypes))
                {
                    offset = close + 1;
                    continue;
                }

                var rendered = target + "." + proc.Name + "(" + string.Join(", ", args) + ")";
                line = line[..match.Index] + rendered + line[(close + 1)..];
                offset = match.Index + rendered.Length;
            }

            if (proc.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
            {
                line = ReplaceCall(line, proc.Name, argsRaw =>
                {
                    var args = SplitArguments(argsRaw);
                    return TryWrapByRefArguments(proc, args, scalarTypes)
                        ? proc.Name + "(" + string.Join(", ", args) + ")"
                        : proc.Name + "(" + argsRaw + ")";
                });
            }
        }
        return line;
    }

    private static bool TryWrapByRefArguments(ProcedureInfo proc, List<string> args, IReadOnlyDictionary<string, string> scalarTypes)
    {
        if (args.Count != proc.Parameters.Count) return false;
        for (var i = 0; i < args.Count; i++)
        {
            var parameter = proc.Parameters[i];
            if (!parameter.ByRef || parameter.IsArray || parameter.IsList) continue;
            var target = args[i].Trim();
            var targetMatch = Regex.Match(target, @"^(?<name>[A-Za-z_]\\w*)(?:\\.Value)?$");
            if (!targetMatch.Success) return false;
            var root = targetMatch.Groups["name"].Value;
            if (!scalarTypes.TryGetValue(root, out var actualType) || !actualType.Equals(parameter.Type, StringComparison.OrdinalIgnoreCase))
                return false;
            args[i] = $"LSByRefRuntime.Create(() => (object?)({target}), __lsv => {target} = {ConvertExpression(parameter.Type, \"__lsv\")})";
        }
        return true;
    }

    private string RewriteErrorExpressions'''

updated, count = pattern.subn(lambda _: replacement, text)
if count != 1:
    raise SystemExit(f"Expected one RewriteByRefCalls block, replaced {count}")
path.write_text(updated, encoding="utf-8")
