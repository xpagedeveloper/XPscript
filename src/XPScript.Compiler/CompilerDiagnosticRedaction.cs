namespace XPScript.Compiler;

internal static class CompilerDiagnosticRedaction
{
    public static string MaskStringLiterals(string sourceLine)
    {
        if (string.IsNullOrEmpty(sourceLine)) return sourceLine;

        var chars = sourceLine.ToCharArray();
        var inString = false;

        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] == '"')
            {
                if (inString && i + 1 < chars.Length && chars[i + 1] == '"')
                {
                    chars[i] = '*';
                    chars[i + 1] = '*';
                    i++;
                    continue;
                }

                inString = !inString;
                continue;
            }

            if (inString)
                chars[i] = '*';
        }

        return new string(chars);
    }
}
