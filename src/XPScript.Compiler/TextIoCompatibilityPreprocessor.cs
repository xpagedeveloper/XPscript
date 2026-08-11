using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class TextIoCompatibilityPreprocessor
{
    private readonly HashSet<string> _specialFileNumbers = new(StringComparer.OrdinalIgnoreCase);

    public string Transform(string source)
    {
        _specialFileNumbers.Clear();
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length);

        // First pass records file numbers opened with Charset and/or Encoding options.
        foreach (var raw in lines)
        {
            if (TryParseSpecialOpen(raw.Trim(), out var open))
                _specialFileNumbers.Add(NormalizeFileNumber(open.FileNumber));
        }

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();
            if (line.Length == 0)
            {
                output.Add(raw);
                continue;
            }

            if (TryParseSpecialOpen(line, out var open))
            {
                output.Add(indent +
                    $"Call XPScriptTextIO.OpenText({open.Path}, \"{open.Mode.ToLowerInvariant()}\", {open.FileNumber}, {open.Charset}, {open.Encoding})");
                continue;
            }

            var close = Regex.Match(line, @"^Close\s+#?(.+?)\s*$", RegexOptions.IgnoreCase);
            if (close.Success && IsSpecialFile(close.Groups[1].Value))
            {
                output.Add(indent + $"Call XPScriptTextIO.CloseFile({close.Groups[1].Value})");
                continue;
            }

            var filePrint = Regex.Match(line, @"^Print\s+#([^,]+)\s*,\s*(.*)$", RegexOptions.IgnoreCase);
            if (filePrint.Success && IsSpecialFile(filePrint.Groups[1].Value))
            {
                output.Add(indent + $"Call XPScriptTextIO.PrintFile({filePrint.Groups[1].Value}, {filePrint.Groups[2].Value})");
                continue;
            }

            var fileWrite = Regex.Match(line, @"^Write\s+#([^,]+)\s*,\s*(.*)$", RegexOptions.IgnoreCase);
            if (fileWrite.Success && IsSpecialFile(fileWrite.Groups[1].Value))
            {
                output.Add(indent + $"Call XPScriptTextIO.WriteFile({fileWrite.Groups[1].Value}, {fileWrite.Groups[2].Value})");
                continue;
            }

            var lineInput = Regex.Match(line, @"^Line\s+Input\s+#([^,]+)\s*,\s*([A-Za-z_]\w*)$", RegexOptions.IgnoreCase);
            if (lineInput.Success && IsSpecialFile(lineInput.Groups[1].Value))
            {
                output.Add(indent + $"{lineInput.Groups[2].Value} = XPScriptTextIO.LineInput({lineInput.Groups[1].Value})");
                continue;
            }

            var fileInput = Regex.Match(line, @"^Input\s+#([^,]+)\s*,\s*([A-Za-z_]\w*)$", RegexOptions.IgnoreCase);
            if (fileInput.Success && IsSpecialFile(fileInput.Groups[1].Value))
            {
                output.Add(indent + $"{fileInput.Groups[2].Value} = XPScriptTextIO.InputFile({fileInput.Groups[1].Value})");
                continue;
            }

            // Console Print/Print$ aliases. Bare Print emits an empty line.
            if (Regex.IsMatch(line, @"^Print\$?\s*$", RegexOptions.IgnoreCase))
            {
                output.Add(indent + "Print \"\"");
                continue;
            }
            var printDollar = Regex.Match(line, @"^Print\$\s+(.+)$", RegexOptions.IgnoreCase);
            if (printDollar.Success)
            {
                output.Add(indent + "Print " + printDollar.Groups[1].Value);
                continue;
            }
            var printDollarCall = Regex.Match(line, @"^Print\$\s*\((.*)\)\s*$", RegexOptions.IgnoreCase);
            if (printDollarCall.Success)
            {
                output.Add(indent + "Print " + printDollarCall.Groups[1].Value);
                continue;
            }

            // Console Input/Input$: Input variable, or Input prompt, variable.
            // File input (Input #n, ...) is deliberately excluded.
            var consoleInput = Regex.Match(line, @"^Input\$?\s+(.+?)\s*,\s*([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
            if (consoleInput.Success && !consoleInput.Groups[1].Value.TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                output.Add(indent + $"{consoleInput.Groups[2].Value} = XPScriptTextIO.ConsoleInput({consoleInput.Groups[1].Value})");
                continue;
            }
            var consoleInputNoPrompt = Regex.Match(line, @"^Input\$?\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
            if (consoleInputNoPrompt.Success)
            {
                output.Add(indent + $"{consoleInputNoPrompt.Groups[1].Value} = XPScriptTextIO.ConsoleInput()");
                continue;
            }

            if (Regex.IsMatch(line, @"^Pause$", RegexOptions.IgnoreCase))
            {
                output.Add(indent + "Call XPScriptTextIO.Pause()");
                continue;
            }

            // New standalone string helpers are normal functions at the XPScript surface.
            var transformed = Regex.Replace(line, @"(?<![\w.])ToBase64\$?\s*\(", "XPScriptTextIO.ToBase64(", RegexOptions.IgnoreCase);
            transformed = Regex.Replace(transformed, @"(?<![\w.])FromBase64\$?\s*\(", "XPScriptTextIO.FromBase64(", RegexOptions.IgnoreCase);
            transformed = Regex.Replace(transformed, @"(?<![\w.])UrlEncode\$?\s*\(", "XPScriptTextIO.UrlEncode(", RegexOptions.IgnoreCase);
            transformed = Regex.Replace(transformed, @"(?<![\w.])UrlDecode\$?\s*\(", "XPScriptTextIO.UrlDecode(", RegexOptions.IgnoreCase);

            foreach (var fileNo in _specialFileNumbers)
            {
                var escaped = Regex.Escape(fileNo);
                transformed = Regex.Replace(
                    transformed,
                    $@"(?<![\w.])EOF\s*\(\s*{escaped}\s*\)",
                    $"XPScriptTextIO.EOF({fileNo})",
                    RegexOptions.IgnoreCase);
            }

            output.Add(indent + transformed);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static bool TryParseSpecialOpen(string line, out SpecialOpen open)
    {
        open = default!;
        var match = Regex.Match(
            line,
            @"^Open\s+(.+?)\s+For\s+(Input|Output|Append)\s+As\s+#?([^\s]+)(.*)$",
            RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        var tail = match.Groups[4].Value.Trim();
        if (tail.Length == 0) return false;

        var charsetMatch = Regex.Match(tail, @"(?:^|\s)Charset\s+(""[^""]*""|[^\s]+)", RegexOptions.IgnoreCase);
        var encodingMatch = Regex.Match(tail, @"(?:^|\s)Encoding\s+(""[^""]*""|[^\s]+)", RegexOptions.IgnoreCase);
        if (!charsetMatch.Success && !encodingMatch.Success) return false;

        var remainder = tail;
        if (charsetMatch.Success) remainder = remainder.Replace(charsetMatch.Value.Trim(), "", StringComparison.OrdinalIgnoreCase).Trim();
        if (encodingMatch.Success) remainder = remainder.Replace(encodingMatch.Value.Trim(), "", StringComparison.OrdinalIgnoreCase).Trim();
        if (remainder.Length != 0)
            throw new CompilerException("Unsupported Open text option(s): " + remainder);

        open = new SpecialOpen(
            match.Groups[1].Value,
            match.Groups[2].Value,
            match.Groups[3].Value,
            charsetMatch.Success ? charsetMatch.Groups[1].Value : "\"default\"",
            encodingMatch.Success ? encodingMatch.Groups[1].Value : "\"none\"");
        return true;
    }

    private bool IsSpecialFile(string value) => _specialFileNumbers.Contains(NormalizeFileNumber(value));

    private static string NormalizeFileNumber(string value) =>
        Regex.Replace(value.Trim().TrimStart('#'), @"\s+", "");

    private sealed record SpecialOpen(string Path, string Mode, string FileNumber, string Charset, string Encoding);
}
