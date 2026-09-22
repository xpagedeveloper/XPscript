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

        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "FileExists", "XPCrossPlatformRuntime.FileExists");

        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "DirExists", "XPCrossPlatformRuntime.DirExists");

        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "IsFile", "XPCrossPlatformRuntime.IsFile");

        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "IsDir", "XPCrossPlatformRuntime.IsDir");

        foreach (var function in new[]
        {
            "FileInfo", "FileHash", "FileEquals", "Files", "Directories", "CopyFile", "MoveFile",
            "ReadFile", "ReadLines", "ReadBytes", "WriteFile", "AppendFile", "WriteLines", "WriteBytes"
        })
        {
            source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, function, $"XPCrossPlatformRuntime.{function}");
        }

        source = Regex.Replace(
            source,
            @"\bNew\s+Path\s*\(",
            "XPCrossPlatformRuntime.PathValue(",
            RegexOptions.IgnoreCase);

        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "Dir", "XPCrossPlatformRuntime.Dir");

        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "StrTemplate", "XPCrossPlatformRuntime.StrTemplate");

        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "ShellArgs", "XPCrossPlatformRuntime.ShellArgs");

        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "Shell", "XPCrossPlatformRuntime.Shell");

        return source;
    }
}
