namespace XPScript.Compiler;

internal static class AndroidUIHostSource
{
    public static string Build(bool debug)
        => Code
            .Replace("__XPSCRIPT_ANDROID_DEBUG__", debug ? "true" : "false", StringComparison.Ordinal)
            .Replace("[assembly: UsesPermission(global::Android.Manifest.Permission.Camera)]", string.Empty, StringComparison.Ordinal);

    public const string Code = """
using System.Text.Json;
using System.Collections.Concurrent;
using global::Android.App;
using global::Android.Content.PM;
using global::Android.OS;
using global::Android.Util;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.UI;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Core.Content;
using AndroidX.Lifecycle;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls.Primitives;
using Avalonia.Themes.Fluent;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace XPScript.UI.Android;

internal static class AndroidDebugMode
{
    public const bool Enabled = __XPSCRIPT_ANDROID_DEBUG__;
}

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

[Activity(
    Label = "XPScript",
    Theme = "@style/Theme.AppCompat.DayNight.NoActionBar",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
    internal static event Action<string, string>? FilePicked;
    private static string _pendingFileField = string.Empty;

    internal void OpenFileField(string fieldName)
    {
        _pendingFileField = fieldName ?? string.Empty;
        var intent = new global::Android.Content.Intent(global::Android.Content.Intent.ActionOpenDocument)
            .AddCategory(global::Android.Content.Intent.CategoryOpenable)
            .SetType("*/*");
        StartActivityForResult(intent, 7102);
    }

    protected override void OnActivityResult(int requestCode, global::Android.App.Result resultCode, global::Android.Content.Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != 7102 || resultCode != Result.Ok || data?.Data is null || _pendingFileField.Length == 0) return;
        FilePicked?.Invoke(_pendingFileField, data.Data.ToString() ?? string.Empty);
        _pendingFileField = string.Empty;
    }

    internal void RequestCameraPermission()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(23) && ContextCompat.CheckSelfPermission(this, global::Android.Manifest.Permission.Camera) != global::Android.Content.PM.Permission.Granted)
            RequestPermissions(new[] { global::Android.Manifest.Permission.Camera }, 7001);
    }
    protected override void OnDestroy() { if (ReferenceEquals(Current, this)) Current = null; base.OnDestroy(); }
    internal static MainActivity? Current { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Current = this;
        if (Intent?.GetBooleanExtra("xpscript.appdebug", false) == true)
            System.Environment.SetEnvironmentVariable("XPSCRIPT_APPDEBUG", "1");
        base.OnCreate(savedInstanceState);
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
}

[Application]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    protected AndroidApp(nint javaReference, global::Android.Runtime.JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }
}

public sealed class MainView : UserControl
{
    internal static MainView? Current { get; private set; }
    private static int _runtimeStarted;
    private readonly ContentControl _contentHost = new();
    private readonly Stack<Control?> _screenStack = new();
    private readonly Grid _root = new();
    private readonly Dictionary<string, StackPanel> _toastRegions = new(StringComparer.OrdinalIgnoreCase);
    private int _toastSequence;

    public MainView()
    {
        Current = this;
        _contentHost.Content = HomeContent();
        _root.Children.Add(_contentHost);
        AddToastRegion("top-left", HorizontalAlignment.Left, VerticalAlignment.Top);
        AddToastRegion("top-right", HorizontalAlignment.Right, VerticalAlignment.Top);
        AddToastRegion("bottom-left", HorizontalAlignment.Left, VerticalAlignment.Bottom);
        AddToastRegion("bottom-right", HorizontalAlignment.Right, VerticalAlignment.Bottom);
        Content = _root;

        if (Interlocked.Exchange(ref _runtimeStarted, 1) == 0)
        {
            Console.AndroidLog = (text, isError) => Log.WriteLine(
                isError ? LogPriority.Error : LogPriority.Info, "XPScript", text);
            _ = Task.Run(RunXPScript);
        }
    }

    private static void RunXPScript()
    {
        try
        {
            System.Environment.ExitCode = 0;
            Program.Main(Array.Empty<string>());
            if (System.Environment.ExitCode == 0)
                Log.Info("XPScript", "XPSCRIPT-EXIT=0");
            else
                Log.Error("XPScript", "XPSCRIPT-EXIT=" + System.Environment.ExitCode);
        }
        catch (Exception exception)
        {
            Log.Error("XPScript", exception.ToString());
            if (AndroidDebugMode.Enabled)
                Log.Error("XPScript", "XPSCRIPT-DEBUG-DUMP=" + exception);
            Log.Error("XPScript", "XPSCRIPT-EXIT=1");
        }
    }

    internal void ShowForm(Control form)
    {
        _screenStack.Push(_contentHost.Content as Control);
        _contentHost.Content = form;
    }

    internal void RestoreHome()
    {
        if (_screenStack.Count > 0)
        {
            _contentHost.Content = _screenStack.Pop();
            return;
        }
        _contentHost.Content = HomeContent();
    }

    internal void ShowToast(Control toast, string position, int durationSeconds)
    {
        if (!_toastRegions.TryGetValue(position, out var region)) region = _toastRegions["top-right"];
        // New items stay at the selected edge: top regions prepend, while
        // bottom anchored regions append so older items move upward.
        Panel.SetZIndex(toast, Interlocked.Increment(ref _toastSequence));
        region.Spacing = -10;
        if (position.StartsWith("bottom", StringComparison.OrdinalIgnoreCase)) region.Children.Add(toast);
        else region.Children.Insert(0, toast);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Clamp(durationSeconds, 1, 30)) };
        timer.Tick += (_, _) => { timer.Stop(); CloseToast(toast); };
        timer.Start();
    }

    internal static void CloseToast(Control toast)
    {
        if (toast.Parent is Panel panel) panel.Children.Remove(toast);
    }

    private void AddToastRegion(string name, HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        var region = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(16),
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            MaxWidth = 420,
            IsHitTestVisible = true
        };
        _toastRegions[name] = region;
        _root.Children.Add(region);
    }

    private static Control HomeContent() => new Grid();
}


internal sealed class GeneratedAndroidMedia3Player : IDisposable
{
    private readonly IExoPlayer _player;
    private readonly Media3Listener _listener;
    private bool _disposed;

    public event EventHandler<bool>? IsPlayingChanged;
    public event EventHandler<int>? PlaybackStateChanged;
    public event EventHandler<string>? PlaybackError;

    public GeneratedAndroidMedia3Player(global::Android.Content.Context context)
    {
        _player = new ExoPlayerBuilder(context).Build()
            ?? throw new InvalidOperationException("Media3 ExoPlayerBuilder returned no player.");
        _listener = new Media3Listener(this);
        _player.AddListener(_listener);
    }

    public void Attach(PlayerView view)
    {
        ThrowIfDisposed();
        view.Player = _player;
    }

    public void SetSource(string source)
    {
        ThrowIfDisposed();
        if (!System.Uri.TryCreate(source, System.UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https" or "file" or "content" or "android.resource"))
            throw new InvalidOperationException("UIForm Video source must be an absolute supported media URI.");
        _player.SetMediaItem(MediaItem.FromUri(global::Android.Net.Uri.Parse(uri.AbsoluteUri)));
        _player.Prepare();
    }

    public void Play() { ThrowIfDisposed(); _player.Play(); }
    public void Pause() { ThrowIfDisposed(); _player.Pause(); }
    public void Stop() { ThrowIfDisposed(); _player.Stop(); }
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
    public void SeekTo(long position) { ThrowIfDisposed(); if (position < 0) throw new ArgumentOutOfRangeException(nameof(position)); _player.SeekTo(position); }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _player.RemoveListener(_listener);
        _player.Release();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class Media3Listener : Java.Lang.Object, IPlayerListener
    {
        private readonly GeneratedAndroidMedia3Player _owner;

        public Media3Listener(GeneratedAndroidMedia3Player owner) => _owner = owner;

        public void OnIsPlayingChanged(bool isPlaying) => _owner.IsPlayingChanged?.Invoke(_owner, isPlaying);
        public void OnPlaybackStateChanged(int playbackState) => _owner.PlaybackStateChanged?.Invoke(_owner, playbackState);
        public void OnPlayerError(PlaybackException? error) => _owner.PlaybackError?.Invoke(_owner, error?.Message ?? "Media3 playback error.");
    }
}

internal sealed class GeneratedAndroidCameraPreviewControl : NativeControlHost
{
    private PreviewView? _previewView;
    private ProcessCameraProvider? _cameraProvider;
    private ImageCapture? _imageCapture;

    public Task<string> CapturePhotoAsync(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (_imageCapture is null) throw new InvalidOperationException("Camera preview is not initialized.");
        var root = Path.GetFullPath(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData));
        var fullPath = Path.GetFullPath(Path.IsPathRooted(outputPath) ? outputPath : Path.Combine(root, outputPath));
        if (outputPath.Contains("..", StringComparison.Ordinal) || !fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Camera photo output path must remain inside the application sandbox.", nameof(outputPath));
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var options = new ImageCapture.OutputFileOptions.Builder(new Java.IO.File(fullPath)).Build();
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = global::Android.App.Application.Context ?? throw new InvalidOperationException("Android application context is unavailable.");
        _imageCapture.TakePicture(options, ContextCompat.GetMainExecutor(context)!, new PhotoCaptureCallback(completion, fullPath));
        return completion.Task;
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var context = global::Android.App.Application.Context ?? throw new InvalidOperationException("Android application context is unavailable.");
        if (ContextCompat.CheckSelfPermission(context, global::Android.Manifest.Permission.Camera) != global::Android.Content.PM.Permission.Granted)
            throw new InvalidOperationException("Camera permission has not been granted.");
        if (MainActivity.Current is not ILifecycleOwner lifecycleOwner)
            throw new InvalidOperationException("Android camera preview requires a lifecycle owner.");
        _previewView = new PreviewView(context);
        var future = ProcessCameraProvider.GetInstance(context);
        future.AddListener(new Java.Lang.Runnable(() =>
        {
            _cameraProvider = future.Get() as ProcessCameraProvider ?? throw new InvalidOperationException("CameraX provider is unavailable.");
            var preview = new Preview.Builder().Build();
            _imageCapture = new ImageCapture.Builder().Build();
            preview.SetSurfaceProvider(ContextCompat.GetMainExecutor(context)!, _previewView.SurfaceProvider);
            _cameraProvider.UnbindAll();
            _cameraProvider.BindToLifecycle(lifecycleOwner, CameraSelector.DefaultBackCamera, preview, _imageCapture);
        }), ContextCompat.GetMainExecutor(context));
        return new PlatformHandle(_previewView.Handle, "Android.CameraX.PreviewView");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _cameraProvider?.UnbindAll();
        _cameraProvider?.Dispose();
        _cameraProvider = null;
        _imageCapture?.Dispose();
        _imageCapture = null;
        _previewView?.Dispose();
        _previewView = null;
        base.DestroyNativeControlCore(control);
    }

    private sealed class PhotoCaptureCallback : Java.Lang.Object, ImageCapture.IOnImageSavedCallback
    {
        private readonly TaskCompletionSource<string> _completion;
        private readonly string _outputPath;
        public PhotoCaptureCallback(TaskCompletionSource<string> completion, string outputPath) { _completion = completion; _outputPath = outputPath; }
        public void OnError(ImageCaptureException exception) => _completion.TrySetException(new InvalidOperationException(exception.Message ?? "Camera photo capture failed."));
        public void OnImageSaved(ImageCapture.OutputFileResults output) => _completion.TrySetResult(_outputPath);
    }
}

internal sealed class GeneratedAndroidVideoControl : NativeControlHost
{
    private GeneratedAndroidMedia3Player? _mediaPlayer;
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
    public bool IsPlaying => _mediaPlayer?.IsPlaying == true;
    public long Position => _mediaPlayer?.Position ?? 0;
    public long Duration => _mediaPlayer?.Duration ?? 0;
    public event EventHandler<bool>? IsPlayingChanged
    {
        add { if (_mediaPlayer is not null) _mediaPlayer.IsPlayingChanged += value; }
        remove { if (_mediaPlayer is not null) _mediaPlayer.IsPlayingChanged -= value; }
    }
    public event EventHandler<int>? PlaybackStateChanged
    {
        add { if (_mediaPlayer is not null) _mediaPlayer.PlaybackStateChanged += value; }
        remove { if (_mediaPlayer is not null) _mediaPlayer.PlaybackStateChanged -= value; }
    }
    public event EventHandler<string>? PlaybackError
    {
        add { if (_mediaPlayer is not null) _mediaPlayer.PlaybackError += value; }
        remove { if (_mediaPlayer is not null) _mediaPlayer.PlaybackError -= value; }
    }
    public void Play() => (_mediaPlayer ?? throw new InvalidOperationException("Video player is not initialized.")).Play();
    public void Pause() => (_mediaPlayer ?? throw new InvalidOperationException("Video player is not initialized.")).Pause();
    public void Stop() => (_mediaPlayer ?? throw new InvalidOperationException("Video player is not initialized.")).Stop();
    public void SeekTo(long position) => (_mediaPlayer ?? throw new InvalidOperationException("Video player is not initialized.")).SeekTo(position);

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var context = global::Android.App.Application.Context
            ?? throw new InvalidOperationException("Android application context is unavailable.");
        _mediaPlayer = new GeneratedAndroidMedia3Player(context);
        _playerView = new PlayerView(context);
        _mediaPlayer.Attach(_playerView);
        if (_source.Length > 0) _mediaPlayer.SetSource(_source);
        return new PlatformHandle(_playerView.Handle, "Android.Media3.PlayerView");
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

internal sealed class GeneratedAndroidAudioControl : Control, IDisposable
{
    private readonly GeneratedAndroidMedia3Player _mediaPlayer;
    private string _source = string.Empty;
    private bool _disposed;

    public GeneratedAndroidAudioControl()
    {
        var context = global::Android.App.Application.Context
            ?? throw new InvalidOperationException("Android application context is unavailable.");
        _mediaPlayer = new GeneratedAndroidMedia3Player(context);
        IsVisible = false;
        Width = 0;
        Height = 0;
    }

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
    public bool IsPlaying => _mediaPlayer.IsPlaying;
    public long Position => _mediaPlayer.Position;
    public long Duration => _mediaPlayer.Duration;
    public event EventHandler<bool> IsPlayingChanged { add => _mediaPlayer.IsPlayingChanged += value; remove => _mediaPlayer.IsPlayingChanged -= value; }
    public event EventHandler<int> PlaybackStateChanged { add => _mediaPlayer.PlaybackStateChanged += value; remove => _mediaPlayer.PlaybackStateChanged -= value; }
    public event EventHandler<string> PlaybackError { add => _mediaPlayer.PlaybackError += value; remove => _mediaPlayer.PlaybackError -= value; }
    public float Volume { get => _mediaPlayer.Volume; set => _mediaPlayer.Volume = value; }
    public void Play() => _mediaPlayer.Play();
    public void Pause() => _mediaPlayer.Pause();
    public void Stop() => _mediaPlayer.Stop();
    public void SeekTo(long position) => _mediaPlayer.SeekTo(position);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _mediaPlayer.Dispose();
        GC.SuppressFinalize(this);
    }
}

public static class AndroidFormHost
{
    private static readonly ConcurrentDictionary<string, Avalonia.Controls.NativeWebView> WebViews = new(StringComparer.Ordinal);

    public static void ShowToast(string requestJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowToast(requestJson));
            return;
        }
        using var document = JsonDocument.Parse(requestJson);
        var root = document.RootElement;
        var message = root.TryGetProperty("message", out var messageValue) ? messageValue.GetString() ?? string.Empty : string.Empty;
        var title = root.TryGetProperty("title", out var titleValue) ? titleValue.GetString() ?? string.Empty : string.Empty;
        var kind = root.TryGetProperty("kind", out var kindValue) ? kindValue.GetString() ?? "normal" : "normal";
        var position = NormalizeToastPosition(root.TryGetProperty("position", out var positionValue) ? positionValue.GetString() : null);
        var duration = root.TryGetProperty("durationSeconds", out var durationValue) && durationValue.TryGetInt32(out var seconds) ? seconds : 5;
        var background = root.TryGetProperty("background", out var backgroundValue) ? backgroundValue.GetString() ?? string.Empty : string.Empty;
        if (MainView.Current is null) throw new InvalidOperationException("XPScript Android UIForm host is not initialized.");
        var warning = kind.Equals("warning", StringComparison.OrdinalIgnoreCase);
        var close = new Avalonia.Controls.Button
        {
            Content = "×", Width = 36, Height = 36, Padding = new Thickness(0),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 24,
            VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Right
        };
        AutomationProperties.SetName(close, "Close notification");
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(title) ? warning ? "Warning" : "Message" : title,
            FontWeight = FontWeight.Bold, Foreground = warning ? Brushes.Red : Brushes.Black,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(close, 1);
        header.Children.Add(close);
        var body = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = warning ? Brushes.Red : Brushes.Black };
        var toast = new Border
        {
            Background = ParseToastBrush(background), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(12),
            BoxShadow = new BoxShadows(new BoxShadow { Blur = 14, Color = Color.FromArgb(80, 0, 0, 0), OffsetY = 3 }),
            Child = new StackPanel { Spacing = 6, Children = { header, body } }
        };
        close.Click += (_, _) => MainView.CloseToast(toast);
        MainView.Current.ShowToast(toast, position, duration);
    }

    private static string NormalizeToastPosition(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "top-left" or "topleft" => "top-left",
            "bottom-left" or "bottomleft" => "bottom-left",
            "bottom-right" or "bottomright" => "bottom-right",
            _ => "top-right"
        };

    private static IBrush? ParseToastBrush(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return Brush.Parse(value); }
        catch (FormatException) { return null; }
    }

    public static string ShowDialog(string requestJson) => ShowDialog(requestJson, null);

    public static string ShowDialog(string requestJson, Func<string, string, string>? eventCallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);
        if (MainView.Current is null)
            throw new InvalidOperationException("XPScript Android UIForm host is not initialized.");

        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Show()
        {
            using var document = JsonDocument.Parse(requestJson);
            var request = document.RootElement;
            var instanceId = request.TryGetProperty("instanceId", out var instanceIdValue) ? instanceIdValue.GetString() ?? string.Empty : string.Empty;
            var fields = request.TryGetProperty("fields", out var fieldArray) && fieldArray.ValueKind == JsonValueKind.Array
                ? fieldArray.EnumerateArray()
                    .Where(x => !x.GetProperty("type").GetString()!.Equals("HiddenField", StringComparison.OrdinalIgnoreCase))
                    .ToArray()
                : Array.Empty<JsonElement>();
            var hiddenValues = request.TryGetProperty("fields", out var allFieldArray) && allFieldArray.ValueKind == JsonValueKind.Array
                ? allFieldArray.EnumerateArray()
                    .Where(x => x.GetProperty("type").GetString()!.Equals("HiddenField", StringComparison.OrdinalIgnoreCase))
                    .Where(x => x.TryGetProperty("value", out _))
                    .ToDictionary(x => x.GetProperty("name").GetString() ?? string.Empty,
                        x => x.GetProperty("value").Clone(),
                        StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            var editors = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
            var fieldsWithoutChangeHandlers = fields
                .Where(field => field.TryGetProperty("hasChangeHandler", out var handler) && handler.ValueKind == JsonValueKind.False)
                .Select(field => field.GetProperty("name").GetString() ?? string.Empty)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var mediaControls = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
            var progressControls = new Dictionary<string, Avalonia.Controls.ProgressBar>(StringComparer.OrdinalIgnoreCase);
            var validationErrors = new Dictionary<string, TextBlock>(StringComparer.OrdinalIgnoreCase);
            var fieldContainers = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
            var fieldLabels = new Dictionary<string, TextBlock>(StringComparer.OrdinalIgnoreCase);
            var actionButtons = new Dictionary<string, Avalonia.Controls.Button>(StringComparer.OrdinalIgnoreCase);
            var panel = new StackPanel { Spacing = 12, Margin = new Thickness(16), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Stretch };
            if (request.TryGetProperty("background", out var formBackground) && formBackground.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(formBackground.GetString()))
            {
                try { panel.Background = Brush.Parse(formBackground.GetString()!); }
                catch (FormatException) { }
            }
            TabControl? tabControl = null;
            var dispatchingFieldChange = false;

            void DispatchMediaEvent(string fieldName, string eventKind, string submittedValue = "")
            {
                if (eventCallback is null) return;
                try
                {
                    var actionState = eventCallback(eventKind + ":" + fieldName, submittedValue);
                    ApplyActionState(actionState, editors, mediaControls, progressControls, validationErrors, fieldContainers, fieldLabels, actionButtons, tabControl);
                }
                catch (Exception exception)
                {
                    Log.Error("XPScript", "UIForm media event '" + eventKind + "' callback failed: " + exception);
                }
            }

            void AttachMediaEvents(string fieldName, Control control)
            {
                switch (control)
                {
                    case GeneratedAndroidVideoControl video:
                        video.IsPlayingChanged += (_, playing) => DispatchMediaEvent(fieldName, playing ? "play" : "pause", JsonSerializer.Serialize(new { position = video.Position, duration = video.Duration, isPlaying = playing }));
                        video.PlaybackStateChanged += (_, state) => { if (state == 4) DispatchMediaEvent(fieldName, "ended"); };
                        video.PlaybackError += (_, error) => DispatchMediaEvent(fieldName, "error", error);
                        break;
                    case GeneratedAndroidAudioControl audio:
                        audio.IsPlayingChanged += (_, playing) => DispatchMediaEvent(fieldName, playing ? "play" : "pause", JsonSerializer.Serialize(new { position = audio.Position, duration = audio.Duration, isPlaying = playing }));
                        audio.PlaybackStateChanged += (_, state) => { if (state == 4) DispatchMediaEvent(fieldName, "ended"); };
                        audio.PlaybackError += (_, error) => DispatchMediaEvent(fieldName, "error", error);
                        break;
                }
            }

            void DispatchFieldChange(string fieldName, string value)
            {
                if (eventCallback is null || dispatchingFieldChange || fieldsWithoutChangeHandlers.Contains(fieldName)) return;
                try
                {
                    dispatchingFieldChange = true;
                    var actionState = eventCallback("change:" + fieldName, value);
                    ApplyActionState(actionState, editors, mediaControls, progressControls, validationErrors, fieldContainers, fieldLabels, actionButtons, tabControl);
                }
                catch (Exception exception)
                {
                    Log.Error("XPScript", "UIForm field change callback failed: " + exception);
                }
                finally
                {
                    dispatchingFieldChange = false;
                }
            }

            void DisposeMediaControls()
            {
                foreach (var control in mediaControls.Values)
                    if (control is IDisposable disposable)
                        disposable.Dispose();
            }

            var bootText = request.TryGetProperty("bootText", out var bootTextValue) ? bootTextValue.GetString() ?? string.Empty : string.Empty;
            var bootImage = request.TryGetProperty("bootImage", out var bootImageValue) ? bootImageValue.GetString() ?? string.Empty : string.Empty;
            if (bootImage.Length > 0)
            {
                try
                {
                    var image = new Avalonia.Controls.Image { MaxHeight = 240, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center };
                    var bytes = ReadAndroidImageBytes(bootImage, "Strict");
                    using var stream = new MemoryStream(bytes, writable: false);
                    image.Source = new Bitmap(stream);
                    panel.Children.Add(image);
                }
                catch (Exception exception)
                {
                    Log.Error("XPScript", "UIForm boot image failed: " + exception.Message);
                }
            }
            if (bootText.Length > 0)
                panel.Children.Add(new TextBlock { Text = bootText, FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center });

            if (request.TryGetProperty("title", out var title))
            {
                var formTitle = title.GetString() ?? "XPScript";
                panel.Children.Add(new TextBlock { Text = formTitle, FontSize = 24 });
                if (MainActivity.Current is not null)
                    MainActivity.Current.Title = formTitle;
            }

            var tabPanels = new Dictionary<string, StackPanel>(StringComparer.OrdinalIgnoreCase);
            if (request.TryGetProperty("tabs", out var tabs) && tabs.ValueKind == JsonValueKind.Array && tabs.GetArrayLength() > 0)
            {
                var tabItems = new List<TabItem>();
                foreach (var tab in tabs.EnumerateArray())
                {
                    var tabName = tab.GetProperty("name").GetString() ?? string.Empty;
                    var tabLabel = tab.TryGetProperty("label", out var tabLabelValue) ? tabLabelValue.GetString() ?? tabName : tabName;
                    var tabPanel = new StackPanel { Spacing = 12 };
                    tabPanels[tabName] = tabPanel;
                    tabItems.Add(new TabItem
                    {
                        Header = new Border
                        {
                            Padding = new Thickness(18, 10),
                            Margin = new Thickness(2, 0),
                            Background = tab.TryGetProperty("background", out var backgroundValue) && !string.IsNullOrWhiteSpace(backgroundValue.GetString()) ? Avalonia.Media.Brush.Parse(backgroundValue.GetString()!) : null,
                            Child = new TextBlock
                            {
                                Text = tabLabel,
                                FontSize = tab.TryGetProperty("fontSize", out var fontSizeValue) && fontSizeValue.ValueKind == JsonValueKind.Number ? fontSizeValue.GetDouble() : 16,
                                FontFamily = tab.TryGetProperty("font", out var fontValue) && !string.IsNullOrWhiteSpace(fontValue.GetString()) ? new Avalonia.Media.FontFamily(fontValue.GetString()!) : null,
                                FontWeight = tab.TryGetProperty("bold", out var boldValue) && boldValue.ValueKind == JsonValueKind.True ? FontWeight.Bold : FontWeight.SemiBold
                            }
                        },
                        Tag = tabName,
                        Content = new ScrollViewer { Content = tabPanel }
                    });
                }
                tabControl = new TabControl
                {
                    ItemsSource = tabItems,
                    TabStripPlacement = string.Equals(request.TryGetProperty("tabPlacement", out var tabPlacementValue) ? tabPlacementValue.GetString() : null, "Bottom", StringComparison.OrdinalIgnoreCase) ? Dock.Bottom : Dock.Top,
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch
                };
                void ApplyTabStyles()
                {
                    var activeName = tabControl.SelectedItem is TabItem selectedItem ? selectedItem.Tag?.ToString() ?? string.Empty : string.Empty;
                    for (var index = 0; index < tabItems.Count; index++)
                    {
                        var tab = tabs[index];
                        var selected = string.Equals(tab.GetProperty("name").GetString(), activeName, StringComparison.OrdinalIgnoreCase);
                        var header = (Border)tabItems[index].Header!;
                        var text = (TextBlock)header.Child!;
                        var background = selected && tab.TryGetProperty("selectedBackground", out var sb) ? sb.GetString() : tab.TryGetProperty("background", out var bg) ? bg.GetString() : null;
                        var color = selected && tab.TryGetProperty("selectedFontColor", out var sc) ? sc.GetString() : tab.TryGetProperty("fontColor", out var fc) ? fc.GetString() : null;
                        var font = selected && tab.TryGetProperty("selectedFont", out var sf) ? sf.GetString() : tab.TryGetProperty("font", out var f) ? f.GetString() : null;
                        var size = selected && tab.TryGetProperty("selectedFontSize", out var ss) && ss.ValueKind == JsonValueKind.Number ? ss.GetDouble() : tab.TryGetProperty("fontSize", out var fs) && fs.ValueKind == JsonValueKind.Number ? fs.GetDouble() : 16;
                        var bold = selected && tab.TryGetProperty("selectedBold", out var sbold) && sbold.ValueKind == JsonValueKind.True || !selected && tab.TryGetProperty("bold", out var boldValue) && boldValue.ValueKind == JsonValueKind.True;
                        header.Background = string.IsNullOrWhiteSpace(background) ? null : Avalonia.Media.Brush.Parse(background!);
                        text.Foreground = string.IsNullOrWhiteSpace(color) ? Avalonia.Media.Brushes.Black : Avalonia.Media.Brush.Parse(color!);
                        text.FontFamily = string.IsNullOrWhiteSpace(font) ? null : new Avalonia.Media.FontFamily(font!);
                        text.FontSize = size;
                        text.FontWeight = bold ? FontWeight.Bold : FontWeight.SemiBold;
                    }
                }
                tabControl.SelectionChanged += (_, _) => ApplyTabStyles();
                var activeTab = request.TryGetProperty("activeTab", out var activeTabValue) ? activeTabValue.GetString() ?? string.Empty : string.Empty;
                var selected = tabItems.FindIndex(item => string.Equals(item.Tag?.ToString(), activeTab, StringComparison.OrdinalIgnoreCase));
                tabControl.SelectedIndex = selected >= 0 ? selected : 0;
                ApplyTabStyles();
                panel.Children.Add(tabControl);
            }

            var namedGrids = new Dictionary<string, Grid>(StringComparer.OrdinalIgnoreCase);
            var componentPanels = new Dictionary<string, StackPanel>(StringComparer.OrdinalIgnoreCase);
            var fileFieldButtons = new Dictionary<string, Avalonia.Controls.Button>(StringComparer.OrdinalIgnoreCase);
            if (request.TryGetProperty("grids", out var grids) && grids.ValueKind == JsonValueKind.Array)
            {
                foreach (var gridDefinition in grids.EnumerateArray())
                {
                    var gridName = gridDefinition.GetProperty("name").GetString() ?? string.Empty;
                    var columns = gridDefinition.TryGetProperty("columns", out var columnValue) && columnValue.TryGetInt32(out var count) ? Math.Max(1, count) : 1;
                    var grid = new Grid();
                    for (var column = 0; column < columns; column++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                    namedGrids[gridName] = grid;
                    var gridTabName = gridDefinition.TryGetProperty("tabName", out var gridTabValue) ? gridTabValue.GetString() ?? string.Empty : string.Empty;
                    if (gridTabName.Length > 0 && tabPanels.TryGetValue(gridTabName, out var gridTabPanel)) gridTabPanel.Children.Add(grid);
                    else panel.Children.Add(grid);
                }
            }

            foreach (var field in fields)
            {
                var name = field.GetProperty("name").GetString() ?? string.Empty;
                var componentId = field.TryGetProperty("id", out var componentIdValue) ? componentIdValue.GetString() ?? string.Empty : string.Empty;
                var label = field.TryGetProperty("label", out var labelValue) ? labelValue.GetString() ?? name : name;
                var type = field.GetProperty("type").GetString() ?? "TextField";

                var tabName = field.TryGetProperty("tabName", out var tabNameValue) ? tabNameValue.GetString() ?? string.Empty : string.Empty;
                var gridName = field.TryGetProperty("gridName", out var gridNameValue) ? gridNameValue.GetString() ?? string.Empty : string.Empty;
                var targetPanel = tabName.Length > 0 && tabPanels.TryGetValue(tabName, out var fieldTabPanel) ? fieldTabPanel : panel;
                var targetGrid = gridName.Length > 0 && namedGrids.TryGetValue(gridName, out var fieldGrid) ? fieldGrid : null;
                var parentId = field.TryGetProperty("parentId", out var parentIdValue) ? parentIdValue.GetString() ?? string.Empty : string.Empty;
                if (parentId.Length > 0 && componentPanels.TryGetValue(parentId, out var parentPanel))
                {
                    targetPanel = parentPanel;
                    targetGrid = null;
                }
                var fieldContainer = new StackPanel { Spacing = 4, Margin = new Thickness(4) };
                fieldContainer.IsVisible = !field.TryGetProperty("visible", out var fieldVisible) || fieldVisible.ValueKind != JsonValueKind.False;
                fieldContainers[name] = fieldContainer;
                if (label.Length > 0 && type is not ("Separator" or "Spacer" or "Image"))
                {
                    var labelBlock = new TextBlock { Text = label };
                    fieldLabels[name] = labelBlock;
                    fieldContainer.Children.Add(labelBlock);
                }

                var fieldCornerRadius = field.TryGetProperty("cornerRadius", out var fieldCornerRadiusValue) && fieldCornerRadiusValue.TryGetDouble(out var fieldRadius) ? fieldRadius : 0;
                var options = field.TryGetProperty("options", out var optionValues) && optionValues.ValueKind == JsonValueKind.Array
                    ? optionValues.EnumerateArray().Select(option => option.GetString() ?? string.Empty).ToArray()
                    : Array.Empty<string>();
                Control editor = type switch
                {
                    "CheckBox" => new Avalonia.Controls.CheckBox(),
                    "Switch" => new Avalonia.Controls.ToggleSwitch(),
                    "Video" => CreateVideo(field),
                    "Audio" => CreateAudio(field),
                    "ProgressBar" => CreateProgressBar(field),
                    "ActivityIndicator" => CreateActivityIndicator(field),
                    "WebView" => CreateWebView(field, instanceId, name),
                    "CameraPreview" => CreateCameraPreview(),
                    "FileField" => new Avalonia.Controls.Button { Content = label.Length == 0 ? "Choose file" : label, HorizontalAlignment = HorizontalAlignment.Left },
                    "Carousel" => CreateCarousel(field),
                    "DateField" => new Avalonia.Controls.DatePicker(),
                    "TimeField" => new Avalonia.Controls.TimePicker(),
                    "DateTimeField" => new AndroidDateTimeFieldEditor(),
                    "MonthField" => new AndroidMonthFieldEditor(),
                    "WeekField" => new AndroidWeekFieldEditor(),
                    "ColorField" => new AndroidColorFieldEditor(),
                    "TextArea" => new TextBox { AcceptsReturn = true, MinHeight = 120, CornerRadius = new CornerRadius(fieldCornerRadius) },
                    "RichTextField" => new TextBox { AcceptsReturn = true, MinHeight = 120, CornerRadius = new CornerRadius(fieldCornerRadius) },
                    "PasswordField" => new TextBox { PasswordChar = '•', CornerRadius = new CornerRadius(fieldCornerRadius) },
                    "NumberField" or "DecimalField" or "CurrencyField" => CreateNumberField(field),
                    "EmailField" or "UrlField" or "TelField" => new TextBox { CornerRadius = new CornerRadius(fieldCornerRadius) },
                    "Select" => new ComboBox { ItemsSource = options },
                    "ListBox" or "ListView" => new ListBox { ItemsSource = options, MinHeight = 112, SelectionMode = SelectionMode.Single },
                    "MultiListBox" or "MultiSelect" or "CheckBoxGroup" => new ListBox { ItemsSource = options, MinHeight = 112, SelectionMode = SelectionMode.Multiple | SelectionMode.Toggle },
                    "LookupField" or "AutoCompleteField" => new ComboBox { ItemsSource = options, IsEditable = true },
                    "RadioGroup" => CreateRadioGroup(options),
                    "RangeField" => CreateRangeField(field),
                    "Separator" => new Separator(),
                    "Spacer" => new Border { Height = 16 },
                    "Image" => CreateImage(field),
                    "Icon" => CreateIcon(field),
                    "Card" or "Panel" => CreateCard(field),
                    "ScrollView" => CreateScrollView(field),
                    _ => new TextBox { CornerRadius = new CornerRadius(fieldCornerRadius) }
                };

                AutomationProperties.SetAutomationId(editor, "xps-field-" + (componentId.Length > 0 ? componentId : name));
                if (field.TryGetProperty("width", out var widthValue) && widthValue.ValueKind == JsonValueKind.Number && widthValue.TryGetDouble(out var width)) editor.Width = width;
                if (field.TryGetProperty("height", out var heightValue) && heightValue.ValueKind == JsonValueKind.Number && heightValue.TryGetDouble(out var height)) editor.Height = height;
                if (editor is TemplatedControl styledEditor)
                {
                    if (field.TryGetProperty("fontSize", out var fontSizeValue) && fontSizeValue.ValueKind == JsonValueKind.Number && fontSizeValue.TryGetDouble(out var fontSize)) styledEditor.FontSize = fontSize;
                    if (field.TryGetProperty("font", out var fontValue) && fontValue.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(fontValue.GetString())) styledEditor.FontFamily = new FontFamily(fontValue.GetString()!);
                }
                // Keep the automation id as the resource id; exposing the id as the name makes Android report it as field text.
                if (field.TryGetProperty("value", out var value) && value.ValueKind != JsonValueKind.Null)
                    SetEditorValue(editor, value);

                editor.IsEnabled = !field.TryGetProperty("enabled", out var enabled) || enabled.ValueKind != JsonValueKind.False;
                ApplyEditorReadOnly(editor, field.TryGetProperty("readOnly", out var readOnly) && readOnly.ValueKind == JsonValueKind.True);
                fieldContainer.Children.Add(editor);
                if (componentId.Length > 0)
                {
                    if (editor is Border { Child: StackPanel cardPanel }) componentPanels[componentId] = cardPanel;
                    else if (editor is ScrollViewer { Content: StackPanel scrollPanel }) componentPanels[componentId] = scrollPanel;
                }

                if (editor is Avalonia.Controls.ProgressBar progressControl) progressControls[name] = progressControl;
                if (type == "FileField" && editor is Avalonia.Controls.Button fileButton)
                {
                    fileFieldButtons[name] = fileButton;
                    fileButton.Click += (_, _) => MainActivity.Current?.OpenFileField(name);
                }
                if (type is "Separator" or "Spacer" or "Image" or "Icon" or "Card" or "Panel" or "ScrollView" or "WebView" or "CameraPreview" or "FileField" or "Carousel" or "ProgressBar" or "ActivityIndicator")
                {
                    AddFieldContainer(field, fieldContainer, targetPanel, targetGrid);
                    continue;
                }

                editors[name] = editor;
                if (type is "Video" or "Audio")
                {
                    mediaControls[name] = editor;
                    AttachMediaEvents(name, editor);
                }
                if (editor is Slider slider)
                    slider.ValueChanged += (_, args) => DispatchFieldChange(name, args.NewValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (editor is Avalonia.Controls.CheckBox checkBox)
                    checkBox.IsCheckedChanged += (_, _) => DispatchFieldChange(name, checkBox.IsChecked == true ? "true" : "false");
                if (editor is Avalonia.Controls.ToggleSwitch toggleSwitch)
                    toggleSwitch.IsCheckedChanged += (_, _) => DispatchFieldChange(name, toggleSwitch.IsChecked == true ? "true" : "false");
                if (editor is Avalonia.Controls.DatePicker datePicker)
                    datePicker.SelectedDateChanged += (_, _) => DispatchFieldChange(name, GetTemporalEditorValue(datePicker) ?? string.Empty);
                if (editor is Avalonia.Controls.TimePicker timePicker)
                    timePicker.SelectedTimeChanged += (_, _) => DispatchFieldChange(name, GetTemporalEditorValue(timePicker) ?? string.Empty);
                if (editor is AndroidDateTimeFieldEditor dateTimeEditor)
                {
                    dateTimeEditor.DateEditor.SelectedDateChanged += (_, _) => DispatchFieldChange(name, GetTemporalEditorValue(dateTimeEditor) ?? string.Empty);
                    dateTimeEditor.TimeEditor.SelectedTimeChanged += (_, _) => DispatchFieldChange(name, GetTemporalEditorValue(dateTimeEditor) ?? string.Empty);
                }
                if (editor is AndroidMonthFieldEditor monthEditor)
                {
                    monthEditor.YearEditor.ValueChanged += (_, _) => DispatchFieldChange(name, GetTemporalEditorValue(monthEditor) ?? string.Empty);
                    monthEditor.MonthEditor.SelectionChanged += (_, _) => DispatchFieldChange(name, GetTemporalEditorValue(monthEditor) ?? string.Empty);
                }
                if (editor is AndroidWeekFieldEditor weekEditor)
                {
                    weekEditor.YearEditor.ValueChanged += (_, _) => DispatchFieldChange(name, GetTemporalEditorValue(weekEditor) ?? string.Empty);
                    weekEditor.WeekEditor.ValueChanged += (_, _) => DispatchFieldChange(name, GetTemporalEditorValue(weekEditor) ?? string.Empty);
                }
                if (editor is NumericUpDown numericEditor)
                    numericEditor.ValueChanged += (_, _) => DispatchFieldChange(name, numericEditor.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
                if (editor is AndroidColorFieldEditor colorField)
                    colorField.ValueEditor.TextChanged += (_, _) => DispatchFieldChange(name, colorField.ValueEditor.Text ?? string.Empty);
                if (editor is TextBox textEditor)
                    textEditor.TextChanged += (_, _) => DispatchFieldChange(name, textEditor.Text ?? string.Empty);
                if (editor is ComboBox comboBox)
                {
                    comboBox.SelectionChanged += (_, _) => DispatchFieldChange(name, comboBox.IsEditable ? comboBox.Text ?? string.Empty : comboBox.SelectedItem?.ToString() ?? string.Empty);
                    if (type is "LookupField" or "AutoCompleteField")
                        comboBox.PropertyChanged += (_, args) => { if (args.Property == ComboBox.TextProperty) DispatchFieldChange(name, comboBox.Text ?? string.Empty); };
                }
                if (editor is ListBox listBox)
                    listBox.SelectionChanged += (_, _) => DispatchFieldChange(name, listBox.SelectionMode.HasFlag(SelectionMode.Multiple)
                        ? string.Join('\u001f', listBox.SelectedItems?.Cast<object>().Select(item => item?.ToString() ?? string.Empty) ?? Array.Empty<string>())
                        : listBox.SelectedItem?.ToString() ?? string.Empty);
                if (editor is StackPanel radioPanel)
                    foreach (var radio in radioPanel.Children.OfType<Avalonia.Controls.RadioButton>())
                        radio.Click += (_, _) => DispatchFieldChange(name, radio.Tag?.ToString() ?? string.Empty);
                if (type == "FileField")
                    editors[name] = editor;
                var validationBlock = new TextBlock
                {
                    Text = string.Empty,
                    IsVisible = false,
                    Foreground = Brushes.Red
                };
                validationErrors[name] = validationBlock;
                fieldContainer.Children.Add(validationBlock);
                AddFieldContainer(field, fieldContainer, targetPanel, targetGrid);
            }

            MainActivity.FilePicked += (name, uri) =>
            {
                if (!fileFieldButtons.TryGetValue(name, out var button)) return;
                button.Tag = uri;
                button.Content = uri.Length == 0 ? "Choose file" : uri;
                DispatchFieldChange(name, uri);
            };

            var actions = new WrapPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            if (request.TryGetProperty("buttons", out var buttonArray) && buttonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var buttonValue in buttonArray.EnumerateArray())
                {
                    if (buttonValue.TryGetProperty("visible", out var visible) && visible.ValueKind == JsonValueKind.False)
                        continue;
                    var buttonName = buttonValue.GetProperty("name").GetString() ?? string.Empty;
                    var buttonLabel = buttonValue.TryGetProperty("label", out var buttonLabelValue) ? buttonLabelValue.GetString() ?? buttonName : buttonName;
                    var buttonIcon = buttonValue.TryGetProperty("icon", out var buttonIconValue) ? buttonIconValue.GetString() ?? string.Empty : string.Empty;
                    var buttonImage = buttonValue.TryGetProperty("image", out var buttonImageValue) ? buttonImageValue.GetString() ?? string.Empty : string.Empty;
                    var cornerRadius = buttonValue.TryGetProperty("cornerRadius", out var cornerRadiusValue) && cornerRadiusValue.TryGetDouble(out var radius) ? radius : 0;
                    var actionButton = new Avalonia.Controls.Button { Content = CreateButtonContent(buttonIcon, buttonImage, buttonLabel), Tag = new[] { buttonIcon, buttonImage }, MinWidth = 100, CornerRadius = new CornerRadius(cornerRadius) };
                    if (buttonValue.TryGetProperty("width", out var buttonWidthValue) && buttonWidthValue.ValueKind == JsonValueKind.Number && buttonWidthValue.TryGetDouble(out var buttonWidth)) actionButton.Width = buttonWidth;
                    if (buttonValue.TryGetProperty("height", out var buttonHeightValue) && buttonHeightValue.ValueKind == JsonValueKind.Number && buttonHeightValue.TryGetDouble(out var buttonHeight)) actionButton.Height = buttonHeight;
                    if (buttonValue.TryGetProperty("fontSize", out var buttonFontSizeValue) && buttonFontSizeValue.ValueKind == JsonValueKind.Number && buttonFontSizeValue.TryGetDouble(out var buttonFontSize)) actionButton.FontSize = buttonFontSize;
                    if (buttonValue.TryGetProperty("font", out var buttonFontValue) && buttonFontValue.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(buttonFontValue.GetString())) actionButton.FontFamily = new FontFamily(buttonFontValue.GetString()!);
                    actionButton.IsEnabled = !buttonValue.TryGetProperty("enabled", out var buttonEnabled) || buttonEnabled.ValueKind != JsonValueKind.False;
                    actionButtons[buttonName] = actionButton;
                    actionButton.Click += (_, _) =>
                    {
                        if (eventCallback is null) return;
                        try
                        {
                            var submitted = editors.ToDictionary(pair => pair.Key, pair => GetEditorValue(pair.Value), StringComparer.OrdinalIgnoreCase);
                            foreach (var hidden in hiddenValues) submitted[hidden.Key] = hidden.Value;
                            var submittedValues = JsonSerializer.Serialize(submitted);
                            var actionState = eventCallback("button:" + buttonName, submittedValues);
                            ApplyActionState(actionState, editors, mediaControls, progressControls, validationErrors, fieldContainers, fieldLabels, actionButtons, tabControl);
                        }
                        catch (Exception exception)
                        {
                            Log.Error("XPScript", "UIForm button '" + buttonName + "' callback failed: " + exception);
                            if (AndroidDebugMode.Enabled)
                                Log.Error("XPScript", "XPSCRIPT-DEBUG-DUMP=" + exception);
                        }
                    };
                    actions.Children.Add(actionButton);
                }
            }

            var cancel = new Avalonia.Controls.Button { Content = "Cancel", MinWidth = 100 };
            var ok = new Avalonia.Controls.Button { Content = "OK", MinWidth = 100 };
            if (request.TryGetProperty("defaultButtonCornerRadius", out var defaultButtonCornerRadiusValue) &&
                defaultButtonCornerRadiusValue.ValueKind == JsonValueKind.Number &&
                defaultButtonCornerRadiusValue.TryGetDouble(out var defaultButtonRadius))
            {
                cancel.CornerRadius = new CornerRadius(defaultButtonRadius);
                ok.CornerRadius = new CornerRadius(defaultButtonRadius);
            }
            actions.Children.Add(cancel);
            actions.Children.Add(ok);
            panel.Children.Add(actions);
            MainView.Current!.ShowForm(new ScrollViewer { Content = panel });

            var initialFocus = request.TryGetProperty("initialFocus", out var initialFocusValue) ? initialFocusValue.GetString() ?? string.Empty : string.Empty;
            if (initialFocus.Length > 0 && editors.TryGetValue(initialFocus, out var initialEditor) && initialEditor.IsEnabled && initialEditor.Focusable)
                initialEditor.Focus();
            else
                editors.Values.FirstOrDefault(editor => editor.IsVisible && editor.IsEnabled && editor.Focusable && editor.IsTabStop)?.Focus();

            cancel.Click += (_, _) =>
            {
                DisposeMediaControls();
                MainView.Current?.RestoreHome();
                completion.TrySetResult(JsonSerializer.Serialize(new { result = "Cancel", values = new { } }));
            };

            ok.Click += (_, _) =>
            {
                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in editors)
                    values[pair.Key] = GetEditorValue(pair.Value);
                foreach (var hidden in hiddenValues)
                    values[hidden.Key] = hidden.Value;

                var submittedValues = JsonSerializer.Serialize(values);
                if (eventCallback is not null)
                {
                    try
                    {
                        var state = eventCallback("submit:OK", submittedValues);
                        using var action = JsonDocument.Parse(state);
                        ApplyActionState(state, editors, mediaControls, progressControls, validationErrors, fieldContainers, fieldLabels, actionButtons, tabControl);
                        if (!action.RootElement.TryGetProperty("accepted", out var accepted) || accepted.ValueKind != JsonValueKind.True) return;
                    }
                    catch (Exception exception)
                    {
                        DisposeMediaControls();
                        MainView.Current?.RestoreHome();
                        completion.TrySetException(exception);
                        return;
                    }
                }

                DisposeMediaControls();
                MainView.Current?.RestoreHome();
                completion.TrySetResult(JsonSerializer.Serialize(new { result = "OK", values }));
            };
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            // Entry-point code may run on Android's UI thread. Keep a nested
            // dispatcher frame so Avalonia can render and process buttons while
            // the XPScript call waits for OK/Cancel.
            Show();
            var frame = new DispatcherFrame();
            completion.Task.ContinueWith(_ => Dispatcher.UIThread.Post(() => frame.Continue = false), TaskScheduler.Default);
            Dispatcher.UIThread.PushFrame(frame);
            return completion.Task.GetAwaiter().GetResult();
        }
        Dispatcher.UIThread.Post(() =>
        {
            try { Show(); }
            catch (Exception exception) { completion.TrySetException(exception); }
        });

        return completion.Task.GetAwaiter().GetResult();
    }

    private static bool ApplyActionState(
        string actionStateJson,
        Dictionary<string, Control> editors,
        Dictionary<string, Control> mediaControls,
        Dictionary<string, Avalonia.Controls.ProgressBar> progressControls,
        Dictionary<string, TextBlock> validationErrors,
        Dictionary<string, Control> fieldContainers,
        Dictionary<string, TextBlock> fieldLabels,
        Dictionary<string, Avalonia.Controls.Button> actionButtons,
        TabControl? tabControl)
    {
        if (string.IsNullOrWhiteSpace(actionStateJson)) return false;
        var hasValidationErrors = false;
        using var document = JsonDocument.Parse(actionStateJson);
        var root = document.RootElement;
        if (root.TryGetProperty("mediaCommands", out var mediaCommands) && mediaCommands.ValueKind == JsonValueKind.Array)
        {
            foreach (var command in mediaCommands.EnumerateArray())
            {
                var name = command.TryGetProperty("name", out var nameValue) ? nameValue.GetString() ?? string.Empty : string.Empty;
                var operation = command.TryGetProperty("command", out var operationValue) ? operationValue.GetString() ?? string.Empty : string.Empty;
                if (!mediaControls.TryGetValue(name, out var mediaControl)) continue;
                switch (operation.ToLowerInvariant())
                {
                    case "play" when mediaControl is GeneratedAndroidVideoControl video: video.Play(); break;
                    case "pause" when mediaControl is GeneratedAndroidVideoControl video: video.Pause(); break;
                    case "stop" when mediaControl is GeneratedAndroidVideoControl video: video.Stop(); break;
                    case "play" when mediaControl is GeneratedAndroidAudioControl audio: audio.Play(); break;
                    case "pause" when mediaControl is GeneratedAndroidAudioControl audio: audio.Pause(); break;
                    case "stop" when mediaControl is GeneratedAndroidAudioControl audio: audio.Stop(); break;
                }
            }
        }
        if (tabControl is not null && root.TryGetProperty("activeTab", out var activeTabElement))
        {
            var activeTab = activeTabElement.GetString() ?? string.Empty;
            var items = tabControl.ItemsSource?.Cast<TabItem>().ToList() ?? new List<TabItem>();
            var selected = items.FindIndex(item => string.Equals(item.Tag?.ToString(), activeTab, StringComparison.OrdinalIgnoreCase));
            if (selected >= 0) tabControl.SelectedIndex = selected;
        }
        if (root.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Array)
        {
            foreach (var field in fields.EnumerateArray())
            {
                var name = field.TryGetProperty("name", out var nameValue) ? nameValue.GetString() ?? string.Empty : string.Empty;
                if (name.Length == 0) continue;
                if (progressControls.TryGetValue(name, out var progressControl))
                {
                    if (field.TryGetProperty("progressValue", out var progressValue) && progressValue.TryGetDouble(out var progress))
                        progressControl.Value = Math.Clamp(progress, 0, 1);
                    if (field.TryGetProperty("progressIndeterminate", out var indeterminate))
                        progressControl.IsIndeterminate = indeterminate.ValueKind == JsonValueKind.True;
                    if (field.TryGetProperty("activityRunning", out var running))
                        progressControl.IsVisible = running.ValueKind != JsonValueKind.False;
                    continue;
                }
                if (!editors.TryGetValue(name, out var editor)) continue;
                var validationError = field.TryGetProperty("validationError", out var validationValue)
                    ? validationValue.GetString() ?? string.Empty
                    : string.Empty;
                if (fieldLabels.TryGetValue(name, out var labelBlock) && field.TryGetProperty("label", out var labelValue))
                    labelBlock.Text = labelValue.GetString() ?? string.Empty;
                if (fieldContainers.TryGetValue(name, out var fieldContainer))
                    fieldContainer.IsVisible = !field.TryGetProperty("visible", out var visible) || visible.ValueKind != JsonValueKind.False;
                var enabledState = !field.TryGetProperty("enabled", out var enabled) || enabled.ValueKind != JsonValueKind.False;
                var readOnlyState = field.TryGetProperty("readOnly", out var readOnly) && readOnly.ValueKind == JsonValueKind.True;
                editor.IsEnabled = enabledState;
                ApplyEditorReadOnly(editor, readOnlyState);
                if (field.TryGetProperty("options", out var optionValues) && optionValues.ValueKind == JsonValueKind.Array)
                    ApplyEditorOptions(editor, optionValues.EnumerateArray().Select(option => option.GetString() ?? string.Empty).ToArray());
                if (validationError.Length == 0)
                {
                    if (field.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Array)
                        SetEditorValues(editor, values);
                    else if (field.TryGetProperty("value", out var value) && value.ValueKind != JsonValueKind.Null)
                        SetEditorValue(editor, value);
                }
                if (validationErrors.TryGetValue(name, out var validationBlock))
                {
                    validationBlock.Text = validationError;
                    validationBlock.IsVisible = validationError.Length > 0;
                }
                if (validationError.Length > 0)
                    hasValidationErrors = true;
            }
        }
        if (root.TryGetProperty("buttons", out var buttons) && buttons.ValueKind == JsonValueKind.Array)
        {
            foreach (var button in buttons.EnumerateArray())
            {
                var buttonName = button.TryGetProperty("name", out var nameValue) ? nameValue.GetString() ?? string.Empty : string.Empty;
                if (buttonName.Length == 0 || !actionButtons.TryGetValue(buttonName, out var actionButton)) continue;
                if (button.TryGetProperty("label", out var labelValue))
                {
                    var label = labelValue.GetString() ?? buttonName;
                    var content = actionButton.Tag as string[] ?? Array.Empty<string>();
                    actionButton.Content = CreateButtonContent(content.ElementAtOrDefault(0) ?? string.Empty, content.ElementAtOrDefault(1) ?? string.Empty, label);
                }
                if (button.TryGetProperty("visible", out var visibleValue))
                    actionButton.IsVisible = visibleValue.ValueKind != JsonValueKind.False;
                if (button.TryGetProperty("enabled", out var enabledValue))
                    actionButton.IsEnabled = enabledValue.ValueKind != JsonValueKind.False;
            }
        }
        if (root.TryGetProperty("navigation", out var navigation) && navigation.ValueKind == JsonValueKind.Object)
            Log.Info("XPScript", "UIForm navigation requested: " + navigation);
        return hasValidationErrors;
    }

    private static void ApplyEditorReadOnly(Control editor, bool readOnly)
    {
        if (editor is TextBox textBox)
        {
            textBox.IsReadOnly = readOnly;
            return;
        }
        if (readOnly)
            editor.IsEnabled = false;
    }

    private static void ApplyEditorOptions(Control editor, IReadOnlyList<string> options)
    {
        if (editor is ComboBox comboBox)
            comboBox.ItemsSource = options.ToArray();
        else if (editor is ListBox listBox)
            listBox.ItemsSource = options.ToArray();
        else if (editor is StackPanel radioPanel)
        {
            radioPanel.Children.Clear();
            var groupName = Guid.NewGuid().ToString("N");
            foreach (var option in options)
                radioPanel.Children.Add(new Avalonia.Controls.RadioButton { Content = option, Tag = option, GroupName = groupName });
        }
    }

    private static void SetEditorValues(Control editor, JsonElement values)
    {
        if (editor is not ListBox listBox) return;
        listBox.SelectedItems?.Clear();
        foreach (var item in values.EnumerateArray())
        {
            var selected = item.GetString() ?? string.Empty;
            if (listBox.ItemsSource is IEnumerable<string> options && options.Contains(selected))
                listBox.SelectedItems?.Add(selected);
        }
    }

    private static void AddFieldContainer(JsonElement field, Control container, StackPanel targetPanel, Grid? targetGrid)
    {
        if (targetGrid is null)
        {
            targetPanel.Children.Add(container);
            return;
        }
        var row = field.TryGetProperty("layoutRow", out var rowValue) && rowValue.TryGetInt32(out var r) ? Math.Max(0, r - 1) : targetGrid.RowDefinitions.Count;
        var column = field.TryGetProperty("layoutColumn", out var columnValue) && columnValue.TryGetInt32(out var c) ? Math.Max(0, c - 1) : 0;
        var columnSpan = field.TryGetProperty("columnSpan", out var csValue) && csValue.TryGetInt32(out var cs) ? Math.Max(1, cs) : 1;
        var rowSpan = field.TryGetProperty("rowSpan", out var rsValue) && rsValue.TryGetInt32(out var rs) ? Math.Max(1, rs) : 1;
        while (targetGrid.RowDefinitions.Count < row + rowSpan) targetGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetRow(container, row);
        Grid.SetColumn(container, column);
        Grid.SetColumnSpan(container, columnSpan);
        Grid.SetRowSpan(container, rowSpan);
        targetGrid.Children.Add(container);
    }

    private static Control CreateVideo(JsonElement field)
    {
        var view = new GeneratedAndroidVideoControl
        {
            MinHeight = 180,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        if (field.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.String)
            view.Source = source.GetString() ?? string.Empty;
        return view;
    }

    private static Control CreateAudio(JsonElement field)
    {
        var audio = new GeneratedAndroidAudioControl();
        if (field.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.String)
            audio.Source = source.GetString() ?? string.Empty;
        return audio;
    }

    private static Control CreateProgressBar(JsonElement field)
    {
        var progress = new Avalonia.Controls.ProgressBar { Minimum = 0, Maximum = 1, HorizontalAlignment = HorizontalAlignment.Stretch };
        if (field.TryGetProperty("progressValue", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var progressValue))
            progress.Value = Math.Clamp(progressValue, 0, 1);
        if (field.TryGetProperty("progressIndeterminate", out var indeterminate) && indeterminate.ValueKind == JsonValueKind.True)
            progress.IsIndeterminate = true;
        return progress;
    }

    private static Control CreateActivityIndicator(JsonElement field)
    {
        var progress = new Avalonia.Controls.ProgressBar { IsIndeterminate = true, HorizontalAlignment = HorizontalAlignment.Stretch };
        if (field.TryGetProperty("activityRunning", out var running) && running.ValueKind == JsonValueKind.False)
            progress.IsVisible = false;
        return progress;
    }

    private static Control CreateRadioGroup(IReadOnlyList<string> options)
    {
        var panel = new StackPanel { Spacing = 6 };
        var groupName = Guid.NewGuid().ToString("N");
        foreach (var option in options)
            panel.Children.Add(new Avalonia.Controls.RadioButton { Content = option, Tag = option, GroupName = groupName });
        return panel;
    }

    private static Control CreateRangeField(JsonElement field)
    {
        var minimum = ReadNumber(field, "minimum", 0);
        var maximum = ReadNumber(field, "maximum", 100);
        var step = ReadNumber(field, "step", 1);
        var slider = new Slider { Minimum = minimum, Maximum = maximum, TickFrequency = step, IsSnapToTickEnabled = true };
        if (field.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var current))
            slider.Value = Math.Clamp(current, minimum, maximum);
        return slider;
    }

    private static Control CreateNumberField(JsonElement field)
    {
        var minimum = ReadNumber(field, "minimum", -double.MaxValue);
        var maximum = ReadNumber(field, "maximum", double.MaxValue);
        if (maximum <= minimum) maximum = minimum + 1;
        var editor = new NumericUpDown
        {
            Minimum = (decimal)Math.Max(minimum, (double)decimal.MinValue),
            Maximum = (decimal)Math.Min(maximum, (double)decimal.MaxValue),
            Increment = (decimal)Math.Max(ReadNumber(field, "step", 1), double.Epsilon),
            CornerRadius = new CornerRadius(field.TryGetProperty("cornerRadius", out var radius) && radius.TryGetDouble(out var value) ? value : 0)
        };
        if (field.TryGetProperty("value", out var currentValue) && currentValue.TryGetDecimal(out var current))
            editor.Value = Math.Clamp(current, editor.Minimum, editor.Maximum);
        return editor;
    }

    private static double ReadNumber(JsonElement element, string propertyName, double fallback)
    {
        return element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetDouble(out var number)
            ? number
            : fallback;
    }

    private static Control CreateIcon(JsonElement field)
    {
        var icon = field.TryGetProperty("icon", out var iconValue) ? iconValue.GetString() ?? string.Empty : string.Empty;
        var glyph = icon.ToLowerInvariant() switch
        {
            "check" or "success" => "✓",
            "close" or "cancel" => "×",
            "warning" => "⚠",
            "info" => "ⓘ",
            "play" => "▶",
            "pause" => "⏸",
            "stop" => "⏹",
            "heart" => "♥",
            "star" => "★",
            _ => icon.Length == 1 ? icon : "•"
        };
        return new TextBlock { Text = glyph, FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center };
    }

    private static Control CreateCard(JsonElement field)
    {
        var label = field.TryGetProperty("label", out var labelValue) ? labelValue.GetString() ?? string.Empty : string.Empty;
        var content = new StackPanel { Spacing = 6 };
        if (label.Length > 0) content.Children.Add(new TextBlock { Text = label, FontWeight = FontWeight.SemiBold });
        return new Border
        {
            Padding = new Thickness(12),
            Margin = new Thickness(2),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = content
        };
    }

    private static Control CreateScrollView(JsonElement field)
    {
        var label = field.TryGetProperty("label", out var labelValue) ? labelValue.GetString() ?? string.Empty : string.Empty;
        var content = new StackPanel { Spacing = 6 };
        if (label.Length > 0) content.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap });
        return new ScrollViewer
        {
            MaxHeight = 240,
            Content = content
        };
    }

    private static Avalonia.Controls.NativeWebView CreateWebView(JsonElement field, string instanceId, string fieldName)
    {
        var view = new Avalonia.Controls.NativeWebView
        {
            MinHeight = 240,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var source = field.TryGetProperty("webViewSource", out var sourceValue) ? sourceValue.GetString() ?? "about:blank" : "about:blank";
        var html = field.TryGetProperty("webViewHtml", out var htmlValue) ? htmlValue.GetString() ?? string.Empty : string.Empty;
        var userAgent = field.TryGetProperty("webViewUserAgent", out var userAgentValue) ? userAgentValue.GetString() ?? string.Empty : string.Empty;
        var background = field.TryGetProperty("webViewBackground", out var backgroundValue) ? backgroundValue.GetString() ?? string.Empty : string.Empty;
        if (!string.IsNullOrWhiteSpace(userAgent)) view.UserAgent = userAgent;
        if (!string.IsNullOrWhiteSpace(background)) view.Background = new SolidColorBrush(Color.Parse(background));
        if (instanceId.Length > 0 && fieldName.Length > 0) WebViews[WebViewKey(instanceId, fieldName)] = view;
        if (!string.IsNullOrEmpty(html)) view.AdapterCreated += (_, _) => view.NavigateToString(html);
        else view.Source = ResolveAndroidWebViewUri(source);
        return view;
    }

    public static string WebViewCommand(string instanceId, string fieldName, string command, string? argument)
    {
        if (!WebViews.TryGetValue(WebViewKey(instanceId, fieldName), out var view))
            throw new InvalidOperationException("UIForm WebView is not active.");

        string result = string.Empty;
        void Execute()
        {
            result = command.ToLowerInvariant() switch
            {
                "source" => view.Source?.ToString() ?? string.Empty,
                "navigate" => NavigateWebView(view, argument),
                "html" => NavigateWebViewHtml(view, argument),
                "script" => WaitWebView(view.InvokeScript(argument ?? string.Empty)) ?? string.Empty,
                "back" => BoolWebView(view.GoBack()),
                "forward" => BoolWebView(view.GoForward()),
                "refresh" => BoolWebView(view.Refresh()),
                "stop" => BoolWebView(view.Stop()),
                "cangoback" => BoolWebView(view.CanGoBack),
                "cangoforward" => BoolWebView(view.CanGoForward),
                "useragent:get" => view.UserAgent ?? string.Empty,
                "useragent:set" => SetWebViewUserAgent(view, argument),
                "background:get" => view.Background?.ToString() ?? string.Empty,
                "background:set" => SetWebViewBackground(view, argument),
                "adapterinfo" => view.AdapterInfo?.ToString() ?? string.Empty,
                "platformhandle" => view.TryGetPlatformHandle()?.Handle.ToString() ?? string.Empty,
                "copy" => EditWebView(view, manager => manager.Copy()),
                "cut" => EditWebView(view, manager => manager.Cut()),
                "paste" => EditWebView(view, manager => manager.Paste()),
                "selectall" => EditWebView(view, manager => manager.SelectAll()),
                "undo" => EditWebView(view, manager => manager.Undo()),
                "redo" => EditWebView(view, manager => manager.Redo()),
                "cookies:get" => GetWebViewCookies(view),
                "cookies:set" => SetWebViewCookie(view, argument),
                "cookies:delete" => DeleteWebViewCookie(view, argument),
                "cookies:clear" => ClearWebViewCookies(view),
                _ => throw new InvalidOperationException("Unknown UIForm WebView command: " + command)
            };
        }
        if (Dispatcher.UIThread.CheckAccess()) Execute(); else Dispatcher.UIThread.Invoke(Execute);
        return result;
    }

    private static string NavigateWebView(Avalonia.Controls.NativeWebView view, string? value)
    {
        var uri = ResolveAndroidWebViewUri(value);
        view.Navigate(uri);
        return uri.AbsoluteUri;
    }

    private static Uri ResolveAndroidWebViewUri(string? value)
    {
        var source = string.IsNullOrWhiteSpace(value) ? "about:blank" : value.Trim();
        if (Uri.TryCreate(source, UriKind.Absolute, out var absolute)) return absolute;
        return new Uri(ResolveAndroidImagePath(source));
    }

    private static string NavigateWebViewHtml(Avalonia.Controls.NativeWebView view, string? html) { view.NavigateToString(html ?? string.Empty); return "true"; }
    private static string SetWebViewUserAgent(Avalonia.Controls.NativeWebView view, string? value) { view.UserAgent = value ?? string.Empty; return view.UserAgent ?? string.Empty; }
    private static string SetWebViewBackground(Avalonia.Controls.NativeWebView view, string? value) { if (!string.IsNullOrWhiteSpace(value)) view.Background = new SolidColorBrush(Color.Parse(value)); return view.Background?.ToString() ?? string.Empty; }
    private static string EditWebView(Avalonia.Controls.NativeWebView view, Action<Avalonia.Controls.NativeWebViewCommandManager> action) { var manager = view.TryGetCommandManager(); if (manager is null) return "false"; action(manager); return "true"; }
    private static string GetWebViewCookies(Avalonia.Controls.NativeWebView view) { var manager = view.TryGetCookieManager(); if (manager is null) return "[]"; var cookies = WaitWebView(manager.GetCookiesAsync()) ?? []; return JsonSerializer.Serialize(cookies.Select(cookie => new { cookie.Name, cookie.Value, cookie.Domain, cookie.Path, cookie.Secure, cookie.HttpOnly })); }
    private static string SetWebViewCookie(Avalonia.Controls.NativeWebView view, string? payload) { var manager = view.TryGetCookieManager(); if (manager is null) return "false"; using var document = JsonDocument.Parse(payload ?? "{}"); manager.AddOrUpdateCookie(BuildWebViewCookie(document.RootElement, true)); return "true"; }
    private static string DeleteWebViewCookie(Avalonia.Controls.NativeWebView view, string? payload) { var manager = view.TryGetCookieManager(); if (manager is null) return "false"; using var document = JsonDocument.Parse(payload ?? "{}"); var cookie = BuildWebViewCookie(document.RootElement, false); manager.DeleteCookie(cookie.Name, cookie.Domain, cookie.Path); return "true"; }
    private static string ClearWebViewCookies(Avalonia.Controls.NativeWebView view) { var manager = view.TryGetCookieManager(); if (manager is null) return "false"; var cookies = WaitWebView(manager.GetCookiesAsync()) ?? []; foreach (var cookie in cookies) manager.DeleteCookie(cookie.Name, cookie.Domain, cookie.Path); return "true"; }
    private static System.Net.Cookie BuildWebViewCookie(JsonElement root, bool includeValue) { static string Read(JsonElement value, string name, string fallback = "") => value.TryGetProperty(name, out var property) ? property.GetString() ?? fallback : fallback; var name = Read(root, "name"); var domain = Read(root, "domain"); var path = Read(root, "path", "/"); if (name.Length == 0 || domain.Length == 0) throw new InvalidOperationException("WebView cookies require name and domain."); return new System.Net.Cookie(name, includeValue ? Read(root, "value") : string.Empty, path.Length == 0 ? "/" : path, domain); }
    private static T? WaitWebView<T>(Task<T> task) { if (!Dispatcher.UIThread.CheckAccess() || task.IsCompleted) return task.GetAwaiter().GetResult(); var frame = new DispatcherFrame(); task.ContinueWith(_ => Dispatcher.UIThread.Post(() => frame.Continue = false), TaskScheduler.Default); Dispatcher.UIThread.PushFrame(frame); return task.GetAwaiter().GetResult(); }
    private static string BoolWebView(bool value) => value ? "true" : "false";
    private static string WebViewKey(string instanceId, string fieldName) => instanceId + "\u001f" + fieldName.ToLowerInvariant();

    private const int MaximumAndroidImageBytes = 32 * 1024 * 1024;

    private static HttpClient CreateAndroidImageHttpClient(string certificateValidation)
    {
        var mode = string.IsNullOrWhiteSpace(certificateValidation) ? "Strict" : certificateValidation.Trim();
        if (!(mode.Equals("Strict", StringComparison.OrdinalIgnoreCase) ||
              mode.Equals("AllowSelfSigned", StringComparison.OrdinalIgnoreCase) ||
              mode.Equals("Insecure", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Image certificate validation must be Strict, AllowSelfSigned, or Insecure.");

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                     System.Net.DecompressionMethods.Deflate |
                                     System.Net.DecompressionMethods.Brotli,
            ServerCertificateCustomValidationCallback = (_, _, chain, errors) =>
            {
                if (errors == System.Net.Security.SslPolicyErrors.None) return true;
                if (mode.Equals("Insecure", StringComparison.OrdinalIgnoreCase)) return true;
                if (!mode.Equals("AllowSelfSigned", StringComparison.OrdinalIgnoreCase)) return false;
                if ((errors & (System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch |
                               System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable)) != 0)
                    return false;
                var statuses = chain?.ChainStatus ?? [];
                return statuses.Length > 0 && statuses.All(status =>
                    status.Status is System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.UntrustedRoot or
                                     System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.PartialChain or
                                     System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.NoError);
            }
        };
        return new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(15) };
    }

    private static Control CreateButtonContent(string icon, string imageSource, string label)
    {
        var content = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
        if (!string.IsNullOrWhiteSpace(icon)) content.Children.Add(new TextBlock { Text = icon, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        if (!string.IsNullOrWhiteSpace(imageSource))
        {
            try
            {
                var bytes = ReadAndroidImageBytes(imageSource, "Strict");
                using var stream = new MemoryStream(bytes, writable: false);
                content.Children.Add(new Avalonia.Controls.Image { Source = new Bitmap(stream), Width = 20, Height = 20, Stretch = Stretch.Uniform });
            }
            catch (Exception exception)
            {
                Log.Error("XPScript", "UIForm button image failed: " + exception.Message);
            }
        }
        content.Children.Add(new TextBlock { Text = label, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        return content;
    }

    private static Control CreateCameraPreview()
    {
        var activity = MainActivity.Current;
        if (activity is null || ContextCompat.CheckSelfPermission(activity, global::Android.Manifest.Permission.Camera) != global::Android.Content.PM.Permission.Granted)
        {
            activity?.RequestCameraPermission();
            return new TextBlock { Text = "Camera permission is required. Grant permission and reopen the form.", TextWrapping = TextWrapping.Wrap };
        }
        return new GeneratedAndroidCameraPreviewControl { MinHeight = 180 };
    }

    private static Control CreateCarousel(JsonElement field)
    {
        var carousel = new TabControl { MinHeight = 220, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        var sources = field.TryGetProperty("carouselSources", out var sourceValues) && sourceValues.ValueKind == JsonValueKind.Array
            ? sourceValues.EnumerateArray().Select(source => source.GetString() ?? string.Empty).Where(source => source.Length > 0).ToArray()
            : Array.Empty<string>();
        foreach (var source in sources)
        {
            var imageField = JsonSerializer.SerializeToElement(new { imageSource = source });
            carousel.Items.Add(new TabItem { Header = (carousel.Items.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), Content = CreateImage(imageField) });
        }
        if (field.TryGetProperty("carouselIndex", out var indexValue) && indexValue.TryGetInt32(out var index))
            carousel.SelectedIndex = Math.Clamp(index, 0, Math.Max(0, carousel.Items.Count - 1));
        return carousel;
    }

    private static Control CreateImage(JsonElement field)
    {
        var image = new Avalonia.Controls.Image { MaxHeight = 320, Stretch = Stretch.Uniform };
        var source = field.TryGetProperty("imageSource", out var sourceValue) ? sourceValue.GetString() ?? string.Empty : string.Empty;
        var altText = field.TryGetProperty("imageAltText", out var altValue) ? altValue.GetString() ?? string.Empty : string.Empty;
        var certificateValidation = field.TryGetProperty("imageCertificateValidation", out var certificateValue) ? certificateValue.GetString() ?? "Strict" : "Strict";
        try
        {
            var bytes = ReadAndroidImageBytes(source, certificateValidation);
            using var stream = new MemoryStream(bytes, writable: false);
            image.Source = new Bitmap(stream);
            if (!string.IsNullOrWhiteSpace(altText)) AutomationProperties.SetName(image, altText);
        }
        catch (Exception exception)
        {
            Log.Error("XPScript", "UIForm image failed: " + exception.Message);
        }
        return image;
    }

    private static byte[] ReadAndroidImageBytes(string source, string certificateValidation)
    {
        var value = (source ?? string.Empty).Trim();
        if (value.Length == 0) throw new InvalidOperationException("UIForm image source is empty.");

        if (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            var comma = value.IndexOf(',');
            if (comma <= 0 || !value[..comma].Contains(";base64", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("UIForm Android Image supports base64 data:image URLs.");
            var bytes = Convert.FromBase64String(value[(comma + 1)..]);
            ValidateAndroidImageSize(bytes.LongLength);
            return bytes;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            using var http = CreateAndroidImageHttpClient(certificateValidation);
            var bytes = http.GetByteArrayAsync(uri).GetAwaiter().GetResult();
            ValidateAndroidImageSize(bytes.LongLength);
            return bytes;
        }

        var path = ResolveAndroidImagePath(value);
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("UIForm image asset was not found.", path);
        ValidateAndroidImageSize(info.Length);
        return File.ReadAllBytes(path);
    }

    private static string ResolveAndroidImagePath(string source)
    {
        var value = (source ?? string.Empty).Trim();
        if (Path.IsPathRooted(value)) return Path.GetFullPath(value);
        if (value.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("UIForm image relative path may not contain '..'.");

        var normalized = value.Replace('/', Path.DirectorySeparatorChar);
        var baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        var assetsPrefix = "assets" + Path.DirectorySeparatorChar;
        var assetRelative = normalized.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase) ? normalized[assetsPrefix.Length..] : normalized;
        var localAssets = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "assets");
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(localAssets, assetRelative)),
            Path.GetFullPath(Path.Combine(baseDirectory, normalized)),
            Path.GetFullPath(Path.Combine(baseDirectory, "assets", assetRelative)),
            Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, normalized))
        };
        foreach (var candidate in candidates)
            if (File.Exists(candidate)) return candidate;
        return candidates[0];
    }

    private static void ValidateAndroidImageSize(long length)
    {
        if (length <= 0 || length > MaximumAndroidImageBytes)
            throw new InvalidOperationException("UIForm image must contain between 1 byte and 32 MiB.");
    }

    private sealed class AndroidDateTimeFieldEditor : StackPanel
    {
        public Avalonia.Controls.DatePicker DateEditor { get; } = new Avalonia.Controls.DatePicker();
        public Avalonia.Controls.TimePicker TimeEditor { get; } = new Avalonia.Controls.TimePicker();

        public AndroidDateTimeFieldEditor()
        {
            Spacing = 8;
            Orientation = Avalonia.Layout.Orientation.Horizontal;
            Children.Add(DateEditor);
            Children.Add(TimeEditor);
        }
    }

    private sealed class AndroidMonthFieldEditor : StackPanel
    {
        public NumericUpDown YearEditor { get; } = new NumericUpDown { Minimum = 1, Maximum = 9999, Width = 120 };
        public ComboBox MonthEditor { get; } = new ComboBox
        {
            ItemsSource = Enumerable.Range(1, 12).Select(month => month.ToString("00", System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
            Width = 100
        };

        public AndroidMonthFieldEditor()
        {
            Spacing = 8;
            Orientation = Avalonia.Layout.Orientation.Horizontal;
            Children.Add(YearEditor);
            Children.Add(MonthEditor);
        }
    }

    private sealed class AndroidWeekFieldEditor : StackPanel
    {
        public NumericUpDown YearEditor { get; } = new NumericUpDown { Minimum = 1, Maximum = 9999, Width = 120 };
        public NumericUpDown WeekEditor { get; } = new NumericUpDown { Minimum = 1, Maximum = 53, Width = 100 };

        public AndroidWeekFieldEditor()
        {
            Spacing = 8;
            Orientation = Avalonia.Layout.Orientation.Horizontal;
            Children.Add(YearEditor);
            Children.Add(WeekEditor);
        }
    }

    private sealed class AndroidColorFieldEditor : StackPanel
    {
        public TextBox ValueEditor { get; } = new TextBox { PlaceholderText = "#RRGGBB", MinWidth = 180 };
        public Border Preview { get; } = new Border { Width = 36, Height = 36, BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray };

        public AndroidColorFieldEditor()
        {
            Spacing = 8;
            Orientation = Avalonia.Layout.Orientation.Horizontal;
            ValueEditor.TextChanged += (_, _) =>
            {
                if (Avalonia.Media.Color.TryParse(ValueEditor.Text, out var color))
                    Preview.Background = new Avalonia.Media.SolidColorBrush(color);
            };
            Preview.Tapped += (_, _) => ShowColorMap();
            Children.Add(ValueEditor);
            Children.Add(Preview);
        }

        private void ShowColorMap()
        {
            var grid = new UniformGrid { Rows = 8, Columns = 18, Margin = new Thickness(6) };
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 18; x++)
            {
                var color = ColorMapColorAt(18, 8, x, y);
                var button = new Avalonia.Controls.Button { Width = 24, Height = 24, Padding = new Thickness(0), Background = new SolidColorBrush(Color.Parse(color)) };
                button.Click += (_, _) => ValueEditor.Text = color;
                grid.Children.Add(button);
            }
            var flyout = new Flyout { Content = grid, Placement = PlacementMode.Bottom };
            flyout.ShowAt(Preview);
        }

        private static string ColorMapColorAt(int width, int height, int x, int y)
        {
            var hue = width <= 1 ? 0d : (double)x / (width - 1) * 360d;
            var saturation = height <= 1 ? 1d : (double)y / (height - 1);
            var chroma = saturation;
            var segment = hue / 60d;
            var intermediate = chroma * (1d - Math.Abs(segment % 2d - 1d));
            var (red, green, blue) = segment switch
            {
                < 1d => (chroma, intermediate, 0d), < 2d => (intermediate, chroma, 0d),
                < 3d => (0d, chroma, intermediate), < 4d => (0d, intermediate, chroma),
                < 5d => (intermediate, 0d, chroma), _ => (chroma, 0d, intermediate)
            };
            return $"#{(int)Math.Round(red * 255d):X2}{(int)Math.Round(green * 255d):X2}{(int)Math.Round(blue * 255d):X2}";
        }
    }

    private static bool SetTemporalEditorValue(Control editor, string text)
    {
        if (editor is Avalonia.Controls.DatePicker datePicker)
        {
            if (DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var date))
                datePicker.SelectedDate = date;
            else
                datePicker.SelectedDate = null;
            return true;
        }
        if (editor is Avalonia.Controls.TimePicker timePicker)
        {
            timePicker.SelectedTime = TimeSpan.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var time) ? time : null;
            return true;
        }
        if (editor is AndroidDateTimeFieldEditor dateTimeEditor)
        {
            if (DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var dateTime))
            {
                dateTimeEditor.DateEditor.SelectedDate = dateTime;
                dateTimeEditor.TimeEditor.SelectedTime = dateTime.TimeOfDay;
            }
            else
            {
                dateTimeEditor.DateEditor.SelectedDate = null;
                dateTimeEditor.TimeEditor.SelectedTime = null;
            }
            return true;
        }
        if (editor is AndroidMonthFieldEditor monthEditor)
        {
            if (DateTime.TryParseExact(text + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var month))
            {
                monthEditor.YearEditor.Value = month.Year;
                monthEditor.MonthEditor.SelectedIndex = month.Month - 1;
            }
            else
            {
                monthEditor.YearEditor.Value = DateTime.Now.Year;
                monthEditor.MonthEditor.SelectedIndex = DateTime.Now.Month - 1;
            }
            return true;
        }
        if (editor is AndroidWeekFieldEditor weekEditor)
        {
            var weekParts = text.Split('-');
            if (weekParts.Length == 2 && int.TryParse(weekParts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var weekYear) &&
                weekParts[1].StartsWith("W", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(weekParts[1][1..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var weekNumber) &&
                weekNumber is >= 1 and <= 53)
            {
                weekEditor.YearEditor.Value = weekYear;
                weekEditor.WeekEditor.Value = weekNumber;
            }
            else
            {
                var now = DateTime.Now;
                weekEditor.YearEditor.Value = System.Globalization.ISOWeek.GetYear(now);
                weekEditor.WeekEditor.Value = System.Globalization.ISOWeek.GetWeekOfYear(now);
            }
            return true;
        }
        if (editor is AndroidColorFieldEditor colorEditor)
        {
            colorEditor.ValueEditor.Text = text;
            return true;
        }
        return false;
    }

    private static string? GetTemporalEditorValue(Control editor)
    {
        if (editor is Avalonia.Controls.DatePicker datePicker)
            return datePicker.SelectedDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        if (editor is Avalonia.Controls.TimePicker timePicker)
            return timePicker.SelectedTime?.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        if (editor is AndroidDateTimeFieldEditor dateTimeEditor)
        {
            if (dateTimeEditor.DateEditor.SelectedDate is not DateTimeOffset date) return string.Empty;
            var time = dateTimeEditor.TimeEditor.SelectedTime ?? TimeSpan.Zero;
            return date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + "T" +
                   time.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture);
        }
        if (editor is AndroidMonthFieldEditor monthEditor)
        {
            var year = Convert.ToInt32(monthEditor.YearEditor.Value ?? DateTime.Now.Year, System.Globalization.CultureInfo.InvariantCulture);
            var month = monthEditor.MonthEditor.SelectedIndex >= 0 ? monthEditor.MonthEditor.SelectedIndex + 1 : DateTime.Now.Month;
            return year.ToString("0000", System.Globalization.CultureInfo.InvariantCulture) + "-" +
                   month.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }
        if (editor is AndroidWeekFieldEditor weekEditor)
        {
            var year = Convert.ToInt32(weekEditor.YearEditor.Value ?? System.Globalization.ISOWeek.GetYear(DateTime.Now), System.Globalization.CultureInfo.InvariantCulture);
            var week = Convert.ToInt32(weekEditor.WeekEditor.Value ?? System.Globalization.ISOWeek.GetWeekOfYear(DateTime.Now), System.Globalization.CultureInfo.InvariantCulture);
            return year.ToString("0000", System.Globalization.CultureInfo.InvariantCulture) + "-W" + week.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }
        if (editor is AndroidColorFieldEditor colorEditor)
            return colorEditor.ValueEditor.Text ?? string.Empty;
        return null;
    }

    private static void SetEditorValue(Control editor, JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
        if (SetTemporalEditorValue(editor, text)) return;
        if (editor is TextBox textBox) textBox.Text = text;
        else if (editor is NumericUpDown numericEditor && decimal.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var numericValue))
            numericEditor.Value = Math.Clamp(numericValue, numericEditor.Minimum, numericEditor.Maximum);
        else if (editor is Avalonia.Controls.CheckBox checkBox)
            checkBox.IsChecked = value.ValueKind == JsonValueKind.True || text.Equals("true", StringComparison.OrdinalIgnoreCase);
        else if (editor is Avalonia.Controls.ToggleSwitch toggleSwitch)
            toggleSwitch.IsChecked = value.ValueKind == JsonValueKind.True || text.Equals("true", StringComparison.OrdinalIgnoreCase);
        else if (editor is ComboBox comboBox) { if (comboBox.IsEditable) comboBox.Text = text; else comboBox.SelectedItem = text; }
        else if (editor is ListBox listBox)
        {
            if (value.ValueKind == JsonValueKind.Array)
            {
                listBox.SelectedItems?.Clear();
                foreach (var item in value.EnumerateArray())
                {
                    var selected = item.GetString() ?? string.Empty;
                    if (listBox.ItemsSource is IEnumerable<string> options && options.Contains(selected))
                        listBox.SelectedItems?.Add(selected);
                }
            }
            else listBox.SelectedItem = text;
        }
        else if (editor is Slider slider && double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number))
            slider.Value = number;
        else if (editor is StackPanel radioPanel)
        {
            foreach (var radio in radioPanel.Children.OfType<Avalonia.Controls.RadioButton>())
                radio.IsChecked = string.Equals(Convert.ToString(radio.Tag, System.Globalization.CultureInfo.InvariantCulture), text, StringComparison.Ordinal);
        }
    }

    private static object? GetEditorValue(Control editor)
    {
        if (editor is Avalonia.Controls.DatePicker or Avalonia.Controls.TimePicker or AndroidDateTimeFieldEditor or AndroidMonthFieldEditor or AndroidWeekFieldEditor or AndroidColorFieldEditor)
            return GetTemporalEditorValue(editor);

        if (editor is Avalonia.Controls.Button fileButton)
            return fileButton.Tag?.ToString() ?? string.Empty;

        return editor switch
        {
        TextBox textBox => textBox.Text ?? string.Empty,
        NumericUpDown numericEditor => numericEditor.Value,
        Avalonia.Controls.CheckBox checkBox => checkBox.IsChecked == true,
        Avalonia.Controls.ToggleSwitch toggleSwitch => toggleSwitch.IsChecked == true,
        ComboBox comboBox => comboBox.IsEditable ? comboBox.Text ?? string.Empty : comboBox.SelectedItem?.ToString() ?? string.Empty,
        ListBox listBox when listBox.SelectionMode.HasFlag(SelectionMode.Multiple) => listBox.SelectedItems?.Cast<object>().Select(item => item?.ToString() ?? string.Empty).ToArray() ?? Array.Empty<string>(),
        ListBox listBox => listBox.SelectedItem?.ToString() ?? string.Empty,
        Slider slider => slider.Value,
        StackPanel radioPanel => radioPanel.Children.OfType<Avalonia.Controls.RadioButton>().FirstOrDefault(radio => radio.IsChecked == true)?.Tag?.ToString() ?? string.Empty,
        _ => null
        };
    }
}
""";
}
