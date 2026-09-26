using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ImageObjectPreprocessor
{
    private static readonly string[] Members =
    [
        "Width", "Height", "Format", "DpiX", "DpiY", "Clone", "Resize", "Crop", "Rotate",
        "FlipHorizontal", "FlipVertical", "Brightness", "Contrast", "Saturation", "Grayscale", "Invert", "Blur", "Sharpen",
        "AutoOrient", "GetExif", "SetExif", "GetMetadata", "SetMetadata", "StripMetadata", "ToBytes", "Save", "Recycle", "Pad", "Composite", "Opacity", "Flatten"
    ];

    public string Transform(string source)
    {
        var codeOnly = PreprocessorFeatureGate.CodeOnly(source);
        if (!PreprocessorFeatureGate.ContainsTypeReference(codeOnly, "XPImage")) return source;

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 8);
        var images = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scopeImages = new Stack<HashSet<string>>();
        var scopeNames = new Stack<string>();

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();

            var scopeStart = Regex.Match(line, @"^(?:Public\s+|Private\s+)?(?:Sub|Function)\s+([A-Za-z_]\w*)\b", RegexOptions.IgnoreCase);
            if (scopeStart.Success)
            {
                scopeImages.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                scopeNames.Push(scopeStart.Groups[1].Value);
                output.Add(raw);
                continue;
            }

            var scopeEnd = Regex.Match(line, @"^End\s+(?:Sub|Function)\s*$", RegexOptions.IgnoreCase);
            if (scopeEnd.Success && scopeImages.Count > 0)
            {
                foreach (var image in scopeImages.Pop().OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    output.Add(indent + $"XPImage.RecycleOwned({image})");
                if (scopeNames.Count > 0) scopeNames.Pop();
                output.Add(raw);
                continue;
            }

            var exitScope = Regex.Match(line, @"^Exit\s+(?:Sub|Function)\s*$", RegexOptions.IgnoreCase);
            if (exitScope.Success && scopeImages.Count > 0)
            {
                foreach (var image in scopeImages.Peek().OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    output.Add(indent + $"XPImage.RecycleOwned({image})");
                output.Add(raw);
                continue;
            }

            var dimNew = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+XPImage\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNew.Success)
            {
                var name = dimNew.Groups[1].Value;
                images.Add(name);
                if (scopeImages.Count > 0) scopeImages.Peek().Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                var args = dimNew.Groups[2].Value.Trim();
                output.Add(indent + $"{name} = new XPImage({args})");
                continue;
            }

            var dim = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+XPImage\s*$", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                images.Add(dim.Groups[1].Value);
                if (scopeImages.Count > 0) scopeImages.Peek().Add(dim.Groups[1].Value);
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

            var nullAssignment = Regex.Match(rewritten, @"^([A-Za-z_]\w*)\s*=\s*(?:Nothing|Null|null)\s*$", RegexOptions.IgnoreCase);
            if (nullAssignment.Success && images.Contains(nullAssignment.Groups[1].Value))
            {
                var name = nullAssignment.Groups[1].Value;
                output.Add(indent + $"XPImage.RecycleOwned({name})");
                output.Add(indent + $"{name} = Nothing");
                continue;
            }

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && scopeImages.Count > 0 && scopeNames.Count > 0 && set.Groups[1].Value.Equals(scopeNames.Peek(), StringComparison.OrdinalIgnoreCase))
            {
                var returned = Regex.Match(set.Groups[2].Value.Trim(), @"^[A-Za-z_]\w*$");
                if (returned.Success) scopeImages.Peek().Remove(returned.Value);
            }
            if (set.Success && images.Contains(set.Groups[1].Value))
            {
                var name = set.Groups[1].Value;
                output.Add(indent + $"XPImage.RecycleOwned({name})");
            }
            if (set.Success && (images.Contains(set.Groups[1].Value) || set.Groups[2].Value.Contains("XPImage.", StringComparison.OrdinalIgnoreCase) || set.Groups[2].Value.Contains("new XPImage", StringComparison.Ordinal)))
                rewritten = set.Groups[1].Value + " = " + set.Groups[2].Value;

            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }
}
