using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NetworkToolsObjectPreprocessor
{
    private static readonly string[] Types =
    [
        "NetworkTools", "NetworkPingResult", "NetworkTraceHop", "NetworkDnsResult", "NetworkPortResult",
        "NetworkUdpResult", "NetworkHttpResult", "NetworkTlsResult", "NetworkInterfaceInfo", "NetworkEndpointInfo"
    ];

    public string Transform(string source)
    {
        var codeOnly = PreprocessorFeatureGate.CodeOnly(source);
        if (!PreprocessorFeatureGate.ContainsTypeReference(codeOnly, Types)) return source;

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 8);
        var objectVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();

            var dimNew = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+NetworkTools\s*(?:\(\s*\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNew.Success)
            {
                var name = dimNew.Groups[1].Value;
                objectVariables.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                output.Add(indent + $"{name} = XPScriptNetworkToolsFactory.Create()");
                continue;
            }

            var dim = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+(" + string.Join("|", Types.Select(Regex.Escape)) + @")\s*$", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                objectVariables.Add(dim.Groups[1].Value);
                output.Add(indent + $"Dim {dim.Groups[1].Value} As Variant");
                continue;
            }

            var rewritten = Regex.Replace(line, @"\bNew\s+NetworkTools\s*(?:\(\s*\))?", "XPScriptNetworkToolsFactory.Create()", RegexOptions.IgnoreCase);
            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && (objectVariables.Contains(set.Groups[1].Value) || set.Groups[2].Value.Contains("Network", StringComparison.OrdinalIgnoreCase)))
                rewritten = set.Groups[1].Value + " = " + set.Groups[2].Value;

            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }
}
