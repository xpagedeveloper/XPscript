using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace XPScript.UI.Android;

[Activity(
    Label = "XPScript",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
}

[Application]
public sealed class AndroidApp : AvaloniaAndroidApplication<App>
{
    protected AndroidApp(
        nint javaReference,
        Android.Runtime.JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }
}
