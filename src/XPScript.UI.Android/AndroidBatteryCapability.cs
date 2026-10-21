using Android.Content;
using Android.OS;

namespace XPScript.UI.Android;

/// <summary>Android battery state capability.</summary>
public sealed class AndroidBatteryCapability
{
    private readonly Context _context;

    public AndroidBatteryCapability(Context context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public int? LevelPercent
    {
        get
        {
            var intent = ReadBatteryIntent();
            var level = intent?.GetIntExtra(BatteryManager.ExtraLevel, -1) ?? -1;
            var scale = intent?.GetIntExtra(BatteryManager.ExtraScale, -1) ?? -1;
            return level >= 0 && scale > 0 ? (int)Math.Round(level * 100d / scale) : null;
        }
    }

    public bool IsCharging
    {
        get
        {
            var status = ReadBatteryIntent()?.GetIntExtra(BatteryManager.ExtraStatus, -1) ?? -1;
            return status is (int)BatteryStatus.Charging or (int)BatteryStatus.Full;
        }
    }

    public bool IsLow => _context.RegisterReceiver(null, new IntentFilter(Intent.ActionBatteryLow)) is not null;

    private Intent? ReadBatteryIntent()
        => _context.RegisterReceiver(null, new IntentFilter(Intent.ActionBatteryChanged));
}
