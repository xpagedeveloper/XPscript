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
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    protected AndroidApp(
        nint javaReference,
        global::Android.Runtime.JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }
}
