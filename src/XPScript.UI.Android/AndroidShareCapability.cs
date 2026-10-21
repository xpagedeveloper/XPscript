using Android.Content;

namespace XPScript.UI.Android;

/// <summary>Android share-sheet capability and intent factory.</summary>
public sealed class AndroidShareCapability
{
    private readonly Context _context;

    public AndroidShareCapability(Context context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public Intent CreateShareIntent(string text, string mimeType = "text/plain")
    {
        ArgumentNullException.ThrowIfNull(text);
        return new Intent(Intent.ActionSend)
            .SetType(string.IsNullOrWhiteSpace(mimeType) ? "text/plain" : mimeType)
            .PutExtra(Intent.ExtraText, text);
    }

    public bool IsAvailable(string mimeType = "text/plain")
        => _context.PackageManager?.ResolveActivity(CreateShareIntent(string.Empty, mimeType), 0) is not null;
}
