using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class CrossPlatformPreprocessor
{
    public string Transform(string source)
    {
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
            @"(?<![\w.])Shellid\s*\(",
            "XPShellIdRuntime.ShellId(",
            RegexOptions.IgnoreCase);

        source = Regex.Replace(
            source,
            @"(?<![\w.])Shell\s*\(",
            "XPCrossPlatformRuntime.Shell(",
            RegexOptions.IgnoreCase);

        return source;
    }
}
