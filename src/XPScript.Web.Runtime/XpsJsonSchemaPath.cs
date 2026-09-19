namespace XPScript.Web.Runtime;

public static class XpsJsonSchemaPath
{
    public static string NormalizeRelative(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var path = value.Trim().Replace('\\', '/');
        if (path.Length is < 1 or > 1024)
            throw new ArgumentException("JsonSchema requires a relative schema file path.", nameof(value));
        if (Path.IsPathRooted(path) || path.StartsWith("/", StringComparison.Ordinal) ||
            path.Split('/').Any(x => x == "..") || path.Any(char.IsControl))
            throw new ArgumentException("JsonSchema path must stay inside the web root.", nameof(value));
        if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("JsonSchema path must reference a .json file.", nameof(value));
        return path;
    }

    public static string ResolveInsideRoot(string root, string value)
    {
        ArgumentNullException.ThrowIfNull(root);
        var path = NormalizeRelative(value);
        var rootPath = Path.GetFullPath(root);
        var candidate = Path.GetFullPath(Path.Combine(rootPath, path.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(rootPath, candidate);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("JsonSchema path must stay inside the web root.", nameof(value));
        return candidate;
    }
}
