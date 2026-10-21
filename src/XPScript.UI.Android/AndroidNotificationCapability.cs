using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

#pragma warning disable CA1416 // Notification channels are guarded by the API 26 check.

namespace XPScript.UI.Android;

/// <summary>Android notification capability and channel setup.</summary>
public sealed class AndroidNotificationCapability
{
    private readonly Context _context;

    public AndroidNotificationCapability(Context context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public bool AreNotificationsEnabled => NotificationManagerCompat.From(_context)?.AreNotificationsEnabled() == true;

    public void EnsureChannel(string channelId, string name, NotificationImportance importance = (NotificationImportance)3)
    {
        if (string.IsNullOrWhiteSpace(channelId)) throw new ArgumentException("Notification channel id is required.", nameof(channelId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Notification channel name is required.", nameof(name));
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;
        var manager = (NotificationManager?)_context.GetSystemService(Context.NotificationService)
            ?? throw new InvalidOperationException("Android notification service is unavailable.");
        manager.CreateNotificationChannel(new NotificationChannel(channelId, name, importance));
    }
}

#pragma warning restore CA1416
