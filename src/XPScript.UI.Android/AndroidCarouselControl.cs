using Avalonia.Controls;
using Avalonia.Platform;
using Android.Views;
using Android.Widget;
using AndroidX.ViewPager2.Widget;
using Android.Graphics;
using System.Net.Http;
using System.Text;
using Avalonia.Threading;

namespace XPScript.UI.Android;

/// <summary>Isolated Android ViewPager2 bridge for the shared UIForm Carousel.</summary>
public sealed class AndroidCarouselControl : NativeControlHost
{
    private ViewPager2? _pager;
    private FrameLayout? _root;
    private LinearLayout? _indicators;
    private CarouselAdapter? _adapter;
    private Timer? _autoAdvanceTimer;
    public event EventHandler<int>? CurrentItemChanged;
    public IReadOnlyList<string> Sources
    {
        get => _adapter?.Sources ?? Array.Empty<string>();
        set { if (_adapter is null) return; _adapter.Sources = value?.ToArray() ?? Array.Empty<string>(); _adapter.NotifyDataSetChanged(); RefreshIndicators(); RestartAutoAdvance(); }
    }
    public bool Loop { get; set; }
    public int? AutoAdvanceMilliseconds { get => _autoAdvanceTimer is null ? null : _autoAdvanceInterval; set => ConfigureAutoAdvance(value); }
    private int? _autoAdvanceInterval;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _root = new FrameLayout(global::Android.App.Application.Context!);
        _pager = new ViewPager2(global::Android.App.Application.Context!);
        _adapter = new CarouselAdapter(global::Android.App.Application.Context!);
        _pager.Adapter = _adapter;
        _pager.Orientation = ViewPager2.OrientationHorizontal;
        _root.AddView(_pager, new FrameLayout.LayoutParams(-1, -1));
        _indicators = new LinearLayout(_root.Context) { Orientation = Orientation.Horizontal, Gravity = GravityFlags.Center };
        var indicatorLayout = new FrameLayout.LayoutParams(-2, -2, GravityFlags.Bottom | GravityFlags.CenterHorizontal) { BottomMargin = 12 };
        _root.AddView(_indicators, indicatorLayout);
        _pager.RegisterOnPageChangeCallback(new PageCallback(index => { RefreshIndicators(index); CurrentItemChanged?.Invoke(this, index); }));
        RefreshIndicators();
        RestartAutoAdvance();
        return new PlatformHandle(_root.Handle, "Android.View.View");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _pager?.Adapter = null;
        _autoAdvanceTimer?.Dispose();
        _autoAdvanceTimer = null;
        _adapter = null;
        _pager?.Dispose();
        _pager = null;
        _root?.Dispose();
        _root = null;
        _indicators = null;
    }

    private void RefreshIndicators(int? selected = null)
    {
        if (_indicators is null || _adapter is null) return;
        _indicators.RemoveAllViews();
        var current = selected ?? _pager?.CurrentItem ?? 0;
        for (var index = 0; index < _adapter.ItemCount; index++)
        {
            var item = new TextView(_indicators.Context) { Text = index == current ? "●" : "○", TextSize = 18, ContentDescription = $"Carousel item {index + 1}" };
            var captured = index;
            item.SetOnClickListener(new ClickListener(() => _pager?.SetCurrentItem(captured, true)));
            _indicators.AddView(item, new LinearLayout.LayoutParams(-2, -2));
        }
    }

    private void ConfigureAutoAdvance(int? milliseconds)
    {
        if (milliseconds is < 250) throw new ArgumentOutOfRangeException(nameof(milliseconds), "Carousel auto-advance interval must be at least 250 milliseconds.");
        _autoAdvanceInterval = milliseconds;
        RestartAutoAdvance();
    }

    private void RestartAutoAdvance()
    {
        _autoAdvanceTimer?.Dispose();
        _autoAdvanceTimer = null;
        if (_autoAdvanceInterval is not > 0 || _pager is null) return;
        _autoAdvanceTimer = new Timer(_ => Dispatcher.UIThread.Post(Advance), null, _autoAdvanceInterval.Value, _autoAdvanceInterval.Value);
    }

    private void Advance()
    {
        if (_pager is null || _adapter is null || _adapter.ItemCount < 2) return;
        var next = _pager.CurrentItem + 1;
        if (next >= _adapter.ItemCount)
        {
            if (!Loop) { _autoAdvanceTimer?.Dispose(); _autoAdvanceTimer = null; return; }
            next = 0;
        }
        _pager.SetCurrentItem(next, true);
    }

    private sealed class PageCallback : ViewPager2.OnPageChangeCallback
    {
        private readonly Action<int> _changed;
        public PageCallback(Action<int> changed) => _changed = changed;
        public override void OnPageSelected(int position) => _changed(position);
    }

    private sealed class ClickListener : Java.Lang.Object, View.IOnClickListener
    {
        private readonly Action _click;
        public ClickListener(Action click) => _click = click;
        public void OnClick(View? v) => _click();
    }

    private sealed class CarouselAdapter : global::AndroidX.RecyclerView.Widget.RecyclerView.Adapter
    {
        private readonly global::Android.Content.Context _context;
        public CarouselAdapter(global::Android.Content.Context context) => _context = context;
        public IReadOnlyList<string> Sources { get; set; } = Array.Empty<string>();
        public override int ItemCount => Sources.Count;
        public override global::AndroidX.RecyclerView.Widget.RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
            => new ImageHolder(new ImageView(parent.Context) { LayoutParameters = new ViewGroup.LayoutParams(-1, -1) });
        public override async void OnBindViewHolder(global::AndroidX.RecyclerView.Widget.RecyclerView.ViewHolder holder, int position)
        {
            if (holder is not ImageHolder image) return;
            var source = Sources[position];
            image.View.Tag = source;
            var bitmap = await AndroidCarouselSourceLoader.LoadAsync(_context, source);
            if (bitmap is not null && Equals(image.View.Tag, source)) image.View.SetImageBitmap(bitmap);
        }
        private sealed class ImageHolder : global::AndroidX.RecyclerView.Widget.RecyclerView.ViewHolder
        {
            public ImageView View { get; }
            public ImageHolder(ImageView view) : base(view) => View = view;
        }
    }
}

