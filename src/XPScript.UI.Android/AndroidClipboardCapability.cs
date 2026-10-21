using Android.Content;

namespace XPScript.UI.Android;

/// <summary>Android text clipboard capability.</summary>
public sealed class AndroidClipboardCapability
{
    private readonly Context _context;

    public AndroidClipboardCapability(Context context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public bool IsAvailable => _context.GetSystemService(Context.ClipboardService) is ClipboardManager;

    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var clipboard = _context.GetSystemService(Context.ClipboardService) as ClipboardManager
            ?? throw new InvalidOperationException("Android clipboard service is unavailable.");
        clipboard.PrimaryClip = ClipData.NewPlainText("XPScript", text);
    }

    public string? GetText()
    {
        var clipboard = _context.GetSystemService(Context.ClipboardService) as ClipboardManager;
        return clipboard?.HasPrimaryClip == true ? clipboard.PrimaryClip?.GetItemAt(0)?.Text : null;
    }
}
