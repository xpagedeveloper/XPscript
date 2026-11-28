namespace XPScript.Compiler;

/// <summary>Prevents two compiler processes from writing the same output directory.</summary>
internal sealed class CompilerDirectoryLock : IDisposable
{
    private const string FileName = ".xpscript-compile.lock";
    private readonly string _path;
    private readonly FileStream _stream;

    private CompilerDirectoryLock(string path, FileStream stream)
    {
        _path = path;
        _stream = stream;
    }

    public static CompilerDirectoryLock Acquire(string directory)
    {
        var fullDirectory = Path.GetFullPath(directory);
        Directory.CreateDirectory(fullDirectory);
        var path = Path.Combine(fullDirectory, FileName);
        try
        {
            var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.WriteLine($"pid={Environment.ProcessId}");
            writer.WriteLine($"started={DateTimeOffset.UtcNow:O}");
            writer.Flush();
            stream.Position = 0;
            return new CompilerDirectoryLock(path, stream);
        }
        catch (IOException)
        {
            throw new CompilerException(
                $"Compilation is already running for output directory '{fullDirectory}'. " +
                "Use a different output directory or wait for the existing compilation to finish.",
                CompilerDiagnosticCodes.InternalCompilationFailed,
                "configuration");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new CompilerException(
                $"Unable to create the compiler lock for output directory '{fullDirectory}': {ex.Message}",
                CompilerDiagnosticCodes.InternalCompilationFailed,
                "configuration");
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
        try { File.Delete(_path); } catch { }
    }
}
