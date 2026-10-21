using Android.Content;

namespace XPScript.UI.Android;

/// <summary>Android URI/app-link capability.</summary>
public sealed class AndroidUriCapability
{
    private readonly Context _context;

    public AndroidUriCapability(Context context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public Intent CreateViewIntent(string uri)
    {
        if (!global::System.Uri.TryCreate(uri, global::System.UriKind.Absolute, out _))
            throw new ArgumentException("A valid URI is required.", nameof(uri));
        return new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(uri));
    }

    public bool IsAvailable(string uri)
        => _context.PackageManager?.ResolveActivity(CreateViewIntent(uri), 0) is not null;
}
