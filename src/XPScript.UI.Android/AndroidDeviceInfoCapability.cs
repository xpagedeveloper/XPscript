using Android.Content;
using Android.OS;

namespace XPScript.UI.Android;

/// <summary>Read-only Android device and application information.</summary>
public sealed class AndroidDeviceInfoCapability
{
    private readonly Context _context;

    public AndroidDeviceInfoCapability(Context context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public string Manufacturer => Build.Manufacturer ?? string.Empty;
    public string Model => Build.Model ?? string.Empty;
    public string AndroidVersion => Build.VERSION.Release ?? string.Empty;
    public int ApiLevel => (int)Build.VERSION.SdkInt;
    public string PackageName => _context.PackageName ?? string.Empty;
}
