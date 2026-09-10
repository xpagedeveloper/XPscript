using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ApplicationObjectPreprocessor
{
    private const string TitleStateKey = "__xps_application_title";
    private const string IconStateKey = "__xps_application_icon";
    private const string ProductStateKey = "__xps_application_executable_product";
    private const string CompanyStateKey = "__xps_application_executable_company";
    private const string VersionStateKey = "__xps_application_executable_version";
    private const string CopyrightStateKey = "__xps_application_executable_copyright";
    private const string FileDescriptionStateKey = "__xps_application_executable_file_description";
    private const string CommentsStateKey = "__xps_application_executable_comments";
    private const string WidthStateKey = "__xps_application_width";
    private const string HeightStateKey = "__xps_application_height";
    internal const string BuildIconMarker = "__XPSCRIPT_APPLICATION_ICON_BUILD__=";
    internal const string BuildProductMarker = "__XPSCRIPT_APPLICATION_PRODUCT_BUILD__=";
    internal const string BuildCompanyMarker = "__XPSCRIPT_APPLICATION_COMPANY_BUILD__=";
    internal const string BuildVersionMarker = "__XPSCRIPT_APPLICATION_VERSION_BUILD__=";
    internal const string BuildCopyrightMarker = "__XPSCRIPT_APPLICATION_COPYRIGHT_BUILD__=";
    internal const string BuildFileDescriptionMarker = "__XPSCRIPT_APPLICATION_FILE_DESCRIPTION_BUILD__=";
    internal const string BuildCommentsMarker = "__XPSCRIPT_APPLICATION_COMMENTS_BUILD__=";
    private static readonly string[] FeatureMarkers =
    [
        "Application.", "Process.State", "Session.State", "Request.State",
        "__XpsCompiledNavigationDispatch"
    ];

    public string Transform(string source)
    {
        if (!PreprocessorFeatureGate.ContainsAny(source, FeatureMarkers)) return source;

        RejectWrites(source);

        source = RewriteWritableApplicationProperty(source, "Title", TitleStateKey);
        source = RewriteWritableApplicationProperty(source, "Executable.Icon", IconStateKey, BuildIconMarker, resolvePath: true);
        source = RewriteWritableApplicationProperty(source, "Icon", IconStateKey, BuildIconMarker, resolvePath: true);
        source = RewriteWritableApplicationProperty(source, "Executable.Product", ProductStateKey, BuildProductMarker);
        source = RewriteWritableApplicationProperty(source, "Executable.Company", CompanyStateKey, BuildCompanyMarker);
        source = RewriteWritableApplicationProperty(source, "Executable.Version", VersionStateKey, BuildVersionMarker);
        source = RewriteWritableApplicationProperty(source, "Executable.Copyright", CopyrightStateKey, BuildCopyrightMarker);
        source = RewriteWritableApplicationProperty(source, "Executable.FileDescription", FileDescriptionStateKey, BuildFileDescriptionMarker);
        source = RewriteWritableApplicationProperty(source, "Executable.Comments", CommentsStateKey, BuildCommentsMarker);
        source = RewriteWritableApplicationProperty(source, "Width", WidthStateKey);
        source = RewriteWritableApplicationProperty(source, "Height", HeightStateKey);
        source = RewriteExitCode(source);

        source = Regex.Replace(source, @"\bApplication\.Registry\.User\b", "XPScriptApplicationRegistryRuntime.User", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.Registry\.System\b", "XPScriptApplicationRegistryRuntime.System", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.Secrets\b", "XPScriptApplicationSecretsRuntime", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.SystemLog\b", "XPScriptApplicationSystemLogRuntime", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.State\b", "XPScriptApplicationRuntime.State", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bProcess\.State\b", "XPScriptProcessRuntime.State", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bSession\.State\b", "XPScriptSessionRuntime.State", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bRequest\.State\b", "XPScriptRequestRuntime.State", RegexOptions.IgnoreCase);

        source = Regex.Replace(
            source,
            @"(?im)^(\s*Private\s+Sub\s+__XpsCompiledNavigationDispatch\s*\([^\r\n]*\)\s*)$",
            "$1" + Environment.NewLine + "    Call XPScriptRequestRuntime.BeforeCompiledNavigation()",
            RegexOptions.CultureInvariant);

        source = Regex.Replace(
            source,
            @"\bApplication\.Args\s*\(((?:[^()]|\([^()]*\))*)\)",
            m => "XPScriptApplicationRuntime.Arg(" + m.Groups[1].Value + ")",
            RegexOptions.IgnoreCase);

        source = Regex.Replace(source, @"\bApplication\.Args\b", "XPScriptApplicationRuntime.Args()", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.ArgCount\b", "XPScriptApplicationRuntime.ArgCount", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.CommandLine\b", "XPScriptApplicationRuntime.CommandLine", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.ExecutablePath\b", "XPScriptApplicationRuntime.ExecutablePath", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.ExecutableFileName\b", "XPScriptApplicationRuntime.ExecutableFileName", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.ExecutableDirectory\b", "XPScriptApplicationRuntime.ExecutableDirectory", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.TempPath\b", "XPScriptApplicationRuntime.TempPath", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.TempFolder\b", "XPScriptApplicationRuntime.TempPath", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.Path\b", "XPScriptApplicationRuntime.Path", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.FileName\b", "XPScriptApplicationRuntime.FileName", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.IsDebugging\b", "XPScriptDebugRuntime.IsEnabled", RegexOptions.IgnoreCase);
        return source;
    }

    private static string RewriteExitCode(string source)
    {
        source = Regex.Replace(
            source,
            @"(?im)^(?<indent>\s*)Application\.ExitCode\s*=\s*(?<value>.+?)\s*$",
            m => m.Groups["indent"].Value + "System.Environment.ExitCode = XPScriptRuntime.CInt(" + m.Groups["value"].Value + ")",
            RegexOptions.CultureInvariant);

        return Regex.Replace(
            source,
            @"\bApplication\.ExitCode\b",
            "System.Environment.ExitCode",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string RewriteWritableApplicationProperty(
        string source,
        string propertyName,
        string stateKey,
        string? buildMarker = null,
        bool resolvePath = false)
    {
        source = Regex.Replace(
            source,
            $@"(?im)^(?<indent>\s*)Application\.{Regex.Escape(propertyName)}\s*=\s*(?<value>.+?)\s*$",
            m =>
            {
                var indent = m.Groups["indent"].Value;
                var value = m.Groups["value"].Value;
                var assignment = indent + $"Call XPScriptApplicationRuntime.State.Set(\"{stateKey}\", " + value + ")";
                if (buildMarker is null) return assignment;

                var literal = TryReadStringLiteral(value);
                if (literal is null) return assignment;
                var markerValue = literal;

                if (resolvePath && literal.Length > 0)
                {
                    var sourcePath = ExpandedSourceContext.Current?.SourcePath;
                    if (string.IsNullOrWhiteSpace(sourcePath)) return assignment;
                    try
                    {
                        var baseDirectory = Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory;
                        markerValue = Path.IsPathRooted(literal) ? Path.GetFullPath(literal) : Path.GetFullPath(literal, baseDirectory);
                        if (Path.GetExtension(markerValue).Equals(".ico", StringComparison.OrdinalIgnoreCase) && !File.Exists(markerValue))
                            throw new CompilerException($"Application.Executable.Icon file was not found: {literal}");
                    }
                    catch (CompilerException)
                    {
                        throw;
                    }
                    catch
                    {
                        return assignment;
                    }
                }

                return indent + "' " + buildMarker + markerValue + Environment.NewLine + assignment;
            },
            RegexOptions.CultureInvariant);

        return Regex.Replace(
            source,
            $@"\bApplication\.{Regex.Escape(propertyName)}\b",
            $"XPScriptApplicationRuntime.State.Get(\"{stateKey}\")",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string? TryReadStringLiteral(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '"' || trimmed[^1] != '"') return null;
        return trimmed[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
    }

    private static void RejectWrites(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = StripComment(lines[i]);
            if (Regex.IsMatch(line, @"\bApplication\.Args\s*\([^)]*\)\s*=", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(line, @"\bApplication\.(?:Args|ArgCount|CommandLine|ExecutablePath|ExecutableFileName|ExecutableDirectory|TempPath|TempFolder|Path|FileName|IsDebugging)\s*=", RegexOptions.IgnoreCase))
                throw new CompilerException($"input.xps({i + 1},1): Application is read-only runtime state.");
        }
    }

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (!inString && line[i] == '\'') return line[..i];
        }
        return line;
    }
}
