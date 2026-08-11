using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

public sealed class CompilerDriver
{
    private static readonly HashSet<string> SupportedRuntimeIdentifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "win-x64", "win-arm64",
        "linux-x64", "linux-arm64",
        "osx-x64", "osx-arm64"
    };

    public static IReadOnlyCollection<string> SupportedRuntimes => SupportedRuntimeIdentifiers;

    public static string CurrentRuntimeIdentifier()
    {
        var architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            _ => throw new PlatformNotSupportedException("XPScript compiler currently supports x64 and arm64 publish targets.")
        };

        if (OperatingSystem.IsWindows()) return "win-" + architecture;
        if (OperatingSystem.IsLinux()) return "linux-" + architecture;
        if (OperatingSystem.IsMacOS()) return "osx-" + architecture;
        throw new PlatformNotSupportedException("Unable to determine a default XPScript publish target for this operating system.");
    }

    public async Task<CompileResult> CompileWithResultAsync(string sourcePath, string outputPath, bool selfContained) =>
        await CompileWithResultAsync(sourcePath, outputPath, selfContained, CurrentRuntimeIdentifier());

    public async Task<CompileResult> CompileWithResultAsync(string sourcePath, string outputPath, bool selfContained, string runtimeIdentifier)
    {
        string source = "";
        try
        {
            if (!Path.GetExtension(sourcePath).Equals(".xps", StringComparison.OrdinalIgnoreCase))
                return CompileResult.Error([CreateDiagnostic(0, 0, "XPScript source files must use the .xps extension.", "", "")]);

            if (!File.Exists(sourcePath))
                return CompileResult.Error([CreateDiagnostic(0, 0, "Source file not found.", sourcePath, sourcePath)]);

            source = await File.ReadAllTextAsync(sourcePath);
            await CompileAsync(sourcePath, outputPath, selfContained, runtimeIdentifier);
            return CompileResult.Ok(outputPath);
        }
        catch (CompilerException ex)
        {
            return CompileResult.Error(ParseCompilerDiagnostics(ex.Message, sourcePath, source));
        }
        catch (Exception ex)
        {
            return CompileResult.Error([CreateDiagnostic(0, 0, ex.Message, "", "")]);
        }
    }

    public async Task CompileAsync(string sourcePath, string outputPath, bool selfContained) =>
        await CompileAsync(sourcePath, outputPath, selfContained, CurrentRuntimeIdentifier());

    public async Task CompileAsync(string sourcePath, string outputPath, bool selfContained, string runtimeIdentifier)
    {
        var rid = NormalizeRuntimeIdentifier(runtimeIdentifier);
        var source = await File.ReadAllTextAsync(sourcePath);
        var transpiler = new XPScriptTranspiler();
        var generatedSource = transpiler.Transpile(source, sourcePath);

        var tempRoot = Path.Combine(Path.GetTempPath(), "XPScript", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectPath = Path.Combine(tempRoot, "Generated.csproj");
            var programPath = Path.Combine(tempRoot, "Program.cs");
            var publishDir = Path.Combine(tempRoot, "publish");

            var csproj = BuildGeneratedProject(rid, selfContained);
            await File.WriteAllTextAsync(projectPath, csproj);
            await File.WriteAllTextAsync(programPath, generatedSource);

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet", UseShellExecute = false, RedirectStandardOutput = true,
                RedirectStandardError = true, CreateNoWindow = true
            };
            psi.ArgumentList.Add("publish"); psi.ArgumentList.Add(projectPath); psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("Release"); psi.ArgumentList.Add("-o"); psi.ArgumentList.Add(publishDir); psi.ArgumentList.Add("--nologo");
            psi.ArgumentList.Add("-r"); psi.ArgumentList.Add(rid);
            psi.ArgumentList.Add("--self-contained"); psi.ArgumentList.Add(selfContained ? "true" : "false");

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start dotnet publish.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask; var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                var diagnosticText = stdout + Environment.NewLine + stderr;
                throw new CompilerException("Generated code failed to compile." + Environment.NewLine + diagnosticText + Environment.NewLine + BuildGeneratedSourceContext(generatedSource, diagnosticText));
            }

            var generatedExecutable = FindPublishedExecutable(publishDir, rid);
            if (generatedExecutable is null)
                throw new CompilerException("Compilation succeeded, but no executable was produced for runtime " + rid + ".");

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory)) Directory.CreateDirectory(outputDirectory);
            File.Copy(generatedExecutable, outputPath, overwrite: true);

            if (!rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) && !OperatingSystem.IsWindows())
            {
                try
                {
                    var mode = File.GetUnixFileMode(outputPath);
                    File.SetUnixFileMode(outputPath, mode | UnixFileMode.UserExecute);
                }
                catch (PlatformNotSupportedException) { }
            }
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    private static string NormalizeRuntimeIdentifier(string value)
    {
        var rid = (value ?? "").Trim().ToLowerInvariant();
        if (!SupportedRuntimeIdentifiers.Contains(rid))
            throw new ArgumentException("Unsupported runtime identifier '" + value + "'. Supported values: " + string.Join(", ", SupportedRuntimeIdentifiers.OrderBy(x => x)) + ".");
        return rid;
    }

    private static string BuildGeneratedProject(string runtimeIdentifier, bool selfContained) => $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <StartupObject>Program</StartupObject>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RuntimeIdentifier>{runtimeIdentifier}</RuntimeIdentifier>
    <SelfContained>{selfContained.ToString().ToLowerInvariant()}</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <EnableCompressionInSingleFile>{selfContained.ToString().ToLowerInvariant()}</EnableCompressionInSingleFile>
  </PropertyGroup>
