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
            var stagedDependencies = new List<(string Stage, string Target)>();

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
                stagedDependencies.Add((staged, Path.Combine(outputDirectory, fileName)));
            }

            // Dependencies are committed first. The new executable is committed last so a
            // dependency failure cannot expose a new executable with incomplete dependencies.
            foreach (var dependency in stagedDependencies)
                ReplaceFile(dependency.Stage, dependency.Target);

            ReplaceFile(stagedExecutable, outputFullPath);
        }
        finally
        {
            try { Directory.Delete(stageDirectory, recursive: true); } catch { }
        }
    }

    private static void ReplaceFile(string stagedPath, string targetPath)
    {
        var backupPath = targetPath + ".xpscript-backup-" + Guid.NewGuid().ToString("N");
        var hadExisting = File.Exists(targetPath);

        try
        {
            if (hadExisting)
                File.Move(targetPath, backupPath);

            File.Move(stagedPath, targetPath);

            if (hadExisting && File.Exists(backupPath))
                File.Delete(backupPath);
        }
        catch
        {
            try
            {
                if (File.Exists(targetPath)) File.Delete(targetPath);
                if (hadExisting && File.Exists(backupPath)) File.Move(backupPath, targetPath);
            }
            catch { }
            throw;
        }
        finally
        {
            try { if (File.Exists(backupPath)) File.Delete(backupPath); } catch { }
        }
    }
}
