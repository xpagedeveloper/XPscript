namespace XPScript.Compiler;

/// <summary>Supplies relative source-map documents to isolated SDK compilation.</summary>
internal static class CompilerSourceDocuments
{
    internal static void Stage(string rootSourcePath, SourceMap map, string workspace)
    {
        var paths = Enumerable.Range(1, map.Count).Select(line => map.Resolve(line, rootSourcePath).SourcePath)
            .Append(rootSourcePath).Select(Path.GetFullPath)
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Where(File.Exists);
        foreach (var source in paths)
        {
            var id = SourceLineMarkerPreprocessor.SafeSourceId(source, rootSourcePath);
            var destination = Path.GetFullPath(Path.Combine(workspace, id));
            if (!destination.StartsWith(Path.GetFullPath(workspace) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new CompilerException("Source document must remain inside the compiler workspace.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: false);
            CompilerPathSecurity.HardenTemporaryFile(destination);
        }
    }
}
