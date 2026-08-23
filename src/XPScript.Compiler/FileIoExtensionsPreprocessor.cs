using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class FileIoExtensionsPreprocessor
{
    private sealed record OpenInfo(string Mode, string? RecordLength);

    public string Transform(string source)
    {
        var opens = CollectOpenFiles(source);
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length);

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();
            if (line.Length == 0) { output.Add(raw); continue; }

            // LotusScript/VB-compatible Reset closes every currently open file number.
            // Reuse the existing bare Close semantics so all writer buffers are flushed and
            // both sequential and Binary/Random handles are released by LSFileRuntime.
            if (Regex.IsMatch(line, @"^Reset$", RegexOptions.IgnoreCase))
            {
                output.Add(indent + "Close");
                continue;
            }

            var lockMatch = Regex.Match(line, @"^(Lock|Unlock)\s+#?([^,\s]+)(?:\s*,\s*(.+))?$", RegexOptions.IgnoreCase);
            if (lockMatch.Success)
            {
                var operation = lockMatch.Groups[1].Value.Equals("Lock", StringComparison.OrdinalIgnoreCase) ? "Lock" : "Unlock";
                var fileNo = lockMatch.Groups[2].Value;
                var range = lockMatch.Groups[3].Success ? lockMatch.Groups[3].Value.Trim() : "";
                opens.TryGetValue(NormalizeFileNumber(fileNo), out var open);

                if (range.Length == 0 || open?.Mode is "input" or "output" or "append")
                {
                    output.Add(indent + $"Call XPScriptFileIO.{operation}File({fileNo})");
                    continue;
                }

                var to = Regex.Match(range, @"^(.+?)\s+To\s+(.+)$", RegexOptions.IgnoreCase);
                var start = to.Success ? to.Groups[1].Value.Trim() : range;
                var end = to.Success ? to.Groups[2].Value.Trim() : range;

                if (open?.Mode == "random" && !string.IsNullOrWhiteSpace(open.RecordLength))
                    output.Add(indent + $"Call XPScriptFileIO.{operation}Records({fileNo}, {start}, {end}, {open.RecordLength})");
                else
                    output.Add(indent + $"Call XPScriptFileIO.{operation}Bytes({fileNo}, {start}, {end})");
                continue;
            }

            var chDrive = Regex.Match(line, @"^ChDrive\s+(.+)$", RegexOptions.IgnoreCase);
            if (chDrive.Success)
            {
                output.Add(indent + $"Call XPScriptFileIO.ChDrive({chDrive.Groups[1].Value})");
                continue;
            }

            var transformed = Regex.Replace(
                line,
                @"(?<![\w.])Input\$\s*\(\s*(?<count>[^,()]+)\s*,\s*#\s*(?<file>[^)]+)\)",
                "XPScriptFileIO.InputChars(${count}, ${file})",
                RegexOptions.IgnoreCase);

            output.Add(indent + transformed);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static Dictionary<string, OpenInfo> CollectOpenFiles(string source)
    {
        var result = new Dictionary<string, OpenInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = StripComment(raw).Trim();
            var match = Regex.Match(line,
                @"^Open\s+.+?\s+For\s+(Input|Output|Append|Binary|Random)\s+As\s+#?([^\s]+)(?<tail>.*)$",
                RegexOptions.IgnoreCase);
            if (!match.Success) continue;
            var mode = match.Groups[1].Value.ToLowerInvariant();
            string? recordLength = null;
            if (mode == "random")
            {
                var len = Regex.Match(match.Groups["tail"].Value, @"\bLen\s*=\s*([^\s]+)", RegexOptions.IgnoreCase);
                if (len.Success) recordLength = len.Groups[1].Value;
            }
            result[NormalizeFileNumber(match.Groups[2].Value)] = new OpenInfo(mode, recordLength);
        }
        return result;
    }

    private static string NormalizeFileNumber(string value) => Regex.Replace(value.Trim().TrimStart('#'), @"\s+", "");

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                inString = !inString;
            }
            else if (!inString && line[i] == '\'') return line[..i];
        }
        return line;
    }
}
