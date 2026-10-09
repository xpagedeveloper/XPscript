using Android.App;
using Android.Content;

namespace XPScript.UI.Android;

/// <summary>Android document-picker capability and intent factory.</summary>
public sealed class AndroidFilePickerCapability
{
    private readonly Context _context;

    public AndroidFilePickerCapability(Context context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public Intent CreateOpenDocumentIntent(string? mimeType = null, bool allowMultiple = false)
    {
        var intent = new Intent(Intent.ActionOpenDocument)
            .AddCategory(Intent.CategoryOpenable)
            .SetType(string.IsNullOrWhiteSpace(mimeType) ? "*/*" : mimeType);
        intent.PutExtra(Intent.ExtraAllowMultiple, allowMultiple);
        return intent;
    }

    public bool IsAvailable(string? mimeType = null)
        => _context.PackageManager?.ResolveActivity(CreateOpenDocumentIntent(mimeType), global::Android.Content.PM.PackageManager.MatchDefaultOnly) is not null;
}
