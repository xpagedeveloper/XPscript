using Android.Content;
using Android.Net;

namespace XPScript.UI.Android;

/// <summary>Android URI/app-link capability.</summary>
public sealed class AndroidUriCapability
{
    private readonly Context _context;

    public AndroidUriCapability(Context context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public Intent CreateViewIntent(string uri)
    {
        if (!Uri.TryParse(uri, out var parsed) || parsed is null)
            throw new ArgumentException("A valid URI is required.", nameof(uri));
        return new Intent(Intent.ActionView, parsed);
    }

    public bool IsAvailable(string uri)
        => _context.PackageManager?.ResolveActivity(CreateViewIntent(uri), 0) is not null;
}
