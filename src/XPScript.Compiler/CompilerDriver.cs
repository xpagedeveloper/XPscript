using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

public sealed class CompilerDriver
{
    public async Task CompileAsync(string sourcePath, string outputPath, bool selfContained)
    {
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

            var csproj = selfContained
                ? """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <StartupObject>Program</StartupObject>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  </PropertyGroup>
</Project>
"""
                : """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <StartupObject>Program</StartupObject>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>false</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
  </PropertyGroup>
</Project>
""";

            await File.WriteAllTextAsync(projectPath, csproj);
            await File.WriteAllTextAsync(programPath, generatedSource);

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("publish");
            psi.ArgumentList.Add(projectPath);
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("Release");
            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(publishDir);
            psi.ArgumentList.Add("--nologo");

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Unable to start dotnet publish.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                var diagnosticText = stdout + Environment.NewLine + stderr;
                throw new CompilerException(
                    "Generated C# failed to compile." + Environment.NewLine +
                    diagnosticText + Environment.NewLine +
                    BuildGeneratedSourceContext(generatedSource, diagnosticText));
            }

            var generatedExe = Directory
                .EnumerateFiles(publishDir, "*.exe", SearchOption.TopDirectoryOnly)
                .SingleOrDefault();

            if (generatedExe is null)
                throw new CompilerException("dotnet publish succeeded, but no .exe was produced.");

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            File.Copy(generatedExe, outputPath, overwrite: true);
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }

    private static string BuildGeneratedSourceContext(string generatedSource, string diagnostics)
    {
        var lineNumbers = Regex.Matches(diagnostics, @"Program\.cs\((\d+),\d+\)")
            .Select(match => int.Parse(match.Groups[1].Value))
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        if (lineNumbers.Length == 0) return "";

        var lines = generatedSource.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var builder = new StringBuilder("Generated C# context:" + Environment.NewLine);
        var emitted = new HashSet<int>();

        foreach (var lineNumber in lineNumbers)
        {
            var start = Math.Max(1, lineNumber - 2);
            var end = Math.Min(lines.Length, lineNumber + 2);
            for (var current = start; current <= end; current++)
            {
                if (!emitted.Add(current)) continue;
                builder.Append(current == lineNumber ? "> " : "  ")
                    .Append(current.ToString().PadLeft(5))
                    .Append(": ")
                    .AppendLine(lines[current - 1]);
            }
        }
        return builder.ToString();
    }
}
