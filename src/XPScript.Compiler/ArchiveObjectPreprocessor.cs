using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ArchiveObjectPreprocessor
{
    private const string FirstEntryHelper = "XPScriptArchiveIteratorRuntime.GetFirstEntry";
    private const string NextEntryHelper = "XPScriptArchiveIteratorRuntime.GetNextEntry";

    private static readonly string[] ArchiveMembers =
    [
        "Path", "Format", "Exists", "ExtendedSupport", "IsEncrypted", "IsReadOnly", "Password", "CompressionLevel",
        "MaxExtractSize", "MaxEntries", "MaxCompressionRatio", "FileCount", "FolderCount", "CompressedSize", "UncompressedSize",
        "Entries", "Open", "Close", "Save", "Create", "AddFile", "AddFolder", "AddText", "AddBytes", "Remove", "Rename",
        "Contains", "GetEntry", "Files", "Folders", "Find", "ReadText", "ReadBytes", "Extract", "ExtractFolder", "ExtractAll", "ToBytes"
    ];

    private static readonly string[] ArchiveEntryMembers =
    [
        "Name", "FullName", "Extension", "Size", "CompressedSize", "CompressionRatio", "Created", "Modified", "IsEncrypted", "CRC"
    ];

    public string Transform(string source, string sourceName = "input.xps")
    {
        var codeOnly = PreprocessorFeatureGate.CodeOnly(source);
        if (!PreprocessorFeatureGate.ContainsTypeReference(codeOnly, "Archive", "ArchiveEntry")) return source;

        new ArchiveCapabilityValidator().Validate(source, sourceName);

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 16);
        var archiveVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var archiveEntryVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var raw = lines[lineIndex];
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();

            var dimNew = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+Archive\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNew.Success)
            {
                var name = dimNew.Groups[1].Value;
                archiveVariables.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                var args = dimNew.Groups[2].Value.Trim();
                output.Add(indent + $"{name} = {CreateArchiveExpression(args, sourceName, lineIndex + 1, raw)}");
                continue;
            }

            var dim = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+(Archive|ArchiveEntry)\s*$", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                var name = dim.Groups[1].Value;
                if (dim.Groups[2].Value.Equals("Archive", StringComparison.OrdinalIgnoreCase)) archiveVariables.Add(name);
                else archiveEntryVariables.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                continue;
            }

            var forAll = Regex.Match(line, @"^ForAll\s+([A-Za-z_]\w*)\s+In\s+([A-Za-z_]\w*)\s*\.\s*(?:Entries|Files\s*\(\s*\)|Folders\s*\(\s*\))\s*$", RegexOptions.IgnoreCase);
            if (forAll.Success && archiveVariables.Contains(forAll.Groups[2].Value))
                archiveEntryVariables.Add(forAll.Groups[1].Value);

            var rewritten = Regex.Replace(line, @"\bNew\s+Archive\s*(?:\(\s*\))?", "XPScriptArchiveFactory.Create()", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+Archive\s*\((.*)\)", m => CreateArchiveExpression(m.Groups[1].Value, sourceName, lineIndex + 1, raw), RegexOptions.IgnoreCase);

            foreach (var archiveName in archiveVariables.OrderByDescending(x => x.Length))
            {
                var escaped = Regex.Escape(archiveName);

                RewriteChainedEntryAliases(ref rewritten, archiveName, escaped, "GetEntry", @"([^()]*)");
                RewriteChainedEntryAliases(ref rewritten, archiveName, escaped, "GetFirstEntry", @"\s*");
                RewriteChainedEntryAliases(ref rewritten, archiveName, escaped, "GetNextEntry", @"([^()]*)");

                var firstPattern = $@"\b{escaped}\s*\.\s*GetFirstEntry\s*\(\s*\)";
                rewritten = Regex.Replace(rewritten, firstPattern, $"{FirstEntryHelper}({archiveName})", RegexOptions.IgnoreCase);

                var nextPattern = $@"\b{escaped}\s*\.\s*GetNextEntry\s*\(\s*([^()]*)\s*\)";
                rewritten = Regex.Replace(rewritten, nextPattern, m => $"{NextEntryHelper}({archiveName}, {m.Groups[1].Value.Trim()})", RegexOptions.IgnoreCase);

                foreach (var member in ArchiveMembers)
                    rewritten = Regex.Replace(rewritten, $@"\b{escaped}\s*\.\s*{Regex.Escape(member)}\b", $"{archiveName}.{member}", RegexOptions.IgnoreCase);
            }

            foreach (var entryName in archiveEntryVariables.OrderByDescending(x => x.Length))
            {
                var escaped = Regex.Escape(entryName);
                if (Regex.IsMatch(rewritten, $@"\b{escaped}\s*\.\s*IsDirectory\b", RegexOptions.IgnoreCase))
                    throw RemovedIsDirectoryException();

                rewritten = Regex.Replace(rewritten, $@"\b{escaped}\s*\.\s*IsFolder\b", $"{entryName}.IsDirectory", RegexOptions.IgnoreCase);
                rewritten = Regex.Replace(rewritten, $@"\b{escaped}\s*\.\s*IsFile\b", $"(Not {entryName}.IsDirectory)", RegexOptions.IgnoreCase);

                foreach (var member in ArchiveEntryMembers)
                    rewritten = Regex.Replace(rewritten, $@"\b{escaped}\s*\.\s*{Regex.Escape(member)}\b", $"{entryName}.{member}", RegexOptions.IgnoreCase);
            }

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && (archiveVariables.Contains(set.Groups[1].Value)
                || archiveEntryVariables.Contains(set.Groups[1].Value)
                || set.Groups[2].Value.Contains("XPScriptArchive", StringComparison.Ordinal)
                || set.Groups[2].Value.Contains("XPScriptExtendedArchive", StringComparison.Ordinal)
                || set.Groups[2].Value.Contains("XPScriptArchiveIteratorRuntime", StringComparison.Ordinal)))
                rewritten = set.Groups[1].Value + " = " + set.Groups[2].Value;

            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static void RewriteChainedEntryAliases(ref string rewritten, string archiveName, string escapedArchiveName, string methodName, string argumentPattern)
    {
        var method = Regex.Escape(methodName);
        var callPattern = $@"\b{escapedArchiveName}\s*\.\s*{method}\s*\(\s*{argumentPattern}\s*\)";

        if (Regex.IsMatch(rewritten, callPattern + @"\s*\.\s*IsDirectory\b", RegexOptions.IgnoreCase))
            throw RemovedIsDirectoryException();

        rewritten = Regex.Replace(
            rewritten,
            callPattern + @"\s*\.\s*IsFolder\b",
            m => m.Value[..m.Value.LastIndexOf('.')] + ".IsDirectory",
            RegexOptions.IgnoreCase);

        rewritten = Regex.Replace(
            rewritten,
            callPattern + @"\s*\.\s*IsFile\b",
            m => "(Not " + m.Value[..m.Value.LastIndexOf('.')] + ".IsDirectory)",
            RegexOptions.IgnoreCase);
    }

    private static CompilerException RemovedIsDirectoryException() =>
        new("ArchiveEntry.IsDirectory is not available. Use ArchiveEntry.IsFile or ArchiveEntry.IsFolder.");

    private static string CreateArchiveExpression(string rawArguments, string sourceName, int line, string sourceLine)
    {
        var args = SplitArguments(rawArguments);
        if (args.Count == 0) return "XPScriptArchiveFactory.Create()";
        if (args.Count == 1) return $"XPScriptArchiveFactory.Create({args[0]})";
        if (args.Count != 2)
            throw SyntaxFailure(sourceName, line, sourceLine, rawArguments, "Archive(filenameOrBytes [, extendedSupport])");

        var extended = args[1].Trim();
        if (extended.Equals("True", StringComparison.OrdinalIgnoreCase))
            return $"XPScriptExtendedArchiveWriterFactory.Create({args[0]})";
        if (extended.Equals("False", StringComparison.OrdinalIgnoreCase))
            return $"XPScriptArchiveFactory.Create({args[0]}, false)";

        throw SyntaxFailure(sourceName, line, sourceLine, extended, "True or False literal");
    }

    private static CompilerException SyntaxFailure(string sourceName, int line, string sourceLine, string found, string expected)
    {
        var safeSource = CompilerDiagnosticRedaction.MaskStringLiterals(sourceLine).TrimEnd();
        var diagnostic = new CompileDiagnostic
        {
            File = Path.GetFileName(sourceName), Line = line, Position = 1, EndLine = line, EndColumn = Math.Max(1, safeSource.Length + 1),
            Description = $"Invalid Archive constructor syntax; expected {expected}.", DiagnosticCode = CompilerDiagnosticCodes.InvalidSyntax, Category = "syntax",
            Properties = [new() { Name = "foundToken", Value = found }, new() { Name = "expectedConstruct", Value = expected }], SourceCode = safeSource
        };
        return new CompilerException(diagnostic.Description, diagnostic.DiagnosticCode, diagnostic.Category, [diagnostic]);
    }

    private static List<string> SplitArguments(string value)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(value)) return result;
        var start = 0;
        var depth = 0;
        var inString = false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                if (inString && i + 1 < value.Length && value[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (inString) continue;
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(value[start..i].Trim());
                start = i + 1;
            }
        }
        result.Add(value[start..].Trim());
        return result;
    }
}
