using Android.App;
using Android.Views;
using Avalonia.Controls;
using Avalonia.Platform;
using AndroidX.Media3.UI;

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
    public bool IsPlaying => _mediaPlayer?.IsPlaying == true;
    public long Position => _mediaPlayer?.Position ?? 0;
    public long Duration => _mediaPlayer?.Duration ?? 0;
    public float Volume { get => _mediaPlayer?.Volume ?? 0; set => (_mediaPlayer ?? throw new InvalidOperationException("Video player is not initialized.")).Volume = value; }
    public void Play() => (_mediaPlayer ?? throw new InvalidOperationException("Video player is not initialized.")).Play();
    public void Pause() => (_mediaPlayer ?? throw new InvalidOperationException("Video player is not initialized.")).Pause();
    public void Stop() => (_mediaPlayer ?? throw new InvalidOperationException("Video player is not initialized.")).Stop();
    public void SeekTo(long position) => (_mediaPlayer ?? throw new InvalidOperationException("Video player is not initialized.")).SeekTo(position);

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