internal static class AndroidCarouselSourceLoader
{
    public static async Task<Bitmap?> LoadAsync(global::Android.Content.Context context, string source)
    {
        if (source.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = context.Assets?.Open(source["assets/".Length..]);
            return stream is null ? null : await Task.Run(() => BitmapFactory.DecodeStream(stream));
        }
        if (source.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            var comma = source.IndexOf(',');
            if (comma < 0) return null;
            var payload = source[(comma + 1)..];
            var bytes = source[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase)
                ? global::Android.Util.Base64.Decode(payload, global::Android.Util.Base64Flags.Default) ?? Array.Empty<byte>()
                : Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
            return await Task.Run(() => BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length));
        }
        if (global::System.Uri.TryCreate(source, global::System.UriKind.Absolute, out var uri))
        {
            if (uri.Scheme is "http" or "https")
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var bytes = await client.GetByteArrayAsync(uri);
                return await Task.Run(() => BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length));
            }
            if (uri.Scheme is "content" or "file")
            {
                var androidUri = global::Android.Net.Uri.Parse(uri.AbsoluteUri);
                if (androidUri is null) return null;
                using var stream = context.ContentResolver?.OpenInputStream(androidUri);
                return stream is null ? null : await Task.Run(() => BitmapFactory.DecodeStream(stream));
            }
        }
        if (global::System.IO.Path.IsPathRooted(source))
        {
            using var stream = File.OpenRead(source);
            return await Task.Run(() => BitmapFactory.DecodeStream(stream));
        }
        return null;
    }
}
