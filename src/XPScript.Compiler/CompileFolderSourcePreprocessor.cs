using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

public sealed class CompileFolderSourcePreprocessor
{
    public sealed record Result(string Source, IReadOnlyList<string> Dependencies, IReadOnlyList<string> Modules, bool Enabled);

    private static readonly Regex CompilePattern = new(
        "^\\s*\\[\\s*Compile\\s*:\\s*(?:\"(?<quoted>[^\"]+)\"|(?<plain>[^\\]]+))\\s*\\]\\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IncludePattern = new(
        "^Include\\s+\"([^\"]+)\"\\s*$",
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

    public static bool IsDesktopProject(string source, string rootSourcePath)
    {
        if (IsDesktopProject(source)) return true;
        if (string.IsNullOrWhiteSpace(rootSourcePath)) return false;

        try
        {
            var match = NormalizeLines(source)
                .Select(line => CompilePattern.Match(StripComment(line).Trim()))
                .FirstOrDefault(candidate => candidate.Success);
            if (match is null || !match.Success) return false;

            var declared = (match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["plain"].Value).Trim();
            if (declared.Length == 0) return false;
            var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(rootSourcePath)) ?? Environment.CurrentDirectory;
            var portable = declared.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(portable)) return false;
            var compileRoot = Path.GetFullPath(Path.Combine(sourceDirectory, portable));
            var relative = Path.GetRelativePath(sourceDirectory, compileRoot);
            if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !Directory.Exists(compileRoot))
                return false;

            foreach (var path in Directory.EnumerateFiles(compileRoot, "*.xps", SearchOption.AllDirectories))
            {
                string moduleSource;
                try { moduleSource = File.ReadAllText(path); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
                if (DesktopUiPattern.IsMatch(moduleSource)) return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public Result Transform(string rootSource, string rootSourcePath, bool enableModules)
    {
        ArgumentNullException.ThrowIfNull(rootSource);
        if (string.IsNullOrWhiteSpace(rootSourcePath))
            throw new CompilerException("Compile-folder processing requires a source file path.");

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

        if (IsDesktopProject(rootSource, rootPath))
            ValidateDesktopMain(rootPath, rootDirectory);

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

        var sourceBuilder = new StringBuilder(strippedRoot.Length + moduleFiles.Length * 1024);
        sourceBuilder.AppendLine(strippedRoot.TrimEnd('\r', '\n'));

        var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootPath };
        var modules = new List<ModuleEntry>
        {
            new(Path.GetFileName(rootPath), "Main", true)
        };

        foreach (var path in moduleFiles)
        {
            var moduleSource = File.ReadAllText(path);
            dependencies.Add(path);
            var relativeName = Path.GetRelativePath(compileRoot, path).Replace('\\', '/');
            var entry = ResolveEntryPoint(moduleSource, path);
            if (entry is null)
                throw new CompilerException($"{relativeName}: Compiled XPS module requires one navigable entry point named Main, Index, or matching the file name, or exactly one Sub.");

            var generatedName = BuildGeneratedEntryName(relativeName);
            moduleSource = RenameProcedure(moduleSource, entry, generatedName);
            modules.Add(new ModuleEntry(relativeName, generatedName, false));

            moduleSource = RewriteModuleIncludes(moduleSource, path, rootDirectory);
            sourceBuilder.AppendLine();
            sourceBuilder.AppendLine("' ----- compiled module: " + relativeName + " -----");
            sourceBuilder.AppendLine(moduleSource.TrimEnd('\r', '\n'));
        }

        foreach (var include in includedPaths) dependencies.Add(include);
        AppendNavigationDispatcher(sourceBuilder, rootPath, modules);

        return new Result(
            sourceBuilder.ToString(),
            dependencies.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            modules.Select(x => x.RelativeName).ToArray(),
            true);
    }

    private static void ValidateDesktopMain(string rootPath, string rootDirectory)
    {
        var expectedMain = Path.Combine(rootDirectory, "main.xps");
        if (!File.Exists(expectedMain))
            throw new CompilerException("Desktop [Compile:folder] projects require main.xps in the application root.");
        if (!PathEquals(rootPath, expectedMain))
            throw new CompilerException("Desktop [Compile:folder] projects must be compiled from main.xps.");
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
                if (value.Length == 0)
                    throw new CompilerException($"{Path.GetFileName(sourcePath)}({i + 1},1): Compile folder cannot be empty.");
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
        if (Path.IsPathRooted(portable))
            throw new CompilerException("Compile folder must be relative to the main .xps file.");

        var resolved = Path.GetFullPath(Path.Combine(sourceDirectory, portable));
        EnsureInsideRoot(sourceDirectory, resolved, "Compile folder");
        if (!Directory.Exists(resolved))
            throw new CompilerException("Compile folder was not found: " + SafeName(declared));
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

            foreach (var line in NormalizeLines(source))
            {
                var match = IncludePattern.Match(StripComment(line).Trim());
                if (!match.Success) continue;
                var declared = match.Groups[1].Value.Trim();
                if (!declared.EndsWith(".xps", StringComparison.OrdinalIgnoreCase)) continue;

                string resolved;
                try { resolved = Path.GetFullPath(declared, directory); }
                catch { continue; }

                try { EnsureInsideRoot(rootDirectory, resolved, "Include source"); }
                catch (CompilerException) { continue; }
                included.Add(PathKey(resolved));
                Scan(resolved);
            }
        }

        Scan(rootPath);
        foreach (var candidate in candidates) Scan(candidate);
        return included;
    }

