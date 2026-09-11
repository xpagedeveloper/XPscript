using System.IO.Compression;
using System.Text;

var root = args.Length > 0 ? args[0] : "./out/archive-security-fixtures";
Directory.CreateDirectory(root);

CreateZip(Path.Combine(root, "traversal.zip"), archive =>
{
    WriteText(archive, "../escape.txt", "escape");
});

CreateZip(Path.Combine(root, "absolute-unix.zip"), archive =>
{
    WriteText(archive, "/escape.txt", "escape");
});

CreateZip(Path.Combine(root, "absolute-windows.zip"), archive =>
{
    WriteText(archive, "C:/escape.txt", "escape");
});

CreateZip(Path.Combine(root, "unc.zip"), archive =>
{
    WriteText(archive, "//server/share/escape.txt", "escape");
});

CreateZip(Path.Combine(root, "symlink-entry.zip"), archive =>
{
    var entry = archive.CreateEntry("link", CompressionLevel.NoCompression);
    entry.ExternalAttributes = unchecked((int)((0xA000 | 0x1FF) << 16));
    using var writer = new StreamWriter(entry.Open(), Encoding.UTF8, leaveOpen: false);
    writer.Write("target.txt");
});

CreateZip(Path.Combine(root, "max-entries.zip"), archive =>
{
    WriteText(archive, "one.txt", "1");
    WriteText(archive, "two.txt", "2");
    WriteText(archive, "three.txt", "3");
});

CreateZip(Path.Combine(root, "max-size.zip"), archive =>
{
    WriteBytes(archive, "large.bin", new byte[4096]);
});

CreateZip(Path.Combine(root, "compression-ratio.zip"), archive =>
{
    WriteBytes(archive, "compressible.bin", new byte[1024 * 1024]);
});

var source = Path.Combine(root, "source");
Directory.CreateDirectory(source);
File.WriteAllText(Path.Combine(source, "real.txt"), "real", Encoding.UTF8);

var link = Path.Combine(source, "link.txt");
try
{
    if (File.Exists(link)) File.Delete(link);
    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        File.CreateSymbolicLink(link, "real.txt");
}
catch (Exception ex)
{
    Console.WriteLine("ARCHIVE-SECURITY-SYMLINK-SKIP=" + ex.GetType().Name);
}

Console.WriteLine("ARCHIVE-SECURITY-FIXTURES=OK");

static void CreateZip(string path, Action<ZipArchive> build)
{
    if (File.Exists(path)) File.Delete(path);
    using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
    using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
    build(archive);
}

static void WriteText(ZipArchive archive, string name, string value)
{
    var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
    using var writer = new StreamWriter(entry.Open(), Encoding.UTF8, leaveOpen: false);
    writer.Write(value);
}

static void WriteBytes(ZipArchive archive, string name, byte[] value)
{
    var entry = archive.CreateEntry(name, CompressionLevel.SmallestSize);
    using var output = entry.Open();
    output.Write(value, 0, value.Length);
}
