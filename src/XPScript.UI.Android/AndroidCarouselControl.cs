using Avalonia.Controls;
using Avalonia.Platform;
using Android.Views;
using Android.Widget;
using AndroidX.ViewPager2.Widget;

namespace XPScript.UI.Android;

/// <summary>Isolated Android ViewPager2 bridge for the shared UIForm Carousel.</summary>
public sealed class AndroidCarouselControl : NativeControlHost
{
    private ViewPager2? _pager;
    private CarouselAdapter? _adapter;
    public event EventHandler<int>? CurrentItemChanged;
    public IReadOnlyList<string> Sources
    {
        get => _adapter?.Sources ?? Array.Empty<string>();
        set { if (_adapter is null) return; _adapter.Sources = value?.ToArray() ?? Array.Empty<string>(); _adapter.NotifyDataSetChanged(); }
    }
    public bool Loop { get; set; }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _pager = new ViewPager2(global::Android.App.Application.Context!);
        _adapter = new CarouselAdapter();
        _pager.Adapter = _adapter;
        _pager.Orientation = ViewPager2.OrientationHorizontal;
        _pager.RegisterOnPageChangeCallback(new PageCallback(index => CurrentItemChanged?.Invoke(this, index)));
        return new PlatformHandle(_pager.Handle, "Android.View.View");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _pager?.Adapter = null;
        _adapter = null;
        _pager?.Dispose();
        _pager = null;
    }

    private sealed class PageCallback : ViewPager2.OnPageChangeCallback
    {
        private readonly Action<int> _changed;
        public PageCallback(Action<int> changed) => _changed = changed;
        public override void OnPageSelected(int position) => _changed(position);
    }

    private sealed class CarouselAdapter : global::AndroidX.RecyclerView.Widget.RecyclerView.Adapter
    {
        public IReadOnlyList<string> Sources { get; set; } = Array.Empty<string>();
        public override int ItemCount => Sources.Count;
        public override global::AndroidX.RecyclerView.Widget.RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
            => new ImageHolder(new ImageView(parent.Context) { LayoutParameters = new ViewGroup.LayoutParams(-1, -1) });
        public override void OnBindViewHolder(global::AndroidX.RecyclerView.Widget.RecyclerView.ViewHolder holder, int position)
        {
            if (holder is ImageHolder image) image.View.SetImageURI(global::Android.Net.Uri.Parse(Sources[position]));
        }
        private sealed class ImageHolder : global::AndroidX.RecyclerView.Widget.RecyclerView.ViewHolder
        {
            public ImageView View { get; }
            public ImageHolder(ImageView view) : base(view) => View = view;
        }
    }
}
