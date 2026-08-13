using System.Text;

namespace XPScript.Compiler;

internal sealed class EscapedQuotePreprocessor
{
    public string Transform(string source)
    {
        var output = new StringBuilder(source.Length);
        var inString = false;

        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (c == '"')
            {
                if (inString && i + 1 < source.Length && source[i + 1] == '"')
                {
                    output.Append("\"\"");
                    i++;
                    continue;
                }

                inString = !inString;
                output.Append(c);
                continue;
            }

            if (inString && c == '\\' && i + 1 < source.Length && source[i + 1] == '"')
            {
                output.Append("\"\"");
                i++;
                continue;
            }

            output.Append(c);
        }

        return output.ToString();
    }
}