    private static string RewriteModuleIncludes(string source, string modulePath, string rootDirectory)
    {
        var moduleDirectory = Path.GetDirectoryName(Path.GetFullPath(modulePath)) ?? rootDirectory;
        var lines = NormalizeLines(source);
        for (var i = 0; i < lines.Length; i++)
        {
            var code = StripComment(lines[i]).Trim();
            var match = IncludePattern.Match(code);
            if (!match.Success) continue;

            var declared = match.Groups[1].Value.Trim();
            string resolved;
            try { resolved = Path.GetFullPath(declared, moduleDirectory); }
            catch { throw new CompilerException($"{Path.GetFileName(modulePath)}({i + 1},1): Invalid Include path."); }

            EnsureInsideRoot(rootDirectory, resolved, "Include source");
            if (resolved.Contains('"'))
                throw new CompilerException($"{Path.GetFileName(modulePath)}({i + 1},1): Include path contains an unsupported quote character.");

            lines[i] = "Include \"" + resolved + "\"";
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string? ResolveEntryPoint(string source, string path)
    {
        var procedures = NormalizeLines(source)
            .Select(line => ProcedurePattern.Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups["name"].Value)
            .ToArray();

        var main = procedures.FirstOrDefault(name => name.Equals("Main", StringComparison.OrdinalIgnoreCase));
        if (main is not null) return main;

        var stem = Regex.Replace(Path.GetFileNameWithoutExtension(path), @"[^A-Za-z0-9_]", "_");
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
        return pattern.Replace(source, match =>
            match.Groups["indent"].Value + match.Groups["visibility"].Value + "Sub " + generatedName, 1);
    }

    private static string BuildGeneratedEntryName(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16];
        return "XpsCompilerGeneratedModule_" + hash;
    }

    private static void AppendNavigationDispatcher(StringBuilder source, string rootPath, IReadOnlyList<ModuleEntry> modules)
    {
        var aliases = BuildAliases(rootPath, modules);
        source.AppendLine();
        source.AppendLine("Private Sub XpsCompilerGeneratedNavigationDispatch(target As String)");
        source.AppendLine("    Dim xpsCompilerGeneratedTarget As String");
        source.AppendLine("    xpsCompilerGeneratedTarget = LCase(Trim(target))");

        var first = true;
        foreach (var pair in aliases.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            source.Append("    ").Append(first ? "If " : "ElseIf ")
                .Append("xpsCompilerGeneratedTarget = \"").Append(EscapeXpsString(pair.Key)).AppendLine("\" Then");
            source.AppendLine("        Call XPScriptRequestRuntime.BeforeCompiledNavigation()");
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
        source.AppendLine("    Call XpsCompilerGeneratedNavigationDispatch(target)");
        source.AppendLine("End Sub");
    }

    private static Dictionary<string, string> BuildAliases(string rootPath, IReadOnlyList<ModuleEntry> modules)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var basenameCounts = modules
            .GroupBy(module => Path.GetFileName(module.RelativeName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var module in modules)
        {
            var relative = module.IsRoot ? Path.GetFileName(rootPath) : module.RelativeName;
            var normalized = relative.Replace('\\', '/').ToLowerInvariant();
            AddAlias(result, normalized, module.EntryProcedure);
            if (normalized.EndsWith(".xps", StringComparison.Ordinal))
                AddAlias(result, normalized[..^4], module.EntryProcedure);

            var basename = Path.GetFileName(normalized);
            if (basenameCounts.TryGetValue(Path.GetFileName(module.RelativeName), out var count) && count == 1)
            {
                AddAlias(result, basename, module.EntryProcedure);
                if (basename.EndsWith(".xps", StringComparison.Ordinal))
                    AddAlias(result, basename[..^4], module.EntryProcedure);
            }
        }
        return result;
    }

    private static void AddAlias(Dictionary<string, string> aliases, string alias, string entry)
    {
        if (alias.Length > 0 && !aliases.ContainsKey(alias)) aliases[alias] = entry;
    }

    private static void EnsureInsideRoot(string rootDirectory, string path, string kind)
    {
        var root = Path.GetFullPath(rootDirectory);
        var full = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(root, full);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new CompilerException(kind + " must remain inside the main .xps directory.");
    }

    private static string SafeName(string value)
    {
        try { return Path.GetFileName(value.TrimEnd('/', '\\')); }
        catch { return "<invalid-path>"; }
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

    private sealed record ModuleEntry(string RelativeName, string EntryProcedure, bool IsRoot);
}
