using Android.Content;
using AndroidX.Media3.Common;
using AndroidX.Media3.Exoplayer;
using AndroidX.Media3.Ui;

namespace XPScript.UI.Android;

/// <summary>
/// Isolated Media3 playback adapter for Android UIForm media controls.
/// </summary>
public sealed class AndroidMedia3Player : IDisposable
{
    private readonly ExoPlayer _player;
    private bool _disposed;

    public AndroidMedia3Player(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _player = new ExoPlayer.Builder(context).Build();
    }

    public bool IsPlaying => !_disposed && _player.IsPlaying;
    public long Position => _disposed ? 0 : Math.Max(0, _player.CurrentPosition);
    public long Duration => _disposed ? 0 : Math.Max(0, _player.Duration);
    public float Volume
    {
        get => _disposed ? 0 : _player.Volume;
        set
        {
            ThrowIfDisposed();
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(nameof(value), "Media volume must be between 0 and 1.");
            _player.Volume = value;
        }
    }

    public void Attach(PlayerView playerView)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(playerView);
        playerView.Player = _player;
    }

    public void SetSource(string source)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (!System.Uri.TryCreate(source.Trim(), System.UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https" or "file" or "content" or "android.resource"))
            throw new ArgumentException("Media3 source must be an absolute http, https, file, content or android.resource URI.", nameof(source));

        var mediaItem = MediaItem.FromUri(Android.Net.Uri.Parse(uri.AbsoluteUri));
        _player.SetMediaItem(mediaItem);
        _player.Prepare();
    }

    public void Play()
    {
        ThrowIfDisposed();
        _player.Play();
    }

    public void Pause()
    {
        ThrowIfDisposed();
        _player.Pause();
    }

    public void Stop()
    {
        ThrowIfDisposed();
        _player.Stop();
    }

    public void SeekTo(long position)
    {
        ThrowIfDisposed();
        if (position < 0) throw new ArgumentOutOfRangeException(nameof(position));
        _player.SeekTo(position);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _player.Release();
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
