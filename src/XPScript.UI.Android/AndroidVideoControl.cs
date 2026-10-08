using Android.App;
using Android.Views;
using Avalonia.Controls;
using Avalonia.Platform;
using Xamarin.Android;

namespace XPScript.UI.Android;

public sealed class AndroidVideoControl : NativeControlHost
{
    private AndroidMedia3Player? _mediaPlayer;
    private PlayerView? _playerView;
    private string _source = string.Empty;

    public string Source
    {
        get => _source;
        set
        {
            _source = value ?? string.Empty;
            if (_mediaPlayer is not null && _source.Length > 0)
                _mediaPlayer.SetSource(_source);
        }
    }

    public AndroidMedia3Player? MediaPlayer => _mediaPlayer;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var context = Application.Context
            ?? throw new InvalidOperationException("Android application context is unavailable.");

        _mediaPlayer = new AndroidMedia3Player(context);
        _playerView = new PlayerView(context);
        _mediaPlayer.Attach(_playerView);

        if (_source.Length > 0)
            _mediaPlayer.SetSource(_source);

        return new PlatformHandle(
            _playerView.Handle,
            "Android.Media3.PlayerView");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _mediaPlayer?.Dispose();
        _mediaPlayer = null;
        _playerView?.Dispose();
        _playerView = null;
        base.DestroyNativeControlCore(control);
    }
}
