using System.Text;
using System.Text.RegularExpressions;

namespace LSLite.Compiler;

internal sealed class ExtendedCompatibilityTranspiler
{
    private static readonly string[] SaxTypes = ["NotesSAXParser", "NotesSAXAttributeList", "NotesSAXException"];
    private static readonly string[] ExtendedFunctions =
    [
        "Environ", "Format", "FormatNumber", "FormatPercent", "Evaluate", "GetObject", "InputBox", "MessageBox", "Shell"
    ];

    public string Transform(string source)
    {
        var lines = JoinContinuations(source);
        var output = new List<string>();

        foreach (var raw in lines)
        {
            var indent = Regex.Match(raw, @"^\s*").Value;
            var line = StripComment(raw).Trim();
            if (line.Length == 0)
            {
                output.Add(raw);
                continue;
            }

            var dimNewSax = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+NotesSAXParser\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNewSax.Success)
            {
                output.Add(indent + $"Dim {dimNewSax.Groups[1].Value} As Variant");
                output.Add(indent + $"{dimNewSax.Groups[1].Value} = LSSaxRuntime.CreateParser({dimNewSax.Groups[2].Value})");
                continue;
            }

            var setNewSax = Regex.Match(line, @"^Set\s+([A-Za-z_]\w*)\s*=\s*New\s+NotesSAXParser\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (setNewSax.Success)
            {
                output.Add(indent + $"{setNewSax.Groups[1].Value} = LSSaxRuntime.CreateParser({setNewSax.Groups[2].Value})");
                continue;
            }

            var onEventCall = Regex.Match(line, @"^On\s+Event\s+([A-Za-z_]\w*)\s+From\s+(.+?)\s+Call\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
            if (onEventCall.Success)
            {
                output.Add(indent + $"Call LSSaxRuntime.Bind({onEventCall.Groups[2].Value}, \"{onEventCall.Groups[1].Value}\", \"{onEventCall.Groups[3].Value}\")");
                continue;
            }

            var onEventRemove = Regex.Match(line, @"^On\s+Event\s+([A-Za-z_]\w*)\s+From\s+(.+?)\s+Remove(?:\s+([A-Za-z_]\w*))?\s*$", RegexOptions.IgnoreCase);
            if (onEventRemove.Success)
            {
                var handler = string.IsNullOrWhiteSpace(onEventRemove.Groups[3].Value) ? "Nothing" : $"\"{onEventRemove.Groups[3].Value}\"";
                output.Add(indent + $"Call LSSaxRuntime.Remove({onEventRemove.Groups[2].Value}, \"{onEventRemove.Groups[1].Value}\", {handler})");
                continue;
            }

            var saxNoArgs = Regex.Match(line, @"^([A-Za-z_]\w*)\.(Process|Parse)\s*$", RegexOptions.IgnoreCase);
            if (saxNoArgs.Success)
            {
                output.Add(indent + $"Call {saxNoArgs.Groups[1].Value}.{saxNoArgs.Groups[2].Value}()");
                continue;
            }

            var saxStatementArgs = Regex.Match(line, @"^([A-Za-z_]\w*)\.(Parse|SetInput|SetOutput|Output)\s+(.+)$", RegexOptions.IgnoreCase);
            if (saxStatementArgs.Success && !saxStatementArgs.Groups[3].Value.TrimStart().StartsWith("(", StringComparison.Ordinal))
            {
                output.Add(indent + $"Call {saxStatementArgs.Groups[1].Value}.{saxStatementArgs.Groups[2].Value}({saxStatementArgs.Groups[3].Value})");
                continue;
            }

            var sleep = Regex.Match(line, @"^Sleep\s*(?:\((.+)\)|\s+(.+))$", RegexOptions.IgnoreCase);
            if (sleep.Success)
            {
                var expression = sleep.Groups[1].Success ? sleep.Groups[1].Value : sleep.Groups[2].Value;
                output.Add(indent + $"Call LSExtendedRuntime.Sleep({expression})");
                continue;
            }

            if (Regex.IsMatch(line, @"^Stop\s*$", RegexOptions.IgnoreCase))
            {
                output.Add(indent + "Call LSExtendedRuntime.Stop()");
                continue;
            }

            var messageStatement = Regex.Match(line, @"^(MessageBox|MsgBox)\s+(.+)$", RegexOptions.IgnoreCase);
            if (messageStatement.Success && !messageStatement.Groups[2].Value.TrimStart().StartsWith("(", StringComparison.Ordinal))
            {
                var args = FillOmittedArguments(messageStatement.Groups[2].Value);
                output.Add(indent + $"Call LSExtendedRuntime.MessageBox({args})");
                continue;
            }

            line = ReplaceOutsideStrings(line, @"(?<![\w.])Err\s*\(\s*\)", "Err");
            line = ReplaceOutsideStrings(line, @"(?<![\w.])Erl\s*\(\s*\)", "Erl");

            foreach (var type in SaxTypes)
                line = ReplaceOutsideStrings(line, $@"\b{type}\b", "Variant");

            foreach (var fn in ExtendedFunctions)
            {
                if (fn.Equals("MessageBox", StringComparison.OrdinalIgnoreCase))
                {
                    line = ReplaceOutsideStrings(line, @"(?<![\w.])MsgBox\s*\(", "LSExtendedRuntime.MessageBox(");
                    line = ReplaceOutsideStrings(line, @"(?<![\w.])MessageBox\s*\(", "LSExtendedRuntime.MessageBox(");
                    continue;
                }

                var dollar = fn is "Environ" or "Format" ? @"\$?" : "";
                line = ReplaceOutsideStrings(line, $@"(?<![\w.]){Regex.Escape(fn)}{dollar}\s*\(", $"LSExtendedRuntime.{fn}(");
            }

            output.Add(indent + line);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static string[] JoinContinuations(string source)
    {
        var physical = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var logical = new List<string>();
        var current = new StringBuilder();

        foreach (var raw in physical)
        {
            var code = StripComment(raw);
            var trimmed = code.TrimEnd();
            var continued = trimmed.EndsWith("_", StringComparison.Ordinal);
            if (continued) trimmed = trimmed[..^1].TrimEnd();

            if (current.Length == 0) current.Append(trimmed);
            else current.Append(' ').Append(trimmed.TrimStart());

            if (continued) continue;
            logical.Add(current.ToString());
            current.Clear();
        }

        if (current.Length > 0) logical.Add(current.ToString());
        return [.. logical];
    }

    private static string FillOmittedArguments(string raw)
    {
        var args = SplitArguments(raw);
        for (var i = 0; i < args.Count; i++)
            if (string.IsNullOrWhiteSpace(args[i])) args[i] = "Nothing";
        return string.Join(", ", args);
    }

    private static List<string> SplitArguments(string raw)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inString = false;
        var depth = 0;
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c == '"')
            {
                current.Append(c);
                if (inString && i + 1 < raw.Length && raw[i + 1] == '"') { current.Append(raw[++i]); continue; }
                inString = !inString;
                continue;
            }
            if (!inString)
            {
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ',' && depth == 0) { result.Add(current.ToString()); current.Clear(); continue; }
            }
            current.Append(c);
        }
        result.Add(current.ToString());
        return result;
    }

    private static string ReplaceOutsideStrings(string input, string pattern, string replacement)
    {
        var sb = new StringBuilder();
        var current = new StringBuilder();
        var inString = false;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c == '"')
            {
                if (inString && i + 1 < input.Length && input[i + 1] == '"') { current.Append("\"\""); i++; continue; }
                if (!inString)
                {
                    sb.Append(Regex.Replace(current.ToString(), pattern, replacement, RegexOptions.IgnoreCase));
                    current.Clear();
                    current.Append(c);
                    inString = true;
                }
                else
                {
                    current.Append(c);
                    sb.Append(current);
                    current.Clear();
                    inString = false;
                }
                continue;
            }
            current.Append(c);
        }

        if (current.Length > 0)
            sb.Append(inString ? current.ToString() : Regex.Replace(current.ToString(), pattern, replacement, RegexOptions.IgnoreCase));
        return sb.ToString();
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
