namespace XPScript.Compiler;

internal static class XPImageRuntimeSource
{
    public const string Code = """
internal sealed class XPImage : System.IDisposable
{
    private const long MaxEncodedBytes = 64L * 1024 * 1024;
    private const long MaxPixels = 100_000_000;
    private const int MaxDimension = 32_768;
    private ImageMagick.MagickImage _image;
    private string _format;

    public XPImage(int width, int height) : this(width, height, "transparent") { }

    public XPImage(int width, int height, string background)
    {
        ValidateDimensions(width, height);
        _image = new ImageMagick.MagickImage(ParseColor(background), (uint)width, (uint)height);
        _format = "png";
    }

    private XPImage(ImageMagick.MagickImage image, string format)
    {
        _image = image;
        ValidateDimensions(checked((int)image.Width), checked((int)image.Height));
        _format = NormalizeFormat(format);
    }

    public int Width => checked((int)_image.Width);
    public int Height => checked((int)_image.Height);
    public string Format => _format;
    public double DpiX => _image.Density?.X ?? 0d;
    public double DpiY => _image.Density?.Y ?? 0d;

    public static XPImage Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new System.ArgumentException("Image path cannot be empty.", nameof(path));
        var resolved = XPScriptFileSystemRuntime.ResolvePath(path);
        var info = new System.IO.FileInfo(resolved);
        if (!info.Exists) throw new System.IO.FileNotFoundException("Image file was not found.", resolved);
        if (info.Length > MaxEncodedBytes) throw new System.InvalidOperationException("Image exceeds the maximum encoded size.");
        return FromBytes(System.IO.File.ReadAllBytes(resolved));
    }

    public static XPImage FromBytes(byte[] data)
    {
        System.ArgumentNullException.ThrowIfNull(data);
        if (data.LongLength == 0) throw new System.ArgumentException("Image data cannot be empty.", nameof(data));
        if (data.LongLength > MaxEncodedBytes) throw new System.InvalidOperationException("Image exceeds the maximum encoded size.");
        try
        {
            var image = new ImageMagick.MagickImage(data);
            var format = image.Format.ToString();
            return new XPImage(image, format);
        }
        catch (ImageMagick.MagickException ex)
        {
            throw new System.InvalidOperationException("Image data is unsupported or malformed.", ex);
        }
    }

    public XPImage Clone() => new((ImageMagick.MagickImage)_image.Clone(), _format);

    public void Resize(int width, int height)
    {
        ValidateDimensions(width, height);
        _image.Resize((uint)width, (uint)height);
    }

    public void Resize(int width)
    {
        if (width <= 0) throw new System.ArgumentOutOfRangeException(nameof(width));
        var height = checked((int)System.Math.Round(Height * (width / (double)Width)));
        ValidateDimensions(width, height);
        _image.Resize((uint)width, (uint)height);
    }

    public void Crop(int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || width <= 0 || height <= 0 || (long)x + width > Width || (long)y + height > Height)
            throw new System.ArgumentOutOfRangeException(nameof(width), "Crop rectangle must be inside the image.");
        _image.Crop(new ImageMagick.MagickGeometry(x, y, (uint)width, (uint)height));
        _image.RePage();
    }

    public void Rotate(double degrees) => _image.Rotate(degrees);
    public void FlipHorizontal() => _image.Flop();
    public void FlipVertical() => _image.Flip();
    public void Grayscale() => _image.Grayscale();
    public void Invert() => _image.Negate();
    public void Blur(double radius) => _image.Blur(radius, radius <= 0 ? 1.0 : radius);
    public void Sharpen(double amount) => _image.Sharpen(0, amount <= 0 ? 1.0 : amount);

    public void AutoOrient() => _image.AutoOrient();

    public void StripMetadata()
    {
        _image.Strip();
    }

    public byte[] ToBytes(string format)
    {
        var normalized = NormalizeFormat(format);
        _image.Format = ParseFormat(normalized);
        var bytes = _image.ToByteArray();
        _format = normalized;
        return bytes;
    }

    public void Save(string path) => Save(path, null);

    public void Save(string path, int? quality)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new System.ArgumentException("Image path cannot be empty.", nameof(path));
        var resolved = XPScriptFileSystemRuntime.ResolvePath(path);
        var format = NormalizeFormat(System.IO.Path.GetExtension(resolved).TrimStart('.'));
        _image.Format = ParseFormat(format);
        if (quality.HasValue)
        {
            if (quality.Value < 1 || quality.Value > 100) throw new System.ArgumentOutOfRangeException(nameof(quality));
            _image.Quality = (uint)quality.Value;
        }
        _image.Write(resolved);
        _format = format;
    }

    public void Dispose() => _image.Dispose();

    private static ImageMagick.MagickColor ParseColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new ImageMagick.MagickColor("transparent");
        return new ImageMagick.MagickColor(value);
    }

    private static string NormalizeFormat(string value)
    {
        var format = (value ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
        return format switch
        {
            "jpg" or "jpeg" => "jpeg",
            "png" => "png",
            "webp" => "webp",
            "gif" => "gif",
            "bmp" => "bmp",
            "tif" or "tiff" => "tiff",
            _ => throw new System.NotSupportedException("Unsupported image format: " + value)
        };
    }

    private static ImageMagick.MagickFormat ParseFormat(string format) => format switch
    {
        "jpeg" => ImageMagick.MagickFormat.Jpeg,
        "png" => ImageMagick.MagickFormat.Png,
        "webp" => ImageMagick.MagickFormat.WebP,
        "gif" => ImageMagick.MagickFormat.Gif,
        "bmp" => ImageMagick.MagickFormat.Bmp,
        "tiff" => ImageMagick.MagickFormat.Tiff,
        _ => throw new System.NotSupportedException("Unsupported image format: " + format)
    };

    private static void ValidateDimensions(int width, int height)
    {
        if (width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension || (long)width * height > MaxPixels)
            throw new System.ArgumentOutOfRangeException(nameof(width), "Image dimensions exceed XPImage limits.");
    }
}
""";
}
