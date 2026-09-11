using System.Net.Http;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace XPScript.UI.Desktop;

internal static class DesktopImageHost
{
    private const int MaximumImageBytes = 32 * 1024 * 1024;
    private static HttpClient CreateHttpClient(string certificateValidation)
    {
        var mode = string.IsNullOrWhiteSpace(certificateValidation) ? "Strict" : certificateValidation.Trim();
        if (!(mode.Equals("Strict", StringComparison.OrdinalIgnoreCase) || mode.Equals("AllowSelfSigned", StringComparison.OrdinalIgnoreCase) || mode.Equals("Insecure", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Image certificate validation must be Strict, AllowSelfSigned, or Insecure.");
        string tlsError = string.Empty;
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                     System.Net.DecompressionMethods.Deflate |
                                     System.Net.DecompressionMethods.Brotli,
            ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) =>
            {
                if (errors == System.Net.Security.SslPolicyErrors.None) return true;
                var details = new List<string>();
                if ((errors & System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch) != 0) details.Add("certificate hostname mismatch");
                if ((errors & System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable) != 0) details.Add("server certificate missing");
                var statuses = chain?.ChainStatus ?? [];
                foreach (var status in statuses) details.Add(status.StatusInformation.Trim().Length > 0 ? status.StatusInformation.Trim() : status.Status.ToString());
                tlsError = details.Count > 0 ? string.Join("; ", details) : "certificate is not trusted";
                if (mode.Equals("Insecure", StringComparison.OrdinalIgnoreCase)) return true;
                if (mode.Equals("AllowSelfSigned", StringComparison.OrdinalIgnoreCase))
                    return (errors & (System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch | System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable)) == 0 &&
                           statuses.Length > 0 && statuses.All(x => x.Status is System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.UntrustedRoot or System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.PartialChain or System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.NoError);
                return false;
            }
        };
        var client = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(15) };
        return client;
    }

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

    public static string ToWebSource(string source, string certificateValidation = "Strict")
    {
        var value = (source ?? string.Empty).Trim();
        if (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) return value;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            using var http = CreateHttpClient(certificateValidation);
            try
            {
                var remoteBytes = http.GetByteArrayAsync(uri).GetAwaiter().GetResult();
                ValidateSize(remoteBytes.LongLength);
                var remoteMime = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant() switch
                {
                    ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp", ".bmp" => "image/bmp", ".svg" => "image/svg+xml", _ => "application/octet-stream"
                };
                return "data:" + remoteMime + ";base64," + Convert.ToBase64String(remoteBytes);
            }
            catch (HttpRequestException ex) { throw new InvalidOperationException("TLS/HTTP image request failed: " + ex.Message, ex); }
        }

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
            using var http = CreateHttpClient("Strict");
            var bytes = http.GetByteArrayAsync(uri).GetAwaiter().GetResult();
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
