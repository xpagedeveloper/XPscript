using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ImageObjectPreprocessor
{
    private static readonly string[] Members =
    [
        "Width", "Height", "Format", "DpiX", "DpiY", "Clone", "Resize", "Crop", "Rotate",
        "FlipHorizontal", "FlipVertical", "Grayscale", "Invert", "Blur", "Sharpen",
        "AutoOrient", "StripMetadata", "ToBytes", "Save", "Dispose", "Pad", "Composite", "Opacity", "Flatten"
    ];

    public string Transform(string source)
    {
        var codeOnly = PreprocessorFeatureGate.CodeOnly(source);
        if (!PreprocessorFeatureGate.ContainsTypeReference(codeOnly, "XPImage")) return source;

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 8);
        var images = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();

            var dimNew = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+XPImage\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNew.Success)
            {
                var name = dimNew.Groups[1].Value;
                images.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                var args = dimNew.Groups[2].Value.Trim();
                output.Add(indent + $"{name} = new XPImage({args})");
                continue;
            }

            var dim = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+XPImage\s*$", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                images.Add(dim.Groups[1].Value);
                output.Add(indent + $"Dim {dim.Groups[1].Value} As Variant");
                continue;
            }

            var rewritten = Regex.Replace(line, @"\bNew\s+XPImage\s*\((.*)\)", m => $"new XPImage({m.Groups[1].Value})", RegexOptions.IgnoreCase);
            foreach (var image in images.OrderByDescending(x => x.Length))
            {
                var escaped = Regex.Escape(image);
                rewritten = Regex.Replace(rewritten, $@"\b{escaped}\s*\.\s*([A-Za-z_]\w*)", m =>
                {
                    var member = Members.FirstOrDefault(x => x.Equals(m.Groups[1].Value, StringComparison.OrdinalIgnoreCase));
                    return image + "." + (member ?? m.Groups[1].Value);
                }, RegexOptions.IgnoreCase);
            }

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && (images.Contains(set.Groups[1].Value) || set.Groups[2].Value.Contains("XPImage.", StringComparison.OrdinalIgnoreCase) || set.Groups[2].Value.Contains("new XPImage", StringComparison.Ordinal)))
                rewritten = set.Groups[1].Value + " = " + set.Groups[2].Value;

            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }
}
