using System.Text;

namespace XPScript.Compiler;

public sealed class ServerSideMetadataPreprocessor
{
    public string Transform(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new StringBuilder(source.Length);
        foreach (var line in lines)
        {
            if (line.Trim().Equals("[ServerSide]", StringComparison.OrdinalIgnoreCase))
                output.AppendLine();
            else
                output.AppendLine(line);
        }
        return output.ToString().TrimEnd('\r', '\n');
    }
}