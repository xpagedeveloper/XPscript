namespace XPScript.Compiler;

internal static class XPImageRuntimeSource
{
    public const string Code = """
internal sealed class XPImage : System.IDisposable
{
    private const long MaxEncodedBytes = 64L * 1024 * 1024;
    private const long MaxPixels = 100_000_000;
    private const int MaxDimension = 32_768;
    private const int MaxMetadataNameChars = 256;
    private const int MaxMetadataValueChars = 64 * 1024;
    private const int MaxProfileBytes = 8 * 1024 * 1024;
    private ImageMagick.MagickImage? _image;
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

    public int Width => checked((int)Image.Width);
    public int Height => checked((int)Image.Height);
    public string Format => _format;
    private ImageMagick.MagickImage Image => _image ?? throw new System.ObjectDisposedException(nameof(XPImage));
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

    public static XPImage FromBase64(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) throw new System.ArgumentException("Base64 image data cannot be empty.", nameof(data));
        var value = data.Trim();
        var comma = value.IndexOf(',');
        if (value.StartsWith("data:", System.StringComparison.OrdinalIgnoreCase))
        {
            if (comma < 0 || value[..comma].IndexOf(";base64", System.StringComparison.OrdinalIgnoreCase) < 0)
                throw new System.ArgumentException("Image data URI must use base64 encoding.", nameof(data));
            value = value[(comma + 1)..];
        }
        if (value.Length > ((MaxEncodedBytes + 2) / 3) * 4 + 16)
            throw new System.InvalidOperationException("Base64 image exceeds the maximum encoded size.");
        try
        {
            return FromBytes(System.Convert.FromBase64String(value));
        }
        catch (System.FormatException ex)
        {
            throw new System.ArgumentException("Image data is not valid base64.", nameof(data), ex);
        }
    }

    public static XPImage FromBytes(byte[] data)
    {
        System.ArgumentNullException.ThrowIfNull(data);
        if (data.LongLength == 0) throw new System.ArgumentException("Image data cannot be empty.", nameof(data));
        if (data.LongLength > MaxEncodedBytes) throw new System.InvalidOperationException("Image exceeds the maximum encoded size.");
        try
        {
            var info = new ImageMagick.MagickImageInfo(data);
            ValidateDimensions(checked((int)info.Width), checked((int)info.Height));
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
    }

    public void Resize(int width, int height, string mode)
    {
        ValidateDimensions(width, height);
        var normalized = (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0 || normalized == "stretch")
        {
            _image.Resize((uint)width, (uint)height);
            return;
        }

        var scaleX = width / (double)Width;
        var scaleY = height / (double)Height;
        var scale = normalized switch
        {
            "fit" or "contain" => System.Math.Min(scaleX, scaleY),
            "fill" or "crop" => System.Math.Max(scaleX, scaleY),
            _ => throw new System.ArgumentException("Resize mode must be stretch, fit, contain, fill or crop.", nameof(mode))
        };
        var resizedWidth = System.Math.Max(1, checked((int)System.Math.Round(Width * scale)));
        var resizedHeight = System.Math.Max(1, checked((int)System.Math.Round(Height * scale)));
        ValidateDimensions(resizedWidth, resizedHeight);
        _image.Resize((uint)resizedWidth, (uint)resizedHeight);

        if (normalized is "fill" or "crop")
        {
            var x = System.Math.Max(0, (resizedWidth - width) / 2);
            var y = System.Math.Max(0, (resizedHeight - height) / 2);
            _image.Crop(new ImageMagick.MagickGeometry(x, y, (uint)width, (uint)height));
        }
    }

    public void Pad(int width, int height, string background)
    {
        ValidateDimensions(width, height);
        if (width < Width || height < Height)
            throw new System.ArgumentOutOfRangeException(nameof(width), "Canvas cannot be smaller than the image.");
        _image.BackgroundColor = ParseColor(background);
        _image.Extent((uint)width, (uint)height, ImageMagick.Gravity.Center);
    }

    public void Composite(XPImage overlay, int x, int y)
    {
        System.ArgumentNullException.ThrowIfNull(overlay);
        _image.Composite(overlay._image, x, y, ImageMagick.CompositeOperator.Over);
    }

    public void Composite(XPImage overlay, int x, int y, double opacity)
    {
        System.ArgumentNullException.ThrowIfNull(overlay);
        ValidateOpacity(opacity);
        using var copy = (ImageMagick.MagickImage)overlay._image.Clone();
        copy.Evaluate(ImageMagick.Channels.Alpha, ImageMagick.EvaluateOperator.Multiply, opacity);
        _image.Composite(copy, x, y, ImageMagick.CompositeOperator.Over);
    }

    public void Opacity(double opacity)
    {
        ValidateOpacity(opacity);
        _image.Evaluate(ImageMagick.Channels.Alpha, ImageMagick.EvaluateOperator.Multiply, opacity);
    }

    public void Flatten(string background)
    {
        _image.BackgroundColor = ParseColor(background);
        _image.Alpha(ImageMagick.AlphaOption.Remove);
    }

    public void Rotate(double degrees)
    {
        if (double.IsNaN(degrees) || double.IsInfinity(degrees))
            throw new System.ArgumentOutOfRangeException(nameof(degrees));
        var radians = degrees * (System.Math.PI / 180d);
        var cosine = System.Math.Abs(System.Math.Cos(radians));
        var sine = System.Math.Abs(System.Math.Sin(radians));
        var rotatedWidth = checked((int)System.Math.Ceiling((Width * cosine) + (Height * sine)));
        var rotatedHeight = checked((int)System.Math.Ceiling((Width * sine) + (Height * cosine)));
        ValidateDimensions(rotatedWidth, rotatedHeight);
        _image.Rotate(degrees);
        ValidateDimensions(Width, Height);
    }
    public void FlipHorizontal() => _image.Flop();
    public void FlipVertical() => _image.Flip();
    public void Brightness(double value)
    {
        ValidatePercentage(value, nameof(value));
        _image.Modulate(new ImageMagick.Percentage(100d + value), new ImageMagick.Percentage(100d), new ImageMagick.Percentage(100d));
    }

    public void Contrast(double value)
    {
        ValidatePercentage(value, nameof(value));
        _image.BrightnessContrast(new ImageMagick.Percentage(0d), new ImageMagick.Percentage(value));
    }

    public void Saturation(double value)
    {
        ValidatePercentage(value, nameof(value));
        _image.Modulate(new ImageMagick.Percentage(100d), new ImageMagick.Percentage(100d + value), new ImageMagick.Percentage(100d));
    }

    public void Grayscale() => _image.Grayscale();
    public void Invert() => _image.Negate();
    public void Blur(double radius) => _image.Blur(radius, radius <= 0 ? 1.0 : radius);
    public void Sharpen(double amount) => _image.Sharpen(0, amount <= 0 ? 1.0 : amount);

    public void AutoOrient() => _image.AutoOrient();

    public string GetExif(string name)
    {
        var tag = ParseExifStringTag(name);
        var profile = Image.GetExifProfile();
        if (profile is null) return string.Empty;
        var value = profile.GetValue(tag);
        return ValidateMetadataValue(value?.GetValue()?.ToString() ?? string.Empty);
    }

    public void SetExif(string name, string value)
    {
        var tag = ParseExifStringTag(name);
        var safeValue = ValidateMetadataValue(value ?? string.Empty);
        var profile = Image.GetExifProfile() ?? new ImageMagick.ExifProfile();
        profile.SetValue(tag, safeValue);
        Image.SetProfile(profile);
    }

    public string GetMetadata(string name)
    {
        var normalized = ValidateMetadataName(name);
        return ValidateMetadataValue(_image.GetAttribute(normalized) ?? string.Empty);
    }

    public void SetMetadata(string name, string value)
    {
        var normalized = ValidateMetadataName(name);
        _image.SetAttribute(normalized, ValidateMetadataValue(value ?? string.Empty));
    }

    public byte[] GetProfile(string name)
    {
        var normalized = NormalizeProfileName(name);
        var profile = Image.GetProfile(normalized);
        if (profile is null) return System.Array.Empty<byte>();
        var bytes = profile.ToByteArray();
        if (bytes.LongLength > MaxProfileBytes) throw new System.InvalidOperationException("Image profile exceeds the maximum size.");
        return bytes;
    }

    public void SetProfile(string name, byte[] data)
    {
        var normalized = NormalizeProfileName(name);
        System.ArgumentNullException.ThrowIfNull(data);
        if (data.LongLength > MaxProfileBytes) throw new System.InvalidOperationException("Image profile exceeds the maximum size.");
        Image.SetProfile(new ImageMagick.ImageProfile(normalized, data));
    }

    public void SetSrgbProfile()
    {
        Image.SetProfile(ImageMagick.ColorProfiles.SRGB);
    }

    public void StripMetadata()
    {
        _image.Strip();
    }

    public byte[] ToBytes(string format) => ToBytes(format, null);

    public byte[] ToBytes(string format, int? quality)
    {
        var normalized = NormalizeFormat(format);
        Image.Format = ParseFormat(normalized);
        ApplyEncodingQuality(normalized, quality);
        var bytes = Image.ToByteArray();
        if (bytes.LongLength > MaxEncodedBytes)
            throw new System.InvalidOperationException("Encoded image exceeds the maximum size.");
        _format = normalized;
        return bytes;
    }

    public void Save(string path) => Save(path, null);

    public void Save(string path, int? quality)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new System.ArgumentException("Image path cannot be empty.", nameof(path));
        var resolved = XPScriptFileSystemRuntime.ResolvePath(path);
        var format = NormalizeFormat(System.IO.Path.GetExtension(resolved).TrimStart('.'));
        Image.Format = ParseFormat(format);
        ApplyEncodingQuality(format, quality);
        Image.Write(resolved);
        _format = format;
    }

    public static void RecycleOwned(object? value)
    {
        if (value is XPImage image) image.Recycle();
    }

    public void Recycle()
    {
        var image = System.Threading.Interlocked.Exchange(ref _image, null);
        image?.Dispose();
    }

    void System.IDisposable.Dispose() => Recycle();

    private static string ValidateMetadataName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new System.ArgumentException("Metadata name cannot be empty.", nameof(name));
        var normalized = name.Trim();
        if (normalized.Length > MaxMetadataNameChars) throw new System.InvalidOperationException("Image metadata name exceeds the maximum length.");
        return normalized;
    }

    private static string ValidateMetadataValue(string value)
    {
        if (value.Length > MaxMetadataValueChars) throw new System.InvalidOperationException("Image metadata value exceeds the maximum length.");
        return value;
    }

    private static string NormalizeProfileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new System.ArgumentException("Profile name cannot be empty.", nameof(name));
        var normalized = name.Trim().ToLowerInvariant();
        if (normalized == "icm") normalized = "icc";
        if (normalized is not ("icc" or "xmp"))
            throw new System.ArgumentException("Supported XPImage profile names are ICC and XMP.", nameof(name));
        return normalized;
    }

    private static ImageMagick.ExifTag<string> ParseExifStringTag(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new System.ArgumentException("EXIF name cannot be empty.", nameof(name));
        return name.Trim().ToLowerInvariant() switch
        {
            "artist" => ImageMagick.ExifTag.Artist,
            "copyright" => ImageMagick.ExifTag.Copyright,
            "datetime" => ImageMagick.ExifTag.DateTime,
            "documentname" => ImageMagick.ExifTag.DocumentName,
            "imagedescription" => ImageMagick.ExifTag.ImageDescription,
            "make" => ImageMagick.ExifTag.Make,
            "model" => ImageMagick.ExifTag.Model,
            "software" => ImageMagick.ExifTag.Software,
            _ => throw new System.ArgumentException("Unsupported XPImage EXIF string tag: " + name, nameof(name))
        };
    }

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

    private void ApplyEncodingQuality(string format, int? quality)
    {
        if (!quality.HasValue) return;
        if (quality.Value < 1 || quality.Value > 100) throw new System.ArgumentOutOfRangeException(nameof(quality));
        if (format is not ("jpeg" or "webp" or "png"))
            throw new System.ArgumentException("Quality is supported only for JPEG, WebP and PNG output.", nameof(quality));
        Image.Quality = (uint)quality.Value;
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

    private static void ValidatePercentage(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < -100d || value > 100d)
            throw new System.ArgumentOutOfRangeException(name, "Value must be between -100 and 100.");
    }

    private static void ValidateOpacity(double opacity)
    {
        if (double.IsNaN(opacity) || double.IsInfinity(opacity) || opacity < 0d || opacity > 1d)
            throw new System.ArgumentOutOfRangeException(nameof(opacity), "Opacity must be between 0 and 1.");
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension || (long)width * height > MaxPixels)
            throw new System.ArgumentOutOfRangeException(nameof(width), "Image dimensions exceed XPImage limits.");
    }
}
""";
}
