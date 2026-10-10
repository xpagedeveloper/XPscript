using Android.Content;
using Android.Net;

#pragma warning disable CA1416 // ConnectivityManager APIs are available from Android API 23, the supported minimum is guarded by the host.

namespace XPScript.UI.Android;

/// <summary>Android network/connectivity capability.</summary>
public sealed class AndroidConnectivityCapability
{
    private readonly Context _context;

    public AndroidConnectivityCapability(Context context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    private ConnectivityManager? Manager => _context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;

    public bool IsAvailable => Manager?.ActiveNetwork is not null;

    public bool HasInternet => GetCapabilities()?.HasCapability(NetCapability.Internet) == true;

    public bool IsValidated => GetCapabilities()?.HasCapability(NetCapability.Validated) == true;

    public bool IsMetered => Manager?.IsActiveNetworkMetered == true;

    private NetworkCapabilities? GetCapabilities()
    {
        var manager = Manager;
        var network = manager?.ActiveNetwork;
        return network is null ? null : manager?.GetNetworkCapabilities(network);
    }
}

#pragma warning restore CA1416
