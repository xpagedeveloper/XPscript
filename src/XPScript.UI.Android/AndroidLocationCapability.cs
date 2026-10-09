using Android.Content;
using Android.Locations;
using AndroidX.Core.Content;

namespace XPScript.UI.Android;

/// <summary>Read-only Android location capability probe. It does not start updates or request permission.</summary>
public sealed class AndroidLocationCapability
{
    private readonly Context _context;

    public AndroidLocationCapability(Context context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public bool HasFinePermission => ContextCompat.CheckSelfPermission(_context, global::Android.Manifest.Permission.AccessFineLocation) == global::Android.Content.PM.Permission.Granted;
    public bool HasCoarsePermission => ContextCompat.CheckSelfPermission(_context, global::Android.Manifest.Permission.AccessCoarseLocation) == global::Android.Content.PM.Permission.Granted;
    public bool HasBackgroundPermission => ContextCompat.CheckSelfPermission(_context, global::Android.Manifest.Permission.AccessBackgroundLocation) == global::Android.Content.PM.Permission.Granted;
    public bool HasPermission => HasFinePermission || HasCoarsePermission;
    public bool IsLocationEnabled
    {
        get
        {
            var manager = (LocationManager?)_context.GetSystemService(Context.LocationService);
            return manager?.IsProviderEnabled(LocationManager.GpsProvider) == true || manager?.IsProviderEnabled(LocationManager.NetworkProvider) == true;
        }
    }

    public IReadOnlyList<string> EnabledProviders
    {
        get
        {
            var manager = (LocationManager?)_context.GetSystemService(Context.LocationService);
            return manager?.GetProviders(true)?.Where(provider => provider is LocationManager.GpsProvider or LocationManager.NetworkProvider).ToArray()
                ?? Array.Empty<string>();
        }
    }

    public string SelectProvider()
    {
        if (!HasPermission) throw new InvalidOperationException("Location permission has not been granted.");
        var providers = EnabledProviders;
        if (providers.Contains(LocationManager.GpsProvider, StringComparer.OrdinalIgnoreCase)) return LocationManager.GpsProvider;
        if (providers.Contains(LocationManager.NetworkProvider, StringComparer.OrdinalIgnoreCase)) return LocationManager.NetworkProvider;
        throw new InvalidOperationException("No Android location provider is enabled.");
    }
}
