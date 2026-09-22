using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal static class CompilerDiagnosticParser
{
    private const string GeneratedMarker = "XPSCRIPT-GENERATED-DIAGNOSTIC|";

    public static List<CompileDiagnostic> Parse(string message, string sourcePath, string source, bool debug, string diagnosticCode = "", string category = "", SourceMap? sourceMap = null)
    {
        debug = debug || CompilerDiagnosticMode.Debug;
        var result = new List<CompileDiagnostic>();
        var escapedSource = Regex.Escape(sourcePath).Replace("\\\\", @"[\\/]");
        var sourcePattern = new Regex(
            $@"(?<file>{escapedSource}|[^\r\n]*\.xps)\((?<line>\d+)(?:,(?<pos>\d+))?\):\s*(?:(?<severity>error|warning|info)\s+(?<id>[A-Za-z]+\d+):\s*)?(?<desc>[^\r\n]+)",
            RegexOptions.IgnoreCase);

        foreach (Match match in sourcePattern.Matches(message))
        {
            var line = int.Parse(match.Groups["line"].Value);
            var pos = match.Groups["pos"].Success ? int.Parse(match.Groups["pos"].Value) : 1;
            var diagnosticSource = NormalizeDiagnosticSourcePath(match.Groups["file"].Value);
            var upstreamCode = match.Groups["id"].Value;
            var description = match.Groups["desc"].Value.Trim();
            var mapped = RecoverIncludeLocation(sourcePath, diagnosticSource, line, description, sourceMap);
            if (mapped is not null)
            {
                diagnosticSource = mapped.SourcePath;
                line = mapped.Line;
                pos = mapped.Position;
            }
            var code = DiagnosticSourceLine(sourcePath, source, diagnosticSource, line);
            var classification = CompilerDiagnosticClassifier.ClassifyUpstream(upstreamCode, mapped is not null || CompilerDiagnosticClassifier.IsSourceMappedPath(diagnosticSource));
            if (classification.DiagnosticCode is CompilerDiagnosticCodes.UnknownSymbol or CompilerDiagnosticCodes.UnknownMember)
                code = RecoverDiagnosticSourceLine(sourcePath, source, diagnosticSource, code, description, upstreamCode);
            result.Add(new CompileDiagnostic
            {
                File = DiagnosticFileName(diagnosticSource),
                Line = line,
                Position = pos,
                Description = Humanize(description),
                SourceCode = code,
                MarkedCode = Mark(code, pos),
                DiagnosticCode = string.IsNullOrWhiteSpace(diagnosticCode) ? classification.DiagnosticCode ?? "" : diagnosticCode,
                UpstreamCode = upstreamCode,
                Severity = match.Groups["severity"].Success ? match.Groups["severity"].Value : "error",
                Category = !string.IsNullOrWhiteSpace(category)
                    ? category
                    : classification.Category ?? "compiler",
                Properties = SourceMappedProperties(upstreamCode, description, code, pos),
                IncludeTrace = DiagnosticIncludeTrace(sourcePath, diagnosticSource, line)
            });
        }

        if (debug)
            AddGeneratedDiagnostics(message, result);

        if (result.Count > 0)
            return result
                .GroupBy(x => (x.File, x.Line, x.Position, x.Description, x.DiagnosticCode, x.UpstreamCode, x.Severity, x.Category))
                .Select(x => x.First())
                .ToList();

        return
        [
            new CompileDiagnostic
            {
                File = DiagnosticFileName(sourcePath),
                Description = debug
                    ? FirstDiagnosticLine(message)
                    : "Compilation failed. Use --debug to show generated C# diagnostics.",
                DiagnosticCode = string.IsNullOrWhiteSpace(diagnosticCode) ? CompilerDiagnosticCodes.CompilationFailed : diagnosticCode,
                Category = string.IsNullOrWhiteSpace(category) ? "compiler" : category
            }
        ];
    }

    private static void AddGeneratedDiagnostics(string message, List<CompileDiagnostic> result)
    {
        var markerPattern = new Regex(
            @"^XPSCRIPT-GENERATED-DIAGNOSTIC\|(?<file>[^|\r\n]+)\|(?<line>\d+)\|(?<pos>\d+)\|(?<id>CS\d+)\|(?<desc>[^\r\n]*)$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        foreach (Match match in markerPattern.Matches(message))
        {
            result.Add(new CompileDiagnostic
            {
                File = DiagnosticFileName(match.Groups["file"].Value),
                Line = int.Parse(match.Groups["line"].Value),
                Position = int.Parse(match.Groups["pos"].Value),
                Description = Humanize(match.Groups["desc"].Value.Trim()),
                UpstreamCode = match.Groups["id"].Value,
                Category = "code-generation"
            });
        }

        var generatedPattern = new Regex(
            @"(?:^|[\\/])?(?<file>Program\.cs)\((?<line>\d+),(?<pos>\d+)\):\s*error\s+(?<id>CS\d+):\s*(?<desc>.*?)(?:\s*\[|$)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        foreach (Match match in generatedPattern.Matches(message))
        {
            result.Add(new CompileDiagnostic
            {
                File = "Program.cs",
                Line = int.Parse(match.Groups["line"].Value),
                Position = int.Parse(match.Groups["pos"].Value),
                Description = Humanize(match.Groups["desc"].Value.Trim()),
                UpstreamCode = match.Groups["id"].Value,
                Category = "code-generation"
            });
        }
    }

    private sealed record RecoveredIncludeLocation(string SourcePath, int Line, int Position);

    private static RecoveredIncludeLocation? RecoverIncludeLocation(string rootSourcePath, string diagnosticSourcePath, int reportedLine, string description, SourceMap? sourceMap)
    {
        if (!IsRootDiagnosticSource(rootSourcePath, diagnosticSourcePath)) return null;
        var context = ExpandedSourceContext.Current;
        var map = sourceMap ?? context?.Map;
        if (map is null) return null;

        // Fallback for generated diagnostics whose #line mapping was lost: match the
        // diagnostic's source expression against physical included source lines.
        var quoted = Regex.Matches(description, @"'(?<value>[^']+)'")
            .Select(m => m.Groups["value"].Value)
            .Where(v => v.Length > 0)
            .ToArray();

        var candidates = Enumerable.Range(1, map.Count)
            .Select(index => map.Resolve(index, rootSourcePath))
            .Where(location => location.IncludeTrace is { Count: > 0 })
            .Where(location => quoted.Length == 0 || quoted.Any(value => location.SourceText.Contains(value, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (candidates.Length != 1) return null;

        var candidate = candidates[0];
        var position = 1;
        foreach (var value in quoted)
        {
            var index = candidate.SourceText.IndexOf(value, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) { position = index + 1; break; }
        }
        return new RecoveredIncludeLocation(candidate.SourcePath, candidate.Line, position);
    }

    private static List<CompileIncludeFrame>? DiagnosticIncludeTrace(string rootSourcePath, string diagnosticSourcePath, int line)
    {
        var context = ExpandedSourceContext.Current;
        if (context is null || line <= 0) return null;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var location = Enumerable.Range(1, context.Map.Count)
            .Select(index => context.Map.Resolve(index, rootSourcePath))
            .FirstOrDefault(item =>
            {
                try
                {
                    var diagnosticFull = Path.IsPathRooted(diagnosticSourcePath)
                        ? Path.GetFullPath(diagnosticSourcePath)
                        : Path.GetFullPath(diagnosticSourcePath, Path.GetDirectoryName(context.SourcePath) ?? Environment.CurrentDirectory);
                    return string.Equals(Path.GetFullPath(item.SourcePath), diagnosticFull, comparison) && item.Line == line;
                }
                catch { return false; }
            });
        if (location?.IncludeTrace is not { Count: > 0 } trace) return null;
        return trace.Select(frame => new CompileIncludeFrame
        {
            File = Path.GetFileName(frame.SourcePath),
            Line = frame.Line,
            IncludedFile = Path.GetFileName(frame.IncludedPath)
        }).ToList();
    }

    private static List<CompileDiagnosticProperty>? SourceMappedProperties(string upstreamCode, string description, string sourceLine, int position)
    {
        var normalizedCode = upstreamCode.Trim().ToUpperInvariant();
        var propertyName = normalizedCode switch
        {
            "CS0103" => "symbol",
            "CS1061" or "CS0117" => "member",
            _ => null
        };
        if (propertyName is null) return null;

        var quotedIdentifiers = Regex.Matches(description, @"'(?<identifier>[A-Za-z_]\w*)'")
            .Select(match => match.Groups["identifier"].Value)
            .ToArray();
        if (quotedIdentifiers.Length > 0)
        {
            var identifier = normalizedCode switch
            {
                "CS1061" when quotedIdentifiers.Length >= 2 => quotedIdentifiers[1],
                "CS0117" when quotedIdentifiers.Length >= 2 => quotedIdentifiers[1],
                _ => quotedIdentifiers[0]
            };
            return SymbolProperties(propertyName, identifier);
        }

        if (string.IsNullOrWhiteSpace(sourceLine)) return null;

        // Prefer an explicit member access from the mapped XPScript line. This is more
        // reliable than the generated-C# column, which can point at the receiver/expression.
        if (normalizedCode is "CS1061" or "CS0117")
        {
            var memberAccess = Regex.Matches(sourceLine, @"\.\s*(?<identifier>[A-Za-z_]\w*)")
                .Select(match => match.Groups["identifier"].Value)
                .LastOrDefault();
            if (!string.IsNullOrWhiteSpace(memberAccess))
                return SymbolProperties(propertyName, memberAccess);
        }

        if (position <= 0) return null;
        var suffix = position <= sourceLine.Length ? sourceLine.Substring(position - 1) : "";
        var sourceMatch = Regex.Match(suffix, @"^(?<identifier>[A-Za-z_]\w*)");
        return sourceMatch.Success
            ? SymbolProperties(propertyName, sourceMatch.Groups["identifier"].Value)
            : null;
    }

    private static List<CompileDiagnosticProperty> SymbolProperties(string propertyName, string identifier)
    {
        var properties = new List<CompileDiagnosticProperty>
        {
            new() { Name = propertyName, Value = identifier }
        };
        foreach (var candidate in CompilerSymbolCatalog.Candidates(identifier))
        {
            properties.Add(new CompileDiagnosticProperty { Name = "candidate", Value = candidate.Name });
            properties.Add(new CompileDiagnosticProperty { Name = "candidateKind", Value = candidate.Kind });
            properties.Add(new CompileDiagnosticProperty { Name = "candidateSignature", Value = candidate.Signature });
        }
        return properties;
    }

    private static string RecoverDiagnosticSourceLine(string rootSourcePath, string rootSource, string diagnosticSourcePath, string currentLine, string description, string upstreamCode)
    {
        var properties = SourceMappedProperties(upstreamCode, description, currentLine, 1);
        var identifier = properties?.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(identifier) || currentLine.Contains(identifier, StringComparison.Ordinal))
            return currentLine;

        var rootMatches = rootSource.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Where(line => line.Contains(identifier, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (rootMatches.Length == 1)
            return RedactSourceLine(rootMatches[0]);

        string text;
        if (IsRootDiagnosticSource(rootSourcePath, diagnosticSourcePath))
            text = rootSource;
        else
        {
            try
            {
                var rootDirectory = Path.GetFullPath(Path.GetDirectoryName(Path.GetFullPath(rootSourcePath)) ?? Environment.CurrentDirectory);
                var resolved = Path.IsPathRooted(diagnosticSourcePath)
                    ? Path.GetFullPath(diagnosticSourcePath)
                    : Path.GetFullPath(Path.Combine(rootDirectory, diagnosticSourcePath));
                if (!Path.GetExtension(resolved).Equals(".xps", StringComparison.OrdinalIgnoreCase) || !File.Exists(resolved))
                    return currentLine;
                text = File.ReadAllText(resolved);
            }
            catch { return currentLine; }
        }

        var matches = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Where(line => line.Contains(identifier, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? RedactSourceLine(matches[0]) : currentLine;
    }

    private static string DiagnosticSourceLine(string rootSourcePath, string rootSource, string diagnosticSourcePath, int line)
    {
        if (line <= 0) return "";
        if (IsRootDiagnosticSource(rootSourcePath, diagnosticSourcePath))
            return SourceLine(rootSource, line);

        try
        {
            var rootDirectory = Path.GetFullPath(Path.GetDirectoryName(Path.GetFullPath(rootSourcePath)) ?? Environment.CurrentDirectory);
            var resolved = Path.IsPathRooted(diagnosticSourcePath)
                ? Path.GetFullPath(diagnosticSourcePath)
                : Path.GetFullPath(Path.Combine(rootDirectory, diagnosticSourcePath));
            if (!Path.GetExtension(resolved).Equals(".xps", StringComparison.OrdinalIgnoreCase) || !File.Exists(resolved))
                return "";
            return SourceLine(File.ReadAllText(resolved), line);
        }
        catch
        {
            return "";
        }
    }

    private static bool IsRootDiagnosticSource(string rootSourcePath, string diagnosticSourcePath)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(rootSourcePath, diagnosticSourcePath, comparison)) return true;
        try
        {
            if (Path.IsPathRooted(diagnosticSourcePath))
                return string.Equals(Path.GetFullPath(rootSourcePath), Path.GetFullPath(diagnosticSourcePath), comparison);
        }
        catch { }
        return !Path.IsPathRooted(diagnosticSourcePath) &&
               string.Equals(Path.GetFileName(rootSourcePath), Path.GetFileName(diagnosticSourcePath), comparison);
    }

    private static string SourceLine(string source, int line)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return line > 0 && line <= lines.Length ? RedactSourceLine(lines[line - 1]) : "";
    }

    private static string RedactSourceLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;
        var output = new StringBuilder(line.Length);
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inString && c == '\\' && i + 1 < line.Length && line[i + 1] == '"')
            {
                output.Append("**");
                i++;
                continue;
            }
            if (c == '"')
            {
                output.Append(c);
                if (inString && i + 1 < line.Length && line[i + 1] == '"')
                {
                    output.Append('"');
                    i++;
                    continue;
                }
                inString = !inString;
                continue;
            }
            output.Append(inString ? '*' : c);
        }
        return output.ToString();
    }

    private static string Mark(string code, int position)
    {
        if (string.IsNullOrEmpty(code) || position <= 0) return code;
        var caret = Math.Clamp(position - 1, 0, code.Length);
        return code + Environment.NewLine + new string(' ', caret) + "^";
    }

    private static string NormalizeDiagnosticSourcePath(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0) return normalized;

        // MSBuild/Roslyn output can prepend logger text before the physical source path,
        // especially on macOS runners. Keep the actual .xps path so source-line recovery
        // is independent of the platform-specific build logger format.
        var marker = normalized.LastIndexOf(".xps", StringComparison.OrdinalIgnoreCase);
        if (marker < 0) return normalized;

        normalized = normalized[..(marker + 4)];
        var absoluteUnix = normalized.LastIndexOf(" /", StringComparison.Ordinal);
        var absoluteWindows = normalized.LastIndexOfAny([' ', '\t']);
        var start = Math.Max(absoluteUnix >= 0 ? absoluteUnix + 1 : -1, absoluteWindows >= 0 ? absoluteWindows + 1 : -1);
        if (start > 0)
            normalized = normalized[start..];

        return normalized.Trim();
    }

    private static string DiagnosticFileName(string value)
    {
        try
        {
            var normalized = value.Trim().Replace('\\', '/');
            var marker = normalized.LastIndexOf(".xps", StringComparison.OrdinalIgnoreCase);
            if (marker >= 0)
            {
                var start = normalized.LastIndexOfAny([' ', ':', '[', '('], marker);
                normalized = normalized[(start + 1)..(marker + 4)];
            }
            return Path.GetFileName(normalized);
        }
        catch { return ""; }
    }

    private static string FirstDiagnosticLine(string message) =>
        message.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0 && !line.StartsWith(GeneratedMarker, StringComparison.Ordinal))
        ?? "Compilation failed.";

    private static string Humanize(string description)
    {
        var convert = Regex.Match(description, @"cannot convert from '([^']+)' to '([^']+)'", RegexOptions.IgnoreCase);
        if (convert.Success) return $"Unable to use {FriendlyType(convert.Groups[1].Value)} where {FriendlyType(convert.Groups[2].Value)} is required.";
        var assign = Regex.Match(description, @"Cannot implicitly convert type '([^']+)' to '([^']+)'", RegexOptions.IgnoreCase);
        if (assign.Success) return $"Unable to assign {FriendlyType(assign.Groups[1].Value)} to {FriendlyType(assign.Groups[2].Value)}.";
        return description;
    }

    private static string FriendlyType(string type) => type.Trim() switch
    {
        "string" or "System.String" => "String",
        "int" or "System.Int32" => "Integer",
        "long" or "System.Int64" => "Long",
        "double" or "System.Double" => "Double",
        "float" or "System.Single" => "Single",
        "bool" or "System.Boolean" => "Boolean",
        "byte" or "System.Byte" => "Byte",
        "decimal" or "System.Decimal" => "Currency",
        _ => type
    };
}
