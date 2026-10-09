using Android.Content;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.UI;

namespace XPScript.UI.Android;

/// <summary>
/// Isolated Media3 playback adapter for Android UIForm media controls.
/// </summary>
public sealed class AndroidMedia3Player : IDisposable
{
    private readonly IExoPlayer _player;
    private bool _disposed;
    private readonly Media3Listener _listener;
    public event EventHandler<bool>? IsPlayingChanged;
    public event EventHandler<int>? PlaybackStateChanged;
    public event EventHandler<string>? PlaybackError;

    public AndroidMedia3Player(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _player = new ExoPlayerBuilder(context).Build()
            ?? throw new InvalidOperationException("Media3 ExoPlayerBuilder returned no player.");
        _listener = new Media3Listener(this);
        _player.AddListener(_listener);
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

        var mediaItem = MediaItem.FromUri(global::Android.Net.Uri.Parse(uri.AbsoluteUri));
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
        _player.RemoveListener(_listener);
        _player.Release();
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class Media3Listener : Java.Lang.Object, IPlayerListener
    {
        private readonly AndroidMedia3Player _owner;
        public Media3Listener(AndroidMedia3Player owner) => _owner = owner;
        public void OnIsPlayingChanged(bool isPlaying) => _owner.IsPlayingChanged?.Invoke(_owner, isPlaying);
        public void OnPlaybackStateChanged(int playbackState) => _owner.PlaybackStateChanged?.Invoke(_owner, playbackState);
        public void OnPlayerError(PlaybackException? error) => _owner.PlaybackError?.Invoke(_owner, error?.Message ?? "Media3 playback error.");
    }
}
