using Android.App;
using Android.Content.PM;
using Android.Util;
using Avalonia.Android;

namespace XPScript.UI.Android;

[Activity(
    Label = "XPScript",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
    internal static MainActivity? Current { get; private set; }

    protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
    {
        Current = this;
        base.OnCreate(savedInstanceState);
    }

    internal void RequestCameraPermission()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(23) &&
            global::AndroidX.Core.Content.ContextCompat.CheckSelfPermission(this, global::Android.Manifest.Permission.Camera) != Permission.Granted)
            RequestPermissions(new[] { global::Android.Manifest.Permission.Camera }, 7001);
    }

    protected override void OnStart()
    {
        base.OnStart();
        Log.Info("XPScript", "XPSCRIPT-LIFECYCLE=start");
    }

    protected override void OnResume()
    {
        base.OnResume();
        Log.Info("XPScript", "XPSCRIPT-LIFECYCLE=resume");
    }

    protected override void OnPause()
    {
        Log.Info("XPScript", "XPSCRIPT-LIFECYCLE=pause");
        base.OnPause();
    }

    protected override void OnStop()
    {
        Log.Info("XPScript", "XPSCRIPT-LIFECYCLE=stop");
        base.OnStop();
    }

    protected override void OnDestroy()
    {
        if (ReferenceEquals(Current, this)) Current = null;
        base.OnDestroy();
    }
}

[Application]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    protected AndroidApp(
        nint javaReference,
        global::Android.Runtime.JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }
}
