namespace XPScript.Compiler;

internal static class CompilerOutputPublisher
{
    private sealed record Dependency(string SourcePath, string FileName);

    public static void Publish(
        string generatedExecutable,
        string outputPath,
        string sourcePath,
        IReadOnlyList<NativeDependencyPackager.Dependency> nativeDependencies,
        IReadOnlyList<ManagedAssemblyReferencePreprocessor.NativeReference> managedNativeDependencies,
        bool makeExecutable)
    {
        var sourceDirectory = Path.GetFullPath(Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? Environment.CurrentDirectory);
        var dependencies = new List<Dependency>();

        foreach (var dependency in nativeDependencies)
        {
            dependencies.Add(new Dependency(
                CompilerPathSecurity.ResolveApplicationLocalNativeFile(sourceDirectory, dependency.DeclaredPath),
                dependency.LoadName));
        }

        foreach (var dependency in managedNativeDependencies)
        {
            var sourceFile = CompilerPathSecurity.ResolveProjectLocalFile(sourceDirectory, dependency.DeclaredPath, "ReferenceNative");
            dependencies.Add(new Dependency(sourceFile, Path.GetFileName(sourceFile)));
        }

        PublishStaged(generatedExecutable, outputPath, dependencies, makeExecutable);
    }

    private static void PublishStaged(
        string generatedExecutable,
        string outputPath,
        IReadOnlyList<Dependency> dependencies,
        bool makeExecutable)
    {
        var outputFullPath = Path.GetFullPath(outputPath);
        if (Directory.Exists(outputFullPath))
            throw new CompilerException("Compiler output path identifies a directory; a file path is required.");

        var outputDirectory = Path.GetDirectoryName(outputFullPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(outputDirectory);

        var stageDirectory = Path.Combine(outputDirectory, ".xpscript-publish-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stageDirectory);
        CompilerPathSecurity.HardenTemporaryDirectory(stageDirectory);

        try
        {
            var executableName = Path.GetFileName(outputFullPath);
            if (string.IsNullOrWhiteSpace(executableName))
                throw new CompilerException("Compiler output path must end with a file name.");

            var stagedExecutable = Path.Combine(stageDirectory, executableName);
            File.Copy(generatedExecutable, stagedExecutable, overwrite: false);
            CompilerPathSecurity.HardenTemporaryFile(stagedExecutable);

            if (makeExecutable && !OperatingSystem.IsWindows())
            {
                try
                {
                    var mode = File.GetUnixFileMode(stagedExecutable);
                    File.SetUnixFileMode(stagedExecutable, mode | UnixFileMode.UserExecute);
                }
                catch (PlatformNotSupportedException) { }
            }

            var seenNames = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            {
                executableName
            };
            var operations = new List<(string Stage, string Target)>();

            foreach (var dependency in dependencies)
            {
                var fileName = Path.GetFileName(dependency.FileName);
                if (string.IsNullOrWhiteSpace(fileName) || fileName != dependency.FileName)
                    throw new CompilerException("Dependency output name must be a file name only: " + dependency.FileName);
                if (!seenNames.Add(fileName))
                    throw new CompilerException("Multiple compiler outputs would use the same file name: " + fileName);

                var staged = Path.Combine(stageDirectory, fileName);
                File.Copy(dependency.SourcePath, staged, overwrite: false);
                CompilerPathSecurity.HardenTemporaryFile(staged);
                operations.Add((staged, Path.Combine(outputDirectory, fileName)));
            }

            // Dependencies are committed first. The executable is committed last.
            operations.Add((stagedExecutable, outputFullPath));
            CommitBatch(stageDirectory, operations);
        }
        finally
        {
            try { Directory.Delete(stageDirectory, recursive: true); } catch { }
        }
    }

    private static void CommitBatch(string stageDirectory, IReadOnlyList<(string Stage, string Target)> operations)
    {
        var backupDirectory = Path.Combine(stageDirectory, "backup");
        Directory.CreateDirectory(backupDirectory);
        CompilerPathSecurity.HardenTemporaryDirectory(backupDirectory);

        var backups = new List<(string Backup, string Target)>();
        var installed = new List<string>();

        try
        {
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (Directory.Exists(operation.Target))
                    throw new CompilerException("Compiler output target identifies an existing directory: " + operation.Target);

                if (File.Exists(operation.Target))
                {
                    var backup = Path.Combine(backupDirectory, i.ToString("D4") + "-" + Path.GetFileName(operation.Target));
                    File.Move(operation.Target, backup);
                    backups.Add((backup, operation.Target));
                }

                File.Move(operation.Stage, operation.Target);
                installed.Add(operation.Target);
            }
        }
        catch
        {
            for (var i = installed.Count - 1; i >= 0; i--)
            {
                try { if (File.Exists(installed[i])) File.Delete(installed[i]); } catch { }
            }

            for (var i = backups.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (File.Exists(backups[i].Backup))
                        File.Move(backups[i].Backup, backups[i].Target);
                }
                catch { }
            }
            throw;
        }
    }
}
