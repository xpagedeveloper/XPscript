using System.Text;
using System.Text.RegularExpressions;

namespace LSLite.Compiler;

internal sealed class TextIoCompatibilityPreprocessor
{
    private readonly HashSet<string> _encodedFileNumbers = new(StringComparer.OrdinalIgnoreCase);

    public string Transform(string source)
    {
        _encodedFileNumbers.Clear();
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length);

        // First pass records file-number expressions used by charset-aware text files.
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            var open = Regex.Match(
                line,
                @"^Open\s+(.+?)\s+For\s+(Input|Output|Append)\s+As\s+#?(.+?)\s+Charset\s+(.+?)\s*$",
                RegexOptions.IgnoreCase);
            if (open.Success)
                _encodedFileNumbers.Add(NormalizeFileNumber(open.Groups[3].Value));
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

            var open = Regex.Match(
                line,
                @"^Open\s+(.+?)\s+For\s+(Input|Output|Append)\s+As\s+#?(.+?)\s+Charset\s+(.+?)\s*$",
                RegexOptions.IgnoreCase);
            if (open.Success)
            {
                output.Add(indent + $"Call LSLiteTextIO.OpenText({open.Groups[1].Value}, \"{open.Groups[2].Value.ToLowerInvariant()}\", {open.Groups[3].Value}, {open.Groups[4].Value})");
                continue;
            }

            var close = Regex.Match(line, @"^Close\s+#?(.+?)\s*$", RegexOptions.IgnoreCase);
            if (close.Success && IsEncodedFile(close.Groups[1].Value))
            {
                output.Add(indent + $"Call LSLiteTextIO.CloseFile({close.Groups[1].Value})");
                continue;
            }

            var filePrint = Regex.Match(line, @"^Print\s+#([^,]+)\s*,\s*(.*)$", RegexOptions.IgnoreCase);
            if (filePrint.Success && IsEncodedFile(filePrint.Groups[1].Value))
            {
                output.Add(indent + $"Call LSLiteTextIO.PrintFile({filePrint.Groups[1].Value}, {filePrint.Groups[2].Value})");
                continue;
            }

            var fileWrite = Regex.Match(line, @"^Write\s+#([^,]+)\s*,\s*(.*)$", RegexOptions.IgnoreCase);
            if (fileWrite.Success && IsEncodedFile(fileWrite.Groups[1].Value))
            {
                output.Add(indent + $"Call LSLiteTextIO.WriteFile({fileWrite.Groups[1].Value}, {fileWrite.Groups[2].Value})");
                continue;
            }

            var lineInput = Regex.Match(line, @"^Line\s+Input\s+#([^,]+)\s*,\s*([A-Za-z_]\w*)$", RegexOptions.IgnoreCase);
            if (lineInput.Success && IsEncodedFile(lineInput.Groups[1].Value))
            {
                output.Add(indent + $"{lineInput.Groups[2].Value} = LSLiteTextIO.LineInput({lineInput.Groups[1].Value})");
                continue;
            }

            var fileInput = Regex.Match(line, @"^Input\s+#([^,]+)\s*,\s*([A-Za-z_]\w*)$", RegexOptions.IgnoreCase);
            if (fileInput.Success && IsEncodedFile(fileInput.Groups[1].Value))
            {
                output.Add(indent + $"{fileInput.Groups[2].Value} = LSLiteTextIO.InputFile({fileInput.Groups[1].Value})");
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
                output.Add(indent + $"{consoleInput.Groups[2].Value} = LSLiteTextIO.ConsoleInput({consoleInput.Groups[1].Value})");
                continue;
            }
            var consoleInputNoPrompt = Regex.Match(line, @"^Input\$?\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
            if (consoleInputNoPrompt.Success)
            {
                output.Add(indent + $"{consoleInputNoPrompt.Groups[1].Value} = LSLiteTextIO.ConsoleInput()");
                continue;
            }

            if (Regex.IsMatch(line, @"^Pause$", RegexOptions.IgnoreCase))
            {
                output.Add(indent + "Call LSLiteTextIO.Pause()");
                continue;
            }

            // New standalone string helpers are normal functions at the LS Lite surface.
            var transformed = Regex.Replace(line, @"(?<![\w.])ToBase64\$?\s*\(", "LSLiteTextIO.ToBase64(", RegexOptions.IgnoreCase);
            transformed = Regex.Replace(transformed, @"(?<![\w.])FromBase64\$?\s*\(", "LSLiteTextIO.FromBase64(", RegexOptions.IgnoreCase);
            transformed = Regex.Replace(transformed, @"(?<![\w.])UrlEncode\$?\s*\(", "LSLiteTextIO.UrlEncode(", RegexOptions.IgnoreCase);
            transformed = Regex.Replace(transformed, @"(?<![\w.])UrlDecode\$?\s*\(", "LSLiteTextIO.UrlDecode(", RegexOptions.IgnoreCase);

            foreach (var fileNo in _encodedFileNumbers)
            {
                var escaped = Regex.Escape(fileNo);
                transformed = Regex.Replace(
                    transformed,
                    $@"(?<![\w.])EOF\s*\(\s*{escaped}\s*\)",
                    $"LSLiteTextIO.EOF({fileNo})",
                    RegexOptions.IgnoreCase);
            }

            output.Add(indent + transformed);
        }

        return string.Join(Environment.NewLine, output);
    }

    private bool IsEncodedFile(string value) => _encodedFileNumbers.Contains(NormalizeFileNumber(value));

    private static string NormalizeFileNumber(string value) =>
        Regex.Replace(value.Trim().TrimStart('#'), @"\s+", "");
}
