using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

public sealed class ServerSideMetadataPreprocessor
{
    private static readonly Regex ServerSideAttribute = new(
        @"^\[ServerSide(?:\s*\(.*\))?\s*\]$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public string Transform(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new StringBuilder(source.Length);
        foreach (var line in lines)
        {
            // [ServerSide] is compiler metadata. Outside the browser-WASM bridge
            // it is deliberately inert, including parameterized forms such as
            // [ServerSide(SpinnerDelay=1000)]. Browser-WASM reads and validates
            // the raw attribute before this generic preprocessing step.
            if (ServerSideAttribute.IsMatch(line.Trim()))
                output.AppendLine();
            else
                output.AppendLine(line);
        }
        return output.ToString().TrimEnd('\r', '\n');
    }
}