using System.Net.Http;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace XPScript.UI.Desktop;

internal static class DesktopImageHost
{
    private const int MaximumImageBytes = 32 * 1024 * 1024;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static Control Create(string source, string altText)
    {
        var bytes = ReadImageBytes(source);
        using var stream = new MemoryStream(bytes, writable: false);
        var bitmap = new Bitmap(stream);
        var image = new Image
        {
            Source = bitmap,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            MaxHeight = 720
        };
        image.DetachedFromVisualTree += (_, _) => bitmap.Dispose();
        if (!string.IsNullOrWhiteSpace(altText)) AutomationProperties.SetName(image, altText);
        return image;
    }

    public static string ToWebSource(string source)
    {
        var value = (source ?? string.Empty).Trim();
        if (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) return value;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") return value;

        var path = ResolveLocalPath(value);
        var bytes = ReadImageBytes(path);
        var mime = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
        return "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
    }

    private static byte[] ReadImageBytes(string source)
    {
        var value = (source ?? string.Empty).Trim();
        if (value.Length == 0) throw new InvalidOperationException("UIForm image source is empty.");

        if (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            var comma = value.IndexOf(',');
            if (comma <= 0 || !value[..comma].Contains(";base64", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("UIForm desktop Image supports base64 data:image URLs.");
            var bytes = Convert.FromBase64String(value[(comma + 1)..]);
            ValidateSize(bytes.LongLength);
            return bytes;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            var bytes = Http.GetByteArrayAsync(uri).GetAwaiter().GetResult();
            ValidateSize(bytes.LongLength);
            return bytes;
        }

        var path = ResolveLocalPath(value);
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("UIForm image asset was not found.", path);
        if (info.LinkTarget is not null || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("UIForm image assets may not resolve through symbolic links or reparse points.");
        ValidateSize(info.Length);
        return File.ReadAllBytes(path);
    }

    private static string ResolveLocalPath(string value)
    {
        if (Path.IsPathRooted(value)) return Path.GetFullPath(value);
        if (value.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("UIForm image relative path may not contain '..'.");

        var normalized = value.Replace('/', Path.DirectorySeparatorChar);
        var appCandidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, normalized));
        if (File.Exists(appCandidate)) return appCandidate;

        var workingCandidate = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, normalized));
        return workingCandidate;
    }

    private static void ValidateSize(long length)
    {
        if (length <= 0 || length > MaximumImageBytes)
            throw new InvalidOperationException("UIForm image must contain between 1 byte and 32 MiB.");
    }
}
