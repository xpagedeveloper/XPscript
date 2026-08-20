using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

public sealed class CompileFolderSourcePreprocessor
{
    public sealed record Result(string Source, IReadOnlyList<string> Dependencies, IReadOnlyList<string> Modules, bool Enabled);

    private static readonly Regex CompilePattern = new(
        @"^\s*\[\s*Compile\s*:\s*(?:\"(?<quoted>[^\"]+)\"|(?<plain>[^\]]+))\s*\]\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IncludePattern = new(
        "^Include\\s+\"([^\"]+)\"\\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MainPattern = new(
        @"^(?<indent>\s*)(?<visibility>(?:Public|Private)\s+)?Sub\s+Main\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ProcedurePattern = new(
        @"^\s*(?:Public\s+|Private\s+)?Sub\s+(?<name>[A-Za-z_]\w*)\s*\(\s*\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DesktopUiPattern = new(
        @"\b(?:UIForm|UIListView|LoadFileDialog|OpenFileDialog|SaveFileDialog)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool ContainsCompileDirective(string source)
        => NormalizeLines(source).Any(line => CompilePattern.IsMatch(StripComment(line).Trim()));

    public static bool IsDesktopProject(string source)
        => DesktopUiPattern.IsMatch(source);

    public Result Transform(string rootSource, string rootSourcePath, bool enableModules)
    {
        ArgumentNullException.ThrowIfNull(rootSource);
        if (string.IsNullOrWhiteSpace(rootSourcePath)) throw new CompilerException("Compile-folder processing requires a source file path.");

        var rootPath = Path.GetFullPath(rootSourcePath);
        var rootDirectory = Path.GetDirectoryName(rootPath) ?? Environment.CurrentDirectory;
        var declarations = ParseCompileDeclarations(rootSource, rootPath);
        var strippedRoot = StripCompileDirectives(rootSource);

        if (declarations.Count == 0)
            return new Result(rootSource, [rootPath], [rootPath], false);

        if (!enableModules)
            return new Result(strippedRoot, [rootPath], [rootPath], false);

        if (declarations.Count != 1)
            throw new CompilerException($"{Path.GetFileName(rootPath)}: A desktop or browser-wasm source may declare exactly one [Compile:folder] rule.");

        var compileRoot = ResolveCompileRoot(rootDirectory, declarations[0]);
        var candidates = Directory.EnumerateFiles(compileRoot, "*.xps", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .OrderBy(path => Path.GetRelativePath(compileRoot, path), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var includedPaths = DiscoverIncludeFiles(candidates, rootPath, rootDirectory);
        var moduleFiles = candidates
            .Where(path => !PathEquals(path, rootPath))
            .Where(path => !includedPaths.Contains(PathKey(path)))
            .ToArray();

        var sourceBuilder = new StringBuilder(strippedRoot.Length + moduleFiles.Length * 512);
        sourceBuilder.AppendLine(strippedRoot.TrimEnd('\r', '\n'));

        var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootPath };
        var modules = new List<ModuleEntry>
        {
            new(rootPath, Path.GetFileName(rootPath), "Main", true)
        };

        foreach (var path in moduleFiles)
        {
            var moduleSource = File.ReadAllText(path);
            dependencies.Add(path);
            var entry = ResolveEntryPoint(moduleSource, path);
            if (entry is not null)
            {
                var generatedName = BuildGeneratedEntryName(Path.GetRelativePath(compileRoot, path));
                moduleSource = RenameProcedure(moduleSource, entry, generatedName);
                modules.Add(new ModuleEntry(path, Path.GetRelativePath(compileRoot, path).Replace('\\', '/'), generatedName, false));
            }

            sourceBuilder.AppendLine();
            sourceBuilder.AppendLine("' ----- compiled module: " + Path.GetRelativePath(compileRoot, path).Replace('\\', '/') + " -----");
            sourceBuilder.AppendLine(moduleSource.TrimEnd('\r', '\n'));
        }

        foreach (var include in includedPaths) dependencies.Add(include);
        AppendNavigationDispatcher(sourceBuilder, rootPath, compileRoot, modules);

        return new Result(
            sourceBuilder.ToString(),
            dependencies.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            modules.Select(x => x.RelativeName).ToArray(),
            true);
    }

    private static List<string> ParseCompileDeclarations(string source, string sourcePath)
    {
        var result = new List<string>();
        var lines = NormalizeLines(source);
        for (var i = 0; i < lines.Length; i++)
        {
            var code = StripComment(lines[i]).Trim();
            var match = CompilePattern.Match(code);
            if (match.Success)
            {
                var value = (match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["plain"].Value).Trim();
                if (value.Length == 0) throw new CompilerException($"{Path.GetFileName(sourcePath)}({i + 1},1): Compile folder cannot be empty.");
                result.Add(value);
                continue;
            }

            if (Regex.IsMatch(code, @"^\[\s*Compile\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                throw new CompilerException($"{Path.GetFileName(sourcePath)}({i + 1},1): Invalid Compile directive. Expected [Compile:folder].");
        }
        return result;
    }

    private static string StripCompileDirectives(string source)
    {
        var lines = NormalizeLines(source);
        for (var i = 0; i < lines.Length; i++)
        {
            if (CompilePattern.IsMatch(StripComment(lines[i]).Trim())) lines[i] = string.Empty;
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string ResolveCompileRoot(string sourceDirectory, string declared)
    {
        var portable = declared.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(portable)) throw new CompilerException("Compile folder must be relative to the main .xps file.");
        var resolved = Path.GetFullPath(Path.Combine(sourceDirectory, portable));
        var relative = Path.GetRelativePath(sourceDirectory, resolved);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new CompilerException("Compile folder must remain inside the main .xps directory.");
        if (!Directory.Exists(resolved)) throw new CompilerException("Compile folder was not found: " + Path.GetFileName(resolved));
        return resolved;
    }

    private static HashSet<string> DiscoverIncludeFiles(IReadOnlyList<string> candidates, string rootPath, string rootDirectory)
    {
        var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Scan(string sourcePath)
        {
            var full = Path.GetFullPath(sourcePath);
            if (!visited.Add(PathKey(full)) || !File.Exists(full)) return;
            var source = File.ReadAllText(full);
            var directory = Path.GetDirectoryName(full) ?? rootDirectory;
            var lines = NormalizeLines(source);
            for (var i = 0; i < lines.Length; i++)
            {
                var code = StripComment(lines[i]).Trim();
                var match = IncludePattern.Match(code);
                if (!match.Success) continue;
                var declared = match.Groups[1].Value.Trim();
                if (!declared.EndsWith(".xps", StringComparison.OrdinalIgnoreCase)) continue;
                var resolved = Path.GetFullPath(declared, directory);
                var relative = Path.GetRelativePath(rootDirectory, resolved);
                if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    continue;
                included.Add(PathKey(resolved));
                Scan(resolved);
            }
        }

        Scan(rootPath);
        foreach (var candidate in candidates) Scan(candidate);
        return included;
    }

    private static string? ResolveEntryPoint(string source, string path)
    {
        var lines = NormalizeLines(source);
        if (lines.Any(line => MainPattern.IsMatch(line))) return "Main";

        var stem = Regex.Replace(Path.GetFileNameWithoutExtension(path), @"[^A-Za-z0-9_]", "_");
        var procedures = lines.Select(line => ProcedurePattern.Match(line)).Where(match => match.Success)
            .Select(match => match.Groups["name"].Value).ToArray();
        var byName = procedures.FirstOrDefault(name => name.Equals(stem, StringComparison.OrdinalIgnoreCase));
        if (byName is not null) return byName;
        var index = procedures.FirstOrDefault(name => name.Equals("Index", StringComparison.OrdinalIgnoreCase));
        if (index is not null) return index;
        return procedures.Length == 1 ? procedures[0] : null;
    }

    private static string RenameProcedure(string source, string originalName, string generatedName)
    {
        var pattern = new Regex(
            $@"(?im)^(?<indent>\s*)(?<visibility>(?:Public|Private)\s+)?Sub\s+{Regex.Escape(originalName)}\b",
            RegexOptions.CultureInvariant);
        var replaced = false;
        return pattern.Replace(source, match =>
        {
            if (replaced) return match.Value;
            replaced = true;
            return match.Groups["indent"].Value + match.Groups["visibility"].Value + "Sub " + generatedName;
        }, 1);
    }

    private static string BuildGeneratedEntryName(string relativePath)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(relativePath.Replace('\\', '/').ToLowerInvariant())))[..16];
        return "__XpsModule_" + hash;
    }

    private static void AppendNavigationDispatcher(StringBuilder source, string rootPath, string compileRoot, IReadOnlyList<ModuleEntry> modules)
    {
        var aliases = BuildAliases(rootPath, compileRoot, modules);
        source.AppendLine();
        source.AppendLine("Private Sub __XpsCompiledNavigationDispatch(target As String, parameterName As String, parameterValue As String)");
        source.AppendLine("    Dim __xpsTarget As String");
        source.AppendLine("    __xpsTarget = LCase(Trim(target))");

        var first = true;
        foreach (var pair in aliases.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            source.Append("    ").Append(first ? "If " : "ElseIf ")
                .Append("__xpsTarget = \"").Append(EscapeXpsString(pair.Key)).AppendLine("\" Then");
            source.Append("        Call ").Append(pair.Value).AppendLine("()");
            first = false;
        }

        if (!first)
        {
            source.AppendLine("    Else");
            source.AppendLine("        Error 5, \"Navigation target is not part of this compiled application.\"");
            source.AppendLine("    End If");
        }
        source.AppendLine("End Sub");
        source.AppendLine();
        source.AppendLine("Public Sub Navigate(target As String)");
        source.AppendLine("    Call __XpsCompiledNavigationDispatch(target, \"\", \"\")");
        source.AppendLine("End Sub");
    }

    private static Dictionary<string, string> BuildAliases(string rootPath, string compileRoot, IReadOnlyList<ModuleEntry> modules)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var basenameCounts = modules.GroupBy(module => Path.GetFileName(module.RelativeName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var module in modules)
        {
            var relative = module.IsRoot ? Path.GetFileName(rootPath) : module.RelativeName;
            var normalized = relative.Replace('\\', '/').ToLowerInvariant();
            AddAlias(result, normalized, module.EntryProcedure);
            if (normalized.EndsWith(".xps", StringComparison.Ordinal)) AddAlias(result, normalized[..^4], module.EntryProcedure);

            var basename = Path.GetFileName(normalized);
            if (basenameCounts.TryGetValue(Path.GetFileName(module.RelativeName), out var count) && count == 1)
            {
                AddAlias(result, basename, module.EntryProcedure);
                if (basename.EndsWith(".xps", StringComparison.Ordinal)) AddAlias(result, basename[..^4], module.EntryProcedure);
            }
        }
        return result;
    }

    private static void AddAlias(Dictionary<string, string> aliases, string alias, string entry)
    {
        if (alias.Length > 0 && !aliases.ContainsKey(alias)) aliases[alias] = entry;
    }

    private static string EscapeXpsString(string value) => value.Replace("\"", "\"\"", StringComparison.Ordinal);
    private static bool PathEquals(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    private static string PathKey(string path) => Path.GetFullPath(path).Replace('\\', '/').ToLowerInvariant();
    private static string[] NormalizeLines(string source) => source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

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

    private sealed record ModuleEntry(string Path, string RelativeName, string EntryProcedure, bool IsRoot);
}
