using System.Text;

namespace XPScript.Compiler;

internal sealed class MultilineStringPreprocessor
{
    public string Transform(string source, string sourceName)
    {
        if (string.IsNullOrEmpty(source)) return source;

        var output = new StringBuilder(source.Length);
        var line = 1;
        var pendingBlankLines = 0;

        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];

            if (c == '\'' )
            {
                while (i < source.Length && source[i] != '\r' && source[i] != '\n')
                    output.Append(source[i++]);
                i--;
                continue;
            }

            if (c == '"')
            {
                output.Append(c);
                for (i++; i < source.Length; i++)
                {
                    output.Append(source[i]);
                    if (source[i] != '"') continue;
                    if (i + 1 < source.Length && source[i + 1] == '"')
                    {
                        output.Append(source[++i]);
                        continue;
                    }
                    break;
                }
                continue;
            }

            if (c == '|' || c == '{')
            {
                var close = c == '|' ? '|' : '}';
                var openingLine = line;
                var content = new StringBuilder();
                var newlineCount = 0;
                var closed = false;

                for (i++; i < source.Length; i++)
                {
                    var value = source[i];
                    if (value == close)
                    {
                        if (i + 1 < source.Length && source[i + 1] == close)
                        {
                            content.Append(close);
                            i++;
                            continue;
                        }
                        closed = true;
                        break;
                    }

                    if (value == '\r')
                    {
                        if (i + 1 < source.Length && source[i + 1] == '\n')
                        {
                            content.Append("\r\n");
                            i++;
                        }
                        else
                        {
                            content.Append('\r');
                        }
                        newlineCount++;
                        line++;
                        continue;
                    }

                    if (value == '\n')
                    {
                        content.Append('\n');
                        newlineCount++;
                        line++;
                        continue;
                    }

                    content.Append(value);
                }

                if (!closed)
                    throw new CompilerException($"{sourceName}({openingLine}): Unterminated multiline string literal opened with '{c}'.");

                output.Append(BuildExpression(content.ToString()));
                pendingBlankLines += newlineCount;
                continue;
            }

            if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < source.Length && source[i + 1] == '\n')
                {
                    output.Append("\r\n");
                    i++;
                }
                else
                {
                    output.Append(c);
                }
                line++;
                while (pendingBlankLines-- > 0)
                    output.Append(Environment.NewLine);
                pendingBlankLines = 0;
                continue;
            }

            output.Append(c);
        }

        while (pendingBlankLines-- > 0)
            output.Append(Environment.NewLine);

        return output.ToString();
    }

    private static string BuildExpression(string content)
    {
        var parts = new List<string>();
        var text = new StringBuilder();

        void FlushText()
        {
            if (text.Length == 0) return;
            parts.Add(Quote(text.ToString()));
            text.Clear();
        }

        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] == '\r')
            {
                FlushText();
                if (i + 1 < content.Length && content[i + 1] == '\n')
                {
                    parts.Add("Chr(13)");
                    parts.Add("Chr(10)");
                    i++;
                }
                else
                {
                    parts.Add("Chr(13)");
                }
                continue;
            }

            if (content[i] == '\n')
            {
                FlushText();
                parts.Add("Chr(10)");
                continue;
            }

            text.Append(content[i]);
        }

        FlushText();
        if (parts.Count == 0) return "\"\"";
        return string.Join(" & ", parts);
    }

    private static string Quote(string value) =>
        "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
