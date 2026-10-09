using Android.Content;
using Android.OS;

namespace XPScript.UI.Android;

/// <summary>Android vibration/haptics capability.</summary>
public sealed class AndroidHapticsCapability
{
    private readonly Context _context;

    public AndroidHapticsCapability(Context context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public bool IsAvailable => _context.GetSystemService(Context.VibratorService) is Vibrator vibrator && vibrator.HasVibrator;

    public void Vibrate(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        var vibrator = _context.GetSystemService(Context.VibratorService) as Vibrator
            ?? throw new InvalidOperationException("Android vibration service is unavailable.");
        var milliseconds = Math.Max(1L, (long)duration.TotalMilliseconds);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            vibrator.Vibrate(VibrationEffect.CreateOneShot(milliseconds, VibrationEffect.DefaultAmplitude));
        else
            vibrator.Vibrate(milliseconds);
    }
}
