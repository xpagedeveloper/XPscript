using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class CrossPlatformPreprocessor
{
    private static readonly string[] FeatureMarkers =
    [
        "Platform", "FileExists", "DirExists", "IsFile", "IsDir", "FileInfo", "FileHash",
        "FileEquals", "Files", "Directories", "CopyFile", "MoveFile", "ReadFile", "ReadLines",
        "ReadBytes", "WriteFile", "AppendFile", "WriteLines", "WriteBytes", "Path", "Dir",
        "StrTemplate", "ShellArgs", "Shell"
    ];

    public string Transform(string source)
    {
        if (!PreprocessorFeatureGate.ContainsAny(source, FeatureMarkers)) return source;

        source = Regex.Replace(
            source,
            @"(?<![\w.])Platform\s*\(\s*\)",
            "XPCrossPlatformRuntime.Platform()",
            RegexOptions.IgnoreCase);

        source = Regex.Replace(
            source,
            @"(?<![\w.])Platform\b(?!\s*\()",
            "XPCrossPlatformRuntime.Platform()",
            RegexOptions.IgnoreCase);

        source = Regex.Replace(
            source,
            @"(?<![\w.])FileExists\s*\(",
            "XPCrossPlatformRuntime.FileExists(",
            RegexOptions.IgnoreCase);

        source = Regex.Replace(
            source,
            @"(?<![\w.])DirExists\s*\(",
            "XPCrossPlatformRuntime.DirExists(",
            RegexOptions.IgnoreCase);

        source = Regex.Replace(
            source,
            @"(?<![\w.])IsFile\s*\(",
            "XPCrossPlatformRuntime.IsFile(",
            RegexOptions.IgnoreCase);

        source = Regex.Replace(
            source,
            @"(?<![\w.])IsDir\s*\(",
            "XPCrossPlatformRuntime.IsDir(",
            RegexOptions.IgnoreCase);

        foreach (var function in new[]
        {
            "FileInfo", "FileHash", "FileEquals", "Files", "Directories", "CopyFile", "MoveFile",
            "ReadFile", "ReadLines", "ReadBytes", "WriteFile", "AppendFile", "WriteLines", "WriteBytes"
        })
        {
            source = Regex.Replace(
                source,
                $@"(?<![\w.]){Regex.Escape(function)}\s*\(",
                $"XPCrossPlatformRuntime.{function}(",
                RegexOptions.IgnoreCase);
        }

        source = Regex.Replace(
            source,
            @"\bNew\s+Path\s*\(",
            "XPCrossPlatformRuntime.PathValue(",
            RegexOptions.IgnoreCase);

        source = Regex.Replace(
            source,
            @"(?<![\w.])Dir\s*\(",
            "XPCrossPlatformRuntime.Dir(",
            RegexOptions.IgnoreCase);

        source = Regex.Replace(
            source,
            @"(?<![\w.])StrTemplate\s*\(",
            "XPCrossPlatformRuntime.StrTemplate(",
            RegexOptions.IgnoreCase);

        source = Regex.Replace(
            source,
            @"(?<![\w.])ShellArgs\s*\(",
            "XPCrossPlatformRuntime.ShellArgs(",
            RegexOptions.IgnoreCase);

        source = Regex.Replace(
            source,
            @"(?<![\w.])Shell\s*\(",
            "XPCrossPlatformRuntime.Shell(",
            RegexOptions.IgnoreCase);

        return source;
    }
}
