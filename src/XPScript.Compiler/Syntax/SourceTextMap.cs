namespace XPScript.Compiler.Syntax;

public readonly record struct SourcePosition(int Line, int Column);

public static class SourceTextMap
{
    public static SourcePosition GetPosition(string source, int offset)
    {
        source ??= string.Empty;
        offset = Math.Clamp(offset, 0, source.Length);

        var line = 1;
        var column = 1;
        for (var i = 0; i < offset; i++)
        {
            if (source[i] == '\r')
            {
                if (i + 1 < offset && source[i + 1] == '\n')
                    i++;
                line++;
                column = 1;
            }
            else if (source[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return new SourcePosition(line, column);
    }
}
