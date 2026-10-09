using Android.App;
using Avalonia.Controls;

namespace XPScript.UI.Android;

/// <summary>
/// Headless Avalonia host for UIForm.Audio. Media3 remains isolated from the shared UI model.
/// </summary>
public sealed class AndroidAudioControl : Control, IDisposable
{
    private readonly AndroidMedia3Player _mediaPlayer;
    private string _source = string.Empty;
    private bool _disposed;

    public AndroidAudioControl()
    {
        var context = Application.Context
            ?? throw new InvalidOperationException("Android application context is unavailable.");
        _mediaPlayer = new AndroidMedia3Player(context);
        IsVisible = false;
        Width = 0;
        Height = 0;
    }

    public AndroidMedia3Player MediaPlayer => _mediaPlayer;
    public bool IsPlaying => _mediaPlayer.IsPlaying;
    public long Position => _mediaPlayer.Position;
    public long Duration => _mediaPlayer.Duration;
    public float Volume { get => _mediaPlayer.Volume; set => _mediaPlayer.Volume = value; }
    public void Play() => _mediaPlayer.Play();
    public void Pause() => _mediaPlayer.Pause();
    public void Stop() => _mediaPlayer.Stop();

    public string Source
    {
        get => _source;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _source = value ?? string.Empty;
            if (_source.Length > 0) _mediaPlayer.SetSource(_source);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _mediaPlayer.Dispose();
        GC.SuppressFinalize(this);
    }
}
