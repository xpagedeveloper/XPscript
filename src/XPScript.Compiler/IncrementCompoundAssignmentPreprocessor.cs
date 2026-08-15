using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class IncrementCompoundAssignmentPreprocessor
{
    private static readonly Regex Postfix = new(
        @"^(?<indent>\s*)(?<name>[A-Za-z_]\w*)\s*(?<op>\+\+|--)\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex Compound = new(
        @"^(?<indent>\s*)(?<name>[A-Za-z_]\w*)\s*(?<op>\+=|-=|\*=|/=|\\=|&=)\s*(?<rhs>.+?)\s*$",
        RegexOptions.CultureInvariant);

    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
            lines[i] = RewriteLine(lines[i]);
        return string.Join(Environment.NewLine, lines);
    }

    private static string RewriteLine(string line)
    {
        var (code, comment) = SplitComment(line);

        var postfix = Postfix.Match(code);
        if (postfix.Success)
        {
            var indent = postfix.Groups["indent"].Value;
            var name = postfix.Groups["name"].Value;
            var helper = postfix.Groups["op"].Value == "++" ? "Increment" : "Decrement";
            return indent + name + " = XPScriptIncrementRuntime." + helper + "(" + name + ")" + comment;
        }

        var compound = Compound.Match(code);
        if (!compound.Success) return line;

        var compoundIndent = compound.Groups["indent"].Value;
        var target = compound.Groups["name"].Value;
        var rhs = compound.Groups["rhs"].Value.Trim();
        if (rhs.Length == 0) return line;

        var op = compound.Groups["op"].Value switch
        {
            "+=" => "+",
            "-=" => "-",
            "*=" => "*",
            "/=" => "/",
            "\\=" => "\\",
            "&=" => "&",
            _ => throw new InvalidOperationException("Unsupported compound-assignment operator.")
        };

        return compoundIndent + target + " = " + target + " " + op + " (" + rhs + ")" + comment;
    }

    private static (string Code, string Comment) SplitComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"')
                {
                    i++;
                    continue;
                }
                inString = !inString;
                continue;
            }

            if (!inString && line[i] == '\'')
                return (line[..i], line[i..]);
        }

        return (line, "");
    }
}
