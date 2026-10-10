using Android.Content;
using Android.OS;

namespace XPScript.UI.Android;

/// <summary>Android photo/media picker capability and intent factory.</summary>
public sealed class AndroidPhotoMediaPickerCapability
{
    private const string PhotoPickerAction = "android.provider.action.PICK_IMAGES";
    private readonly Context _context;

    public AndroidPhotoMediaPickerCapability(Context context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public Intent CreatePickIntent(string mimeType = "image/*", bool allowMultiple = false)
    {
        var intent = Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu
            ? new Intent(PhotoPickerAction)
            : new Intent(Intent.ActionGetContent).AddCategory(Intent.CategoryOpenable);
        intent.SetType(string.IsNullOrWhiteSpace(mimeType) ? "image/*" : mimeType);
        intent.PutExtra(Intent.ExtraAllowMultiple, allowMultiple);
        return intent;
    }

    public bool IsAvailable(string? mimeType = null)
        => _context.PackageManager?.ResolveActivity(CreatePickIntent(mimeType ?? "image/*"), 0) is not null;
}