</Project>
""";

    private static string? FindPublishedExecutable(string publishDirectory, string rid)
    {
        if (rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase))
            return Directory.EnumerateFiles(publishDirectory, "*.exe", SearchOption.TopDirectoryOnly).SingleOrDefault();

        var candidates = Directory.EnumerateFiles(publishDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.HasExtension(path))
            .Where(path => !Path.GetFileName(path).EndsWith(".dbg", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return candidates.Length == 1 ? candidates[0] : candidates.FirstOrDefault(path => Path.GetFileName(path).Equals("Generated", StringComparison.OrdinalIgnoreCase));
    }

    private static List<CompileDiagnostic> ParseCompilerDiagnostics(string message, string sourcePath, string source)
    {
        var result = new List<CompileDiagnostic>();
        var sourceLines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var escapedSource = Regex.Escape(sourcePath).Replace("\\\\", @"[\\/]");
        var sourcePattern = new Regex($@"(?:{escapedSource}|[^\r\n]*\.xps)\((?<line>\d+)(?:,(?<pos>\d+))?\):\s*(?<desc>[^\r\n]+)", RegexOptions.IgnoreCase);

        foreach (Match match in sourcePattern.Matches(message))
        {
            var line = int.Parse(match.Groups["line"].Value);
            var pos = match.Groups["pos"].Success ? int.Parse(match.Groups["pos"].Value) : 1;
            var code = line > 0 && line <= sourceLines.Length ? sourceLines[line - 1] : "";
            result.Add(CreateDiagnostic(line, pos, Humanize(match.Groups["desc"].Value.Trim()), code, Mark(code, pos)));
        }
        if (result.Count > 0) return result.GroupBy(x => (x.Line, x.Position, x.Description)).Select(x => x.First()).ToList();

        var generatedPattern = new Regex(@"Program\.cs\((?<line>\d+),(?<pos>\d+)\):\s*error\s+CS\d+:\s*(?<desc>.*?)(?:\s*\[|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        foreach (Match match in generatedPattern.Matches(message))
        {
            result.Add(CreateDiagnostic(int.Parse(match.Groups["line"].Value), int.Parse(match.Groups["pos"].Value), Humanize(match.Groups["desc"].Value.Trim()), "", ""));
        }
        if (result.Count == 0) result.Add(CreateDiagnostic(0, 0, Humanize(message.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "Compilation failed."), "", ""));
        return result;
    }

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
        "string" or "System.String" => "String", "int" or "System.Int32" => "Integer", "long" or "System.Int64" => "Long",
        "double" or "System.Double" => "Double", "float" or "System.Single" => "Single", "bool" or "System.Boolean" => "Boolean",
        "byte" or "System.Byte" => "Byte", "decimal" or "System.Decimal" => "Currency", _ => type
    };

    private static CompileDiagnostic CreateDiagnostic(int line, int pos, string description, string code, string marked) => new()
    { Line = line, Position = pos, Description = description, Code = code, MarkedCode = marked };

    private static string Mark(string code, int position)
    {
        if (string.IsNullOrEmpty(code) || position <= 0) return code;
        var caret = Math.Clamp(position - 1, 0, code.Length);
        return code + Environment.NewLine + new string(' ', caret) + "^";
    }

    private static string BuildGeneratedSourceContext(string generatedSource, string diagnostics)
    {
        var lineNumbers = Regex.Matches(diagnostics, @"Program\.cs\((\d+),\d+\)").Select(m => int.Parse(m.Groups[1].Value)).Distinct().OrderBy(x => x).ToArray();
        if (lineNumbers.Length == 0) return "";
        var lines = generatedSource.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var builder = new StringBuilder("Generated code context:" + Environment.NewLine); var emitted = new HashSet<int>();
        foreach (var lineNumber in lineNumbers)
            for (var current = Math.Max(1, lineNumber - 2); current <= Math.Min(lines.Length, lineNumber + 2); current++)
                if (emitted.Add(current)) builder.Append(current == lineNumber ? "> " : "  ").Append(current.ToString().PadLeft(5)).Append(": ").AppendLine(lines[current - 1]);
        return builder.ToString();
    }
}
