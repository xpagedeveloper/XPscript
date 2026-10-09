using Android.App;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

[assembly: UsesPermission(global::Android.Manifest.Permission.Camera)]

namespace XPScript.UI.Android;

public sealed class App : Avalonia.Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        Styles.Add(new FluentTheme());

        if (ApplicationLifetime is IActivityApplicationLifetime activityLifetime)
            activityLifetime.MainViewFactory = () => new MainView();

        base.OnFrameworkInitializationCompleted();
    }
}
