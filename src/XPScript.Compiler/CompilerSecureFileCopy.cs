namespace XPScript.Compiler;

internal static class CompilerSecureFileCopy
{
    public static void CopyValidatedRegularFile(string sourcePath, string destinationPath, string kind)
    {
        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);

        RejectLinkedSource(source, kind);

        try
        {
            using var input = new FileStream(
                source,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                options: FileOptions.SequentialScan);

            // Re-check the path immediately after the handle has been opened. The copy below
            // then reads from this already-open handle rather than re-opening sourcePath.
            RejectLinkedSource(source, kind);

            using var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                options: FileOptions.SequentialScan);

            input.CopyTo(output);
            output.Flush(flushToDisk: true);
        }
        catch (CompilerException)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            throw new CompilerException(kind + " changed or disappeared before it could be staged.");
        }
        catch (DirectoryNotFoundException)
        {
            throw new CompilerException(kind + " could not be staged because a required directory is unavailable.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new CompilerException(kind + " could not be staged because access was denied.");
        }
        catch (IOException)
        {
            throw new CompilerException(kind + " could not be staged safely.");
        }
    }

    private static void RejectLinkedSource(string sourcePath, string kind)
    {
        try
        {
            var info = new FileInfo(sourcePath);
            if (!info.Exists)
                throw new CompilerException(kind + " was not found while preparing compiler staging.");

            if (info.LinkTarget is not null || (info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new CompilerException(kind + " may not be a symbolic link or reparse-point file during compiler staging.");
        }
        catch (CompilerException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new CompilerException("Unable to safely inspect " + kind + " before compiler staging.");
        }
    }
}
