namespace XPScript.Compiler;

internal static class UIExtensionRuntimeSource
{
    public const string Code = """
internal static class XPScriptUI
{
    private static readonly List<XPScriptUIForm> Forms = [];
    internal static bool SingleVisibleFormMode { get; set; }

    private static XPScriptUIForm Register(XPScriptUIForm form)
    {
        Forms.Add(form);
        return form;
    }

    internal static void Activate(XPScriptUIForm form)
    {
        if (SingleVisibleFormMode)
            foreach (var candidate in Forms) candidate.Visible = false;
        Forms.Remove(form);
        Forms.Add(form);
        form.Visible = true;
    }

    internal static void Deactivate(XPScriptUIForm form)
    {
        form.Visible = false;
        Forms.Remove(form);
        if (SingleVisibleFormMode && Forms.Count > 0) Forms[^1].Visible = true;
    }

    public static XPScriptUIForm CreateForm()
        => Register(new(string.Empty, null, null, true));

    public static XPScriptUIForm CreateForm(object? title)
        => Register(new(XPScriptRuntime.CStr(title), null, null, true));

    public static XPScriptUIForm CreateForm(object? title, object? width, object? height)
        => Register(new(XPScriptRuntime.CStr(title), ToOptionalPositiveInt(width, "width"), ToOptionalPositiveInt(height, "height"), true));

    public static XPScriptUIForm CreateForm(object? title, object? width, object? height, object? resizable)
        => Register(new(XPScriptRuntime.CStr(title), ToOptionalPositiveInt(width, "width"), ToOptionalPositiveInt(height, "height"), Convert.ToBoolean(resizable, System.Globalization.CultureInfo.CurrentCulture)));

    private static int? ToOptionalPositiveInt(object? value, string name)
    {
        if (value is null) return null;
        int converted;
        try
        {
            converted = Convert.ToInt32(value, System.Globalization.CultureInfo.CurrentCulture);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new XPScriptRuntimeException(13, $"UIForm {name} must be an Integer value.");
        }

        if (converted <= 0)
            throw new XPScriptRuntimeException(5, $"UIForm {name} must be greater than zero.");
        return converted;
    }
}

internal abstract class XPScriptUIComponent
{
    private string _id = string.Empty;
    private XPScriptUIForm? _owner;
    private readonly List<XPScriptUIComponent> _children = [];

    protected XPScriptUIComponent(XPScriptUIForm? owner = null, string type = "UIComponent")
    {
        _owner = owner;
        Type = type;
    }

    public string Id
    {
        get => _id;
        set
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.Length > 128)
                throw new XPScriptRuntimeException(5, "UIForm component Id must contain at most 128 characters.");
            if (normalized.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.')))
                throw new XPScriptRuntimeException(5, "UIForm component Id contains unsupported characters.");
            _owner?.SetComponentId(this, normalized);
            _id = normalized;
        }
    }

    public virtual string Type { get; }
    public XPScriptUIComponent? Parent { get; internal set; }
    public XPScriptUIComponent[] Children => _children.ToArray();

    public void AddChild(object? component)
    {
        if (component is not XPScriptUIComponent child)
            throw new XPScriptRuntimeException(13, "UIForm AddChild requires a UI component.");
        if (ReferenceEquals(child, this))
            throw new XPScriptRuntimeException(5, "A UIForm component cannot be its own child.");
        if (child is XPScriptUIForm || _owner is null || child._owner is null || !ReferenceEquals(_owner, child._owner))
            throw new XPScriptRuntimeException(5, "UIForm child components must belong to the same UIForm.");
        for (var ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
            if (ReferenceEquals(ancestor, child))
                throw new XPScriptRuntimeException(5, "UIForm component hierarchy cannot contain cycles.");
        if (child.Parent is XPScriptUIComponent oldParent)
            oldParent._children.Remove(child);
        child.Parent = this;
        if (!_children.Contains(child)) _children.Add(child);
    }

    internal IReadOnlyList<XPScriptUIComponent> ChildComponents => _children;

    internal void AttachOwner(XPScriptUIForm owner) => _owner = owner;
    internal XPScriptUIForm? Owner => _owner;
}

internal sealed class XPScriptUIField : XPScriptUIComponent
{
    private readonly XPScriptUIForm _owner;
    private readonly string _controlType;
    private string _webViewSource = "about:blank";
    private string _webViewHtml = string.Empty;
    private string _webViewUserAgent = string.Empty;
    private string _webViewBackground = string.Empty;
    private string _mediaSource = string.Empty;
    private long _mediaPosition;
    private long _mediaDuration;
    private double _mediaVolume = 1.0;
    private bool _mediaAutoPlay;
    private bool _mediaLoop;
    private bool _mediaMuted;
    private double _mediaPlaybackRate = 1.0;
    private bool _mediaIsPlaying;
    private double _progressValue;
    private double _rangeValue;
    private bool _progressIndeterminate;
    private bool _activityRunning;
    private double _step = 1;

    internal XPScriptUIField(XPScriptUIForm owner, string name, string label, string type)
        : base(owner, type)
    {
        _owner = owner;
        Name = name;
        Label = label;
        _controlType = type;
    }

    public string Name { get; }
    public string Label { get; set; }
    public override string Type => _controlType;
    public List<string> CarouselSources { get; } = [];
    public int CarouselIndex { get; set; }
    public bool CarouselLoop { get; set; }
    public int? CarouselAutoAdvanceMilliseconds { get; set; }
    public bool Required { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public decimal? Minimum { get; set; }
    public decimal? Maximum { get; set; }
    public double Step { get { EnsureSlider(); return _step; } set { EnsureSlider(); if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0) throw new XPScriptRuntimeException(5, "UIForm Slider Step must be greater than zero."); _step = value; } }
    public List<string> Options { get; } = [];
    public object? SelectedValue
    {
        get => _owner.GetFieldValue(Name);
        set => _owner.SetFieldValue(Name, value);
    }

    public void AddOption(object? value)
    {
        if (_controlType is not ("Select" or "RadioGroup" or "ListBox" or "ListView" or "MultiListBox" or "MultiSelect" or "CheckBoxGroup" or "LookupField" or "AutoCompleteField"))
            throw new XPScriptRuntimeException(5, "Options are not supported for this UIForm control.");
        var option = XPScriptRuntime.CStr(value);
        if (option.Length is < 1 or > 256)
            throw new XPScriptRuntimeException(5, "UIForm option must contain between 1 and 256 characters.");
        if (!Options.Contains(option, StringComparer.Ordinal)) Options.Add(option);
    }

    public void ClearOptions()
    {
        if (_controlType is not ("Select" or "RadioGroup" or "ListBox" or "ListView" or "MultiListBox" or "MultiSelect" or "CheckBoxGroup" or "LookupField" or "AutoCompleteField"))
            throw new XPScriptRuntimeException(5, "Options are not supported for this UIForm control.");
        Options.Clear();
    }
    public bool Focusable { get; set; }
    public bool IsTabStop { get; set; }
    public string RegexPattern { get; set; } = string.Empty;
    public string ImageSource { get; set; } = string.Empty;
    public string ImageAltText { get; set; } = string.Empty;
    public string ImageCertificateValidation { get; set; } = "Strict";
    public int LayoutRow { get; set; }
    public int LayoutColumn { get; set; }
    public int ColumnSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public string RegionId { get; set; } = string.Empty;
    public string RefreshTargetRegion { get; set; } = string.Empty;
    public string RefreshHandler { get; set; } = string.Empty;
    public string OnChangeHandler { get; set; } = string.Empty;
    public bool Visible { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    private double _cornerRadius;
    private double? _width;
    private double? _height;
    private string _font = string.Empty;
    private double? _fontSize;
    public double CornerRadius
    {
        get => _cornerRadius;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1000)
                throw new XPScriptRuntimeException(5, "UIForm control CornerRadius must be between 0 and 1000.");
            _cornerRadius = value;
        }
    }
    public double? Width { get => _width; set => _width = NormalizeOptionalDimension(value, "Width"); }
    public double? Height { get => _height; set => _height = NormalizeOptionalDimension(value, "Height"); }
    public string Font { get => _font; set => _font = value ?? string.Empty; }
    public double? FontSize { get => _fontSize; set => _fontSize = NormalizeOptionalDimension(value, "FontSize"); }
    public string Placeholder { get; set; } = string.Empty;
    public string Tooltip { get; set; } = string.Empty;

    private static double? NormalizeOptionalDimension(double? value, string name)
    {
        if (!value.HasValue) return null;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value <= 0 || value.Value > 10000)
            throw new XPScriptRuntimeException(5, $"UIForm control {name} must be greater than 0 and no more than 10000.");
        return value;
    }
    public string TabName { get; set; } = string.Empty;
    public string GridName { get; set; } = string.Empty;
    public string IconName { get; set; } = string.Empty;

    public string Source
    {
        get
        {
            if (Type == "WebView") return _owner.Visible ? WebViewCommand("source", null) : _webViewSource;
            if (Type is "Video" or "Audio") return _mediaSource;
            throw new XPScriptRuntimeException(5, "Source is only supported for WebView, Video and Audio fields.");
        }
        set
        {
            if (Type is "Video" or "Audio")
            {
                _mediaSource = NormalizeMediaSource(value);
                _mediaPosition = 0;
                _mediaDuration = 0;
                _mediaIsPlaying = false;
                if (_owner.Visible) TryDesktopMediaCommand("source", _mediaSource);
                return;
            }
            EnsureWebView();
            _webViewSource = NormalizeWebViewUrl(value);
            _webViewHtml = string.Empty;
            if (_owner.Visible) _ = WebViewCommand("navigate", _webViewSource);
        }
    }
    public long Position
    {
        get { EnsureMedia(); return ReadDesktopMediaLong("position", _mediaPosition); }
        set { EnsureMedia(); _mediaPosition = Math.Max(0, value); TryDesktopMediaCommand("position", _mediaPosition.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
    }
    public long Duration { get { EnsureMedia(); return ReadDesktopMediaLong("duration", _mediaDuration); } internal set { EnsureMedia(); _mediaDuration = Math.Max(0, value); } }
    public double Volume
    {
        get { EnsureMedia(); return _mediaVolume; }
        set { EnsureMedia(); if (value < 0 || value > 1) throw new XPScriptRuntimeException(5, "UIForm media Volume must be between 0 and 1."); _mediaVolume = value; TryDesktopMediaCommand("volume", value.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
    }
    public bool AutoPlay { get { EnsureMedia(); return _mediaAutoPlay; } set { EnsureMedia(); _mediaAutoPlay = value; TryDesktopMediaCommand("autoplay", value ? "true" : "false"); } }
    public bool Loop { get { EnsureMedia(); return _mediaLoop; } set { EnsureMedia(); _mediaLoop = value; TryDesktopMediaCommand("loop", value ? "true" : "false"); } }
    public bool Muted { get { EnsureMedia(); return _mediaMuted; } set { EnsureMedia(); _mediaMuted = value; TryDesktopMediaCommand("muted", value ? "true" : "false"); } }
    public double PlaybackRate
    {
        get { EnsureMedia(); return _mediaPlaybackRate; }
        set { EnsureMedia(); if (value <= 0) throw new XPScriptRuntimeException(5, "UIForm media PlaybackRate must be greater than zero."); _mediaPlaybackRate = value; TryDesktopMediaCommand("rate", value.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
    }
    public bool IsPlaying { get { EnsureMedia(); return ReadDesktopMediaBool("playing", _mediaIsPlaying); } }
    public void Play() { EnsureMedia(); _mediaIsPlaying = true; TryDesktopMediaCommand("play", null); }
    public void Pause() { EnsureMedia(); _mediaIsPlaying = false; TryDesktopMediaCommand("pause", null); }
    public double Value
    {
        get
        {
            if (string.Equals(_controlType, "RangeField", StringComparison.Ordinal)) return _rangeValue;
            EnsureProgress();
            return _progressValue;
        }
        set
        {
            if (string.Equals(_controlType, "RangeField", StringComparison.Ordinal))
            {
                EnsureSlider();
                if (double.IsNaN(value) || double.IsInfinity(value)) throw new XPScriptRuntimeException(5, "UIForm Slider Value must be finite.");
                if (Minimum.HasValue && value < (double)Minimum.Value || Maximum.HasValue && value > (double)Maximum.Value)
                    throw new XPScriptRuntimeException(5, "UIForm Slider Value is outside its configured range.");
                _rangeValue = value;
                return;
            }
            EnsureProgress();
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1)
                throw new XPScriptRuntimeException(5, "UIForm ProgressBar Value must be between 0 and 1.");
            _progressValue = value;
        }
    }
    public bool IsIndeterminate
    {
        get => Type == "ProgressBar" ? _progressIndeterminate : EnsureActivityIndicatorValue();
        set { EnsureProgress(); _progressIndeterminate = value; }
    }
    public bool IsRunning
    {
        get { EnsureActivityIndicator(); return _activityRunning; }
        set { EnsureActivityIndicator(); _activityRunning = value; }
    }
    public string Html
    {
        get { EnsureWebView(); return _webViewHtml; }
        set { EnsureWebView(); _webViewHtml = value ?? string.Empty; if (_owner.Visible) _ = WebViewCommand("html", _webViewHtml); }
    }
    public string UserAgent
    {
        get => Type == "WebView" && _owner.Visible ? WebViewCommand("useragent:get", null) : _webViewUserAgent;
        set { EnsureWebView(); _webViewUserAgent = value ?? string.Empty; if (_owner.Visible) _ = WebViewCommand("useragent:set", _webViewUserAgent); }
    }
    public string Background
    {
        get => Type == "WebView" && _owner.Visible ? WebViewCommand("background:get", null) : _webViewBackground;
        set { EnsureWebView(); _webViewBackground = value ?? string.Empty; if (_owner.Visible) _ = WebViewCommand("background:set", _webViewBackground); }
    }
    public bool CanGoBack => Type == "WebView" && _owner.Visible && WebViewCommand("cangoback", null).Equals("true", StringComparison.OrdinalIgnoreCase);
    public bool CanGoForward => Type == "WebView" && _owner.Visible && WebViewCommand("cangoforward", null).Equals("true", StringComparison.OrdinalIgnoreCase);
    public string AdapterInfo => Type == "WebView" && _owner.Visible ? WebViewCommand("adapterinfo", null) : string.Empty;
    public string PlatformHandle => Type == "WebView" && _owner.Visible ? WebViewCommand("platformhandle", null) : string.Empty;

    public void Navigate(object? url) { EnsureWebView(); _webViewSource = NormalizeWebViewUrl(XPScriptRuntime.CStr(url)); _webViewHtml = string.Empty; if (_owner.Visible) _ = WebViewCommand("navigate", _webViewSource); }
    public void NavigateToString(object? html) { EnsureWebView(); _webViewHtml = XPScriptRuntime.CStr(html); if (_owner.Visible) _ = WebViewCommand("html", _webViewHtml); }
    public string InvokeScript(object? script) { EnsureWebView(); return WebViewCommand("script", XPScriptRuntime.CStr(script)); }
    public bool GoBack() { EnsureWebView(); return WebViewCommand("back", null).Equals("true", StringComparison.OrdinalIgnoreCase); }
    public bool GoForward() { EnsureWebView(); return WebViewCommand("forward", null).Equals("true", StringComparison.OrdinalIgnoreCase); }
    public bool Refresh() { EnsureWebView(); return WebViewCommand("refresh", null).Equals("true", StringComparison.OrdinalIgnoreCase); }
    public bool Stop()
    {
        if (Type is "Video" or "Audio")
        {
            _mediaIsPlaying = false;
            _mediaPosition = 0;
            TryDesktopMediaCommand("stop", null);
            return true;
        }
        EnsureWebView();
        return WebViewCommand("stop", null).Equals("true", StringComparison.OrdinalIgnoreCase);
    }
    public void ShowPrintUI() { EnsureWebView(); _ = WebViewCommand("print", null); }
    public void PrintToPdf(object? path) { EnsureWebView(); _ = WebViewCommand("pdf", XPScriptRuntime.CStr(path)); }
    public void Copy() { EnsureWebView(); _ = WebViewCommand("copy", null); }
    public void Cut() { EnsureWebView(); _ = WebViewCommand("cut", null); }
    public void Paste() { EnsureWebView(); _ = WebViewCommand("paste", null); }
    public void SelectAll() { EnsureWebView(); _ = WebViewCommand("selectall", null); }
    public void Undo() { EnsureWebView(); _ = WebViewCommand("undo", null); }
    public void Redo() { EnsureWebView(); _ = WebViewCommand("redo", null); }
    public string GetCookies() { EnsureWebView(); return WebViewCommand("cookies:get", null); }
    public void SetCookie(object? name, object? value, object? domain, object? path) { EnsureWebView(); _ = WebViewCommand("cookies:set", System.Text.Json.JsonSerializer.Serialize(new { name = XPScriptRuntime.CStr(name), value = XPScriptRuntime.CStr(value), domain = XPScriptRuntime.CStr(domain), path = XPScriptRuntime.CStr(path) })); }
    public void DeleteCookie(object? name, object? domain, object? path) { EnsureWebView(); _ = WebViewCommand("cookies:delete", System.Text.Json.JsonSerializer.Serialize(new { name = XPScriptRuntime.CStr(name), domain = XPScriptRuntime.CStr(domain), path = XPScriptRuntime.CStr(path) })); }
    public void ClearCookies() { EnsureWebView(); _ = WebViewCommand("cookies:clear", null); }

    internal string WebViewSource => _webViewSource;
    internal string WebViewHtml => _webViewHtml;
    internal string WebViewUserAgent => _webViewUserAgent;
    internal string WebViewBackground => _webViewBackground;

    private string WebViewCommand(string command, string? argument)
    {
        if (!_owner.Visible) throw new XPScriptRuntimeException(5, "UIForm WebView live functions require the form to be visible.");
        return XPScriptUIDesktopAdapter.WebViewCommand(_owner.InstanceId, Name, command, argument);
    }
    private void EnsureWebView() { if (Type != "WebView") throw new XPScriptRuntimeException(5, "This UIForm field is not a WebView."); }
    private void EnsureMedia() { if (Type is not ("Video" or "Audio")) throw new XPScriptRuntimeException(5, "This UIForm field is not a media field."); }
    private void TryDesktopMediaCommand(string command, string? argument)
    {
        if (Type is not ("Video" or "Audio") || !_owner.Visible) return;
        try
        {
            var host = System.Type.GetType("XPScript.UI.Desktop.DesktopFormHost, XPScript.UI.Desktop", false);
            var method = host?.GetMethod("MediaCommand", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, [_owner.InstanceId, Name, command, argument]);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new XPScriptRuntimeException(5, "UIForm desktop media command failed: " + ex.InnerException.Message);
        }
    }
    private long ReadDesktopMediaLong(string command, long fallback)
    {
        var value = TryDesktopMediaQuery(command);
        return long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? Math.Max(0, parsed) : fallback;
    }
    private bool ReadDesktopMediaBool(string command, bool fallback)
        => bool.TryParse(TryDesktopMediaQuery(command), out var parsed) ? parsed : fallback;
    private string TryDesktopMediaQuery(string command)
    {
        if (Type is not ("Video" or "Audio") || !_owner.Visible) return string.Empty;
        try
        {
            var host = System.Type.GetType("XPScript.UI.Desktop.DesktopFormHost, XPScript.UI.Desktop", false);
            var method = host?.GetMethod("MediaCommand", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            return Convert.ToString(method?.Invoke(null, [_owner.InstanceId, Name, command, null]), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new XPScriptRuntimeException(5, "UIForm desktop media query failed: " + ex.InnerException.Message);
        }
    }
    private void EnsureProgress() { if (!string.Equals(_controlType, "ProgressBar", StringComparison.Ordinal)) throw new XPScriptRuntimeException(5, "This UIForm field is not a ProgressBar."); }
    private bool EnsureActivityIndicatorValue() { EnsureActivityIndicator(); return true; }
    private void EnsureActivityIndicator() { if (!string.Equals(_controlType, "ActivityIndicator", StringComparison.Ordinal)) throw new XPScriptRuntimeException(5, "This UIForm field is not an ActivityIndicator."); }
    private void EnsureSlider() { if (!string.Equals(_controlType, "RangeField", StringComparison.Ordinal)) throw new XPScriptRuntimeException(5, "This UIForm field is not a Slider."); }
    private static string NormalizeMediaSource(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0) return string.Empty;
        if (!Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out var uri))
            throw new XPScriptRuntimeException(5, "UIForm media Source is invalid.");
        if (uri.IsAbsoluteUri)
        {
            if (uri.Scheme is not ("http" or "https" or "file" or "content" or "android.resource"))
                throw new XPScriptRuntimeException(5, "UIForm media Source uses an unsupported URI scheme.");
            return uri.AbsoluteUri;
        }
        var normalized = text.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
            throw new XPScriptRuntimeException(5, "UIForm media relative Source must stay within the application asset root.");
        return normalized.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) ? normalized : "assets/" + normalized;
    }

    private static string NormalizeWebViewUrl(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0) return "about:blank";
        if (!Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out var uri)) throw new XPScriptRuntimeException(5, "UIForm WebView Source is invalid.");
        if (uri.IsAbsoluteUri)
        {
            if (uri.Scheme is not ("http" or "https" or "file" or "about" or "data")) throw new XPScriptRuntimeException(5, "UIForm WebView Source uses an unsupported URI scheme.");
            return uri.AbsoluteUri;
        }
        var normalized = text.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
            throw new XPScriptRuntimeException(5, "UIForm WebView relative Source must stay within the application asset root.");
        return normalized.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) ? normalized : "assets/" + normalized;
    }

    private static string NormalizeMediaSource(object? value, string kind)
        => NormalizeMediaSource(XPScriptRuntime.CStr(value));
}

internal sealed partial class XPScriptUIForm : XPScriptUIComponent
{
    private string _title;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private bool _visible;
    private bool _modal;
    private int? _width;
    private int? _height;
    private bool _resizable;
    private string _bootText = string.Empty;
    private string _bootImage = string.Empty;
    private XPScriptJsonObject _data = XPScriptNativeJson.CreateObject();
    private readonly List<XPScriptUIField> _fields = [];
    private readonly List<XPScriptUIComponent> _components = [];
    private readonly List<(string Name, string Command)> _mediaCommands = [];
    private int _gridColumns = 1;
    private double _cornerRadius;
    private double? _defaultControlWidth;
    private double? _defaultControlHeight;
    private string _defaultFont = string.Empty;
    private double? _defaultFontSize;

    internal XPScriptUIForm(string title, int? width, int? height, bool resizable)
        : base(null, "UIForm")
    {
        AttachOwner(this);
        _title = title;
        _width = width;
        _height = height;
        _resizable = resizable;
    }

    public string Title { get => _title; set => _title = value ?? string.Empty; }
    public int Width { get => _width ?? 0; set { if (value <= 0) throw new XPScriptRuntimeException(5, "UIForm width must be greater than zero."); _width = value; } }
    public int Height { get => _height ?? 0; set { if (value <= 0) throw new XPScriptRuntimeException(5, "UIForm height must be greater than zero."); _height = value; } }
    public bool Resizable { get => _resizable; set => _resizable = value; }
    public double CornerRadius
    {
        get => _cornerRadius;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1000)
                throw new XPScriptRuntimeException(5, "UIForm CornerRadius must be between 0 and 1000.");
            _cornerRadius = value;
            foreach (var item in _fields) item.CornerRadius = value;
        }
    }
    public double? DefaultControlWidth { get => _defaultControlWidth; set { _defaultControlWidth = NormalizeOptionalDimension(value, "DefaultControlWidth"); ApplyDefaults(); } }
    public double? DefaultControlHeight { get => _defaultControlHeight; set { _defaultControlHeight = NormalizeOptionalDimension(value, "DefaultControlHeight"); ApplyDefaults(); } }
    public string DefaultFont { get => _defaultFont; set { _defaultFont = value ?? string.Empty; ApplyDefaults(); } }
    public double? DefaultFontSize { get => _defaultFontSize; set { _defaultFontSize = NormalizeOptionalDimension(value, "DefaultFontSize"); ApplyDefaults(); } }
    public bool ShowDefaultButtons { get; set; } = true;
    public double? DefaultButtonCornerRadius { get; private set; }
    public bool Visible { get => _visible; set => _visible = value; }
    public bool Modal => _modal;
    internal string InstanceId => _instanceId;
    public string BootText { get => _bootText; set => _bootText = value ?? string.Empty; }
    public string BootImage
    {
        get => _bootImage;
        set
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length == 0) { _bootImage = string.Empty; return; }
            if (!Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out var uri)) throw new XPScriptRuntimeException(5, "UIForm BootImage source is invalid.");
            if (uri.IsAbsoluteUri) { _bootImage = uri.AbsoluteUri; return; }
            var normalized = text.Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
                throw new XPScriptRuntimeException(5, "UIForm BootImage relative source must stay within the application asset root.");
            _bootImage = normalized.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) ? normalized : "assets/" + normalized;
        }
    }
    public bool HasExplicitSize => _width.HasValue || _height.HasValue;
    public object Data => _data;
    public int FieldCount => _fields.Count;
    public XPScriptUIComponent[] Components => _components.ToArray();
    public XPScriptUIComponent? GetComponentById(object? id)
    {
        var value = XPScriptRuntime.CStr(id).Trim();
        if (value.Length == 0) return null;
        return this.Id.Equals(value, StringComparison.OrdinalIgnoreCase)
            ? this
            : _components.FirstOrDefault(component => component.Id.Equals(value, StringComparison.OrdinalIgnoreCase));
    }
    public XPScriptUIComponent[] GetComponentsByType(object? type)
    {
        var value = XPScriptRuntime.CStr(type).Trim();
        return _components.Where(component => component.Type.Equals(value, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
    public int GridColumns => _gridColumns;
    public void BindData(object? value)
    {
        _data = value switch
        {
            XPScriptJsonObject obj => obj,
            XPScriptJsonDocument document when document.Root.AsObject() is XPScriptJsonObject obj => obj,
            null => XPScriptNativeJson.CreateObject(),
            _ => throw new XPScriptRuntimeException(13, "UIForm.BindData requires a JsonObject or a JsonDocument with an object root.")
        };
    }

    public XPScriptUIField AddTextField(object? name) => AddField(name, name, "TextField");
    public XPScriptUIField AddTextField(object? name, object? label) => AddField(name, label, "TextField");
    public XPScriptUIField AddTextArea(object? name) => AddField(name, name, "TextArea");
    public XPScriptUIField AddTextArea(object? name, object? label) => AddField(name, label, "TextArea");
    public XPScriptUIField AddNumberField(object? name) => AddField(name, name, "NumberField");
    public XPScriptUIField AddNumberField(object? name, object? label) => AddField(name, label, "NumberField");
    public XPScriptUIField AddRangeField(object? name) => AddField(name, name, "RangeField");
    public XPScriptUIField AddRangeField(object? name, object? label) => AddField(name, label, "RangeField");
    public XPScriptUIField AddSlider(object? name) => AddField(name, name, "RangeField");
    public XPScriptUIField AddSlider(object? name, object? label) => AddField(name, label, "RangeField");
    public XPScriptUIField AddCheckBox(object? name) => AddField(name, name, "CheckBox");
    public XPScriptUIField AddCheckBox(object? name, object? label) => AddField(name, label, "CheckBox");
    public XPScriptUIField AddSwitch(object? name) => AddField(name, name, "Switch");
    public XPScriptUIField AddSwitch(object? name, object? label) => AddField(name, label, "Switch");
    public XPScriptUIField AddIcon(object? name, object? icon)
    {
        var field = AddField(name, string.Empty, "Icon");
        field.IconName = XPScriptRuntime.CStr(icon).Trim();
        return field;
    }
    public XPScriptUIField AddIcon(object? name, object? icon, object? label)
    {
        var field = AddField(name, label, "Icon");
        field.IconName = XPScriptRuntime.CStr(icon).Trim();
        return field;
    }
    public XPScriptUIField AddCard(object? name) => AddField(name, name, "Card");
    public XPScriptUIField AddCard(object? name, object? label) => AddField(name, label, "Card");
    public XPScriptUIField AddPanel(object? name) => AddField(name, name, "Panel");
    public XPScriptUIField AddPanel(object? name, object? label) => AddField(name, label, "Panel");
    public XPScriptUIField AddScrollView(object? name) => AddField(name, name, "ScrollView");
    public XPScriptUIField AddScrollView(object? name, object? label) => AddField(name, label, "ScrollView");
    public XPScriptUIField AddDateField(object? name) => AddField(name, name, "DateField");
    public XPScriptUIField AddDateField(object? name, object? label) => AddField(name, label, "DateField");
    public XPScriptUIField AddTimeField(object? name) => AddField(name, name, "TimeField");
    public XPScriptUIField AddTimeField(object? name, object? label) => AddField(name, label, "TimeField");
    public XPScriptUIField AddDateTimeField(object? name) => AddField(name, name, "DateTimeField");
    public XPScriptUIField AddDateTimeField(object? name, object? label) => AddField(name, label, "DateTimeField");
    public XPScriptUIField AddMonthField(object? name) => AddField(name, name, "MonthField");
    public XPScriptUIField AddMonthField(object? name, object? label) => AddField(name, label, "MonthField");
    public XPScriptUIField AddColorField(object? name) => AddField(name, name, "ColorField");
    public XPScriptUIField AddColorField(object? name, object? label) => AddField(name, label, "ColorField");
    public XPScriptUIField AddEmailField(object? name) => AddField(name, name, "EmailField");
    public XPScriptUIField AddEmailField(object? name, object? label) => AddField(name, label, "EmailField");
    public XPScriptUIField AddUrlField(object? name) => AddField(name, name, "UrlField");
    public XPScriptUIField AddUrlField(object? name, object? label) => AddField(name, label, "UrlField");
    public XPScriptUIField AddPasswordField(object? name) => AddField(name, name, "PasswordField");
    public XPScriptUIField AddPasswordField(object? name, object? label) => AddField(name, label, "PasswordField");
    public XPScriptUIField AddSelect(object? name) => AddField(name, name, "Select");
    public XPScriptUIField AddSelect(object? name, object? label) => AddField(name, label, "Select");
    public XPScriptUIField AddListBox(object? name) => AddField(name, name, "ListBox");
    public XPScriptUIField AddListBox(object? name, object? label) => AddField(name, label, "ListBox");
    public XPScriptUIField AddListView(object? name) => AddField(name, name, "ListView");
    public XPScriptUIField AddListView(object? name, object? label) => AddField(name, label, "ListView");
    public XPScriptUIField AddMultiListBox(object? name) => AddField(name, name, "MultiListBox");
    public XPScriptUIField AddMultiListBox(object? name, object? label) => AddField(name, label, "MultiListBox");
    public XPScriptUIField AddRadioGroup(object? name) => AddField(name, name, "RadioGroup");
    public XPScriptUIField AddRadioGroup(object? name, object? label) => AddField(name, label, "RadioGroup");
    public XPScriptUIField AddHiddenField(object? name) => AddField(name, string.Empty, "HiddenField");
    public XPScriptUIField AddWebView(object? name) => AddField(name, string.Empty, "WebView");
    public XPScriptUIField AddVideo(object? name) => AddField(name, name, "Video");
    public XPScriptUIField AddVideo(object? name, object? label) => AddField(name, label, "Video");
    public XPScriptUIField AddCarousel(object? name, params object?[] sources)
    {
        var field = AddField(name, name, "Carousel");
        foreach (var source in sources ?? []) field.CarouselSources.Add(NormalizeMediaSource(source, "carousel"));
        return field;
    }
    public void SetCarouselIndex(object? name, object? index)
    {
        var field = FindField(name);
        if (field.Type != "Carousel") throw new XPScriptRuntimeException(5, "UIForm.SetCarouselIndex requires a Carousel field.");
        var value = Convert.ToInt32(index, System.Globalization.CultureInfo.InvariantCulture);
        if (value < 0 || value >= field.CarouselSources.Count) throw new XPScriptRuntimeException(5, "Carousel index is outside the available item range.");
        field.CarouselIndex = value;
    }
    public void SetCarouselLoop(object? name, object? loop)
    {
        var field = FindField(name);
        if (field.Type != "Carousel") throw new XPScriptRuntimeException(5, "UIForm.SetCarouselLoop requires a Carousel field.");
        field.CarouselLoop = Convert.ToBoolean(loop, System.Globalization.CultureInfo.InvariantCulture);
    }
    public void SetCarouselAutoAdvance(object? name, object? milliseconds)
    {
        var field = FindField(name);
        if (field.Type != "Carousel") throw new XPScriptRuntimeException(5, "UIForm.SetCarouselAutoAdvance requires a Carousel field.");
        var value = Convert.ToInt32(milliseconds, System.Globalization.CultureInfo.InvariantCulture);
        if (value < 0) throw new XPScriptRuntimeException(5, "Carousel auto-advance interval cannot be negative.");
        field.CarouselAutoAdvanceMilliseconds = value == 0 ? null : value;
    }
    private static string NormalizeMediaSource(object? value, string kind)
        => XPScriptRuntime.CStr(value).Trim();
    public XPScriptUIField AddAudio(object? name) => AddField(name, name, "Audio");
    public XPScriptUIField AddAudio(object? name, object? label) => AddField(name, label, "Audio");
    public XPScriptUIField AddProgressBar(object? name) => AddField(name, string.Empty, "ProgressBar");
    public XPScriptUIField AddProgressBar(object? name, object? label) => AddField(name, label, "ProgressBar");
    public XPScriptUIField AddActivityIndicator(object? name) => AddField(name, string.Empty, "ActivityIndicator");
    public XPScriptUIField AddActivityIndicator(object? name, object? label) => AddField(name, label, "ActivityIndicator");

    public void PlayMedia(object? name) => QueueMediaCommand(name, "play");
    public void PauseMedia(object? name) => QueueMediaCommand(name, "pause");
    public void StopMedia(object? name) => QueueMediaCommand(name, "stop");

    private void QueueMediaCommand(object? nameValue, string command)
    {
        var field = FindField(nameValue);
        if (field.Type is not ("Video" or "Audio"))
            throw new XPScriptRuntimeException(5, "UIForm media commands require an Audio or Video field.");
        _mediaCommands.Add((field.Name, command));
    }
    public XPScriptUIField AddWebView(object? name, object? label) => AddField(name, label, "WebView");
    public XPScriptUIField AddCameraPreview(object? name) => AddField(name, name, "CameraPreview");
    public XPScriptUIField AddCameraPreview(object? name, object? label) => AddField(name, label, "CameraPreview");

    public void AddOption(object? name, object? value)
    {
        var field = FindField(name);
        field.AddOption(value);
    }

    public void SetRequired(object? name, object? required)
    {
        var field = FindField(name);
        if (field.Type == "HiddenField")
            throw new XPScriptRuntimeException(5, "UIForm HiddenField cannot be marked required.");
        field.Required = Convert.ToBoolean(required, System.Globalization.CultureInfo.CurrentCulture);
    }

    public void SetLength(object? name, object? minLength, object? maxLength)
    {
        var field = FindField(name);
        if (field.Type is not ("TextField" or "TextArea" or "PasswordField" or "EmailField" or "UrlField"))
            throw new XPScriptRuntimeException(5, "UIForm length validation is only supported for text fields.");
        int min;
        int max;
        try
        {
            min = Convert.ToInt32(minLength, System.Globalization.CultureInfo.CurrentCulture);
            max = Convert.ToInt32(maxLength, System.Globalization.CultureInfo.CurrentCulture);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new XPScriptRuntimeException(13, "UIForm text length limits must be Integer values.");
        }
        if (min < 0 || max < min)
            throw new XPScriptRuntimeException(5, "UIForm text length range is invalid.");
        field.MinLength = min;
        field.MaxLength = max;
    }

    public void SetNumberRange(object? name, object? minimum, object? maximum)
    {
        var field = FindField(name);
        if (field.Type is not ("NumberField" or "RangeField"))
            throw new XPScriptRuntimeException(5, "UIForm numeric range is only supported for NumberField and RangeField.");
        decimal min;
        decimal max;
        try
        {
            min = Convert.ToDecimal(minimum, System.Globalization.CultureInfo.InvariantCulture);
            max = Convert.ToDecimal(maximum, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new XPScriptRuntimeException(13, "UIForm numeric range limits must be numeric values.");
        }
        if (max < min)
            throw new XPScriptRuntimeException(5, "UIForm numeric range is invalid.");
        field.Minimum = min;
        field.Maximum = max;
    }

    public void SetSliderStep(object? name, object? step)
    {
        var field = FindField(name);
        field.Step = Convert.ToDouble(step, System.Globalization.CultureInfo.InvariantCulture);
    }

    public object? GetFieldValue(object? name)
    {
        var fieldName = NormalizeFieldName(name);
        return _data.Contains(fieldName) ? _data.Get(fieldName) ?? string.Empty : string.Empty;
    }

    public string GetFieldValueString(object? name) => XPScriptRuntime.CStr(GetFieldValue(name));

    public object GetFieldValues(object? name)
    {
        var values = ReadSelectedValues(NormalizeFieldName(name));
        var result = XPScriptNativeJson.CreateArray();
        foreach (var value in values) result.Add(value);
        return result;
    }

    public void SetFieldValue(object? name, object? value)
    {
        var fieldName = NormalizeFieldName(name);
        var exists = _data.Contains(fieldName);
        var isEmptyText = value is null || XPScriptRuntime.CStr(value).Length == 0;
        if (!exists && isEmptyText) return;
        _data.Set(fieldName, value ?? string.Empty);
    }

    public string ShowDialog()
    {
        if (!XPScriptUIWebAdapter.IsAvailable)
            throw new XPScriptRuntimeException(5, "UIForm.ShowDialog requires a configured desktop UI backend or an active XPScript web request.");
        if (XPScriptUIWebAdapter.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var field in _fields)
            {
                if (field.Type == "MultiListBox") ApplySubmittedValues(field, XPScriptUIWebAdapter.FormValues(field.Name));
                else ApplySubmittedValue(field, XPScriptUIWebAdapter.FormFirst(field.Name));
            }
            return "OK";
        }
        XPScriptUIWebAdapter.WriteHtml(RenderWebForm());
        return "Pending";
    }

    private XPScriptUIField AddField(object? name, object? label, string type)
    {
        var fieldName = NormalizeFieldName(name);
        if (_fields.Any(field => field.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase)))
            throw new XPScriptRuntimeException(5, $"UIForm field '{fieldName}' already exists.");
        var field = new XPScriptUIField(this, fieldName, XPScriptRuntime.CStr(label), type);
        field.CornerRadius = _cornerRadius;
        ApplyDefaults(field);
        _fields.Add(field);
        RegisterComponent(field);
        return field;
    }

    internal void RegisterComponent(XPScriptUIComponent component, XPScriptUIComponent? parent = null)
    {
        component.AttachOwner(this);
        component.Parent = parent ?? this;
        _components.Add(component);
        if (component.Id.Length > 0) SetComponentId(component, component.Id);
    }

    internal void SetComponentId(XPScriptUIComponent component, string id)
    {
        if (id.Length > 0 && _components.Any(other => !ReferenceEquals(other, component) && other.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ||
            id.Length > 0 && !ReferenceEquals(component, this) && Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            throw new XPScriptRuntimeException(5, $"UIForm component Id '{id}' is already in use.");
    }

    private void ApplyDefaults()
    {
        foreach (var item in _fields) ApplyDefaults(item);
        foreach (var button in _buttons)
        {
            if (_defaultControlWidth.HasValue) button.Width = _defaultControlWidth;
            if (_defaultControlHeight.HasValue) button.Height = _defaultControlHeight;
            if (_defaultFont.Length > 0) button.Font = _defaultFont;
            if (_defaultFontSize.HasValue) button.FontSize = _defaultFontSize;
            button.CornerRadius = _cornerRadius;
        }
    }

    private void ApplyDefaults(XPScriptUIField field)
    {
        field.CornerRadius = _cornerRadius;
        if (_defaultControlWidth.HasValue) field.Width = _defaultControlWidth;
        if (_defaultControlHeight.HasValue) field.Height = _defaultControlHeight;
        if (_defaultFont.Length > 0) field.Font = _defaultFont;
        if (_defaultFontSize.HasValue) field.FontSize = _defaultFontSize;
    }

    private static double? NormalizeOptionalDimension(double? value, string name)
    {
        if (!value.HasValue) return null;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value <= 0 || value.Value > 10000)
            throw new XPScriptRuntimeException(5, $"UIForm control {name} must be greater than zero and at most 10000.");
        return value;
    }

    private XPScriptUIField FindField(object? name)
    {
        var fieldName = NormalizeFieldName(name);
        return _fields.FirstOrDefault(field => field.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
            ?? throw new XPScriptRuntimeException(5, $"UIForm field '{fieldName}' does not exist.");
    }

    private IReadOnlyList<string> ReadSelectedValues(string fieldName)
    {
        if (!_data.Contains(fieldName)) return Array.Empty<string>();
        var value = _data.Get(fieldName);
        if (value is XPScriptJsonArray array)
        {
            var values = new List<string>(array.Count);
            for (var i = 0; i < array.Count; i++)
            {
                var item = XPScriptRuntime.CStr(array.Get(i));
                if (item.Length > 0 && !values.Contains(item, StringComparer.Ordinal)) values.Add(item);
            }
            return values;
        }
        var scalar = XPScriptRuntime.CStr(value);
        return scalar.Length == 0 ? Array.Empty<string>() : new[] { scalar };
    }

    private void ApplySubmittedValues(XPScriptUIField field, IReadOnlyList<string> submitted)
    {
        if (field.Type != "MultiListBox")
            throw new XPScriptRuntimeException(5, "UIForm multi-value submission is only supported for MultiListBox fields.");

        var selected = submitted
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (field.Required && selected.Length == 0)
            throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' is required.");
        foreach (var value in selected)
        {
            if (!field.Options.Contains(value, StringComparer.Ordinal))
                throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' contains an unsupported option.");
        }

        if (selected.Length == 0 && !_data.Contains(field.Name)) return;
        var array = XPScriptNativeJson.CreateArray();
        foreach (var value in selected) array.Add(value);
        _data.Set(field.Name, array);
    }

    private void ApplySubmittedValue(XPScriptUIField field, string submitted)
    {
        var exists = _data.Contains(field.Name);
        if (field.Type == "PasswordField" && submitted.Length == 0 && exists)
            return;
        if (field.Required && submitted.Length == 0)
            throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' is required.");
        if (field.Type is "TextField" or "TextArea" or "PasswordField" or "EmailField" or "UrlField")
        {
            if (field.MinLength.HasValue && submitted.Length < field.MinLength.Value)
                throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' must contain at least {field.MinLength.Value} characters.");
            if (field.MaxLength.HasValue && submitted.Length > field.MaxLength.Value)
                throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' must contain at most {field.MaxLength.Value} characters.");
        }

        switch (field.Type)
        {
            case "NumberField":
            case "RangeField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!decimal.TryParse(submitted, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var number))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid number.");
                if (field.Minimum.HasValue && number < field.Minimum.Value)
                    throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' must be at least {field.Minimum.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
                if (field.Maximum.HasValue && number > field.Maximum.Value)
                    throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' must be at most {field.Maximum.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
                _data.Set(field.Name, number);
                return;
            case "CheckBox":
            case "Switch":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, false); return; }
                _data.Set(field.Name, submitted.Equals("1", StringComparison.OrdinalIgnoreCase) || submitted.Equals("true", StringComparison.OrdinalIgnoreCase) || submitted.Equals("on", StringComparison.OrdinalIgnoreCase));
                return;
            case "DateField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!DateTime.TryParseExact(submitted, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid date in yyyy-MM-dd format.");
                _data.Set(field.Name, submitted);
                return;
            case "TimeField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!TimeOnly.TryParseExact(submitted, new[] { "HH:mm", "HH:mm:ss" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid time in HH:mm or HH:mm:ss format.");
                _data.Set(field.Name, submitted);
                return;
            case "DateTimeField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!DateTime.TryParseExact(submitted, new[] { "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid local date/time in yyyy-MM-ddTHH:mm or yyyy-MM-ddTHH:mm:ss format.");
                _data.Set(field.Name, submitted);
                return;
            case "MonthField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!DateTime.TryParseExact(submitted, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid month in yyyy-MM format.");
                _data.Set(field.Name, submitted);
                return;
            case "ColorField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!System.Text.RegularExpressions.Regex.IsMatch(submitted, "^#[0-9A-Fa-f]{6}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a color in #RRGGBB format.");
                _data.Set(field.Name, submitted.ToLowerInvariant());
                return;
            case "EmailField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                try
                {
                    var address = new System.Net.Mail.MailAddress(submitted);
                    if (!address.Address.Equals(submitted, StringComparison.OrdinalIgnoreCase)) throw new FormatException();
                }
                catch (FormatException)
                {
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid email address.");
                }
                _data.Set(field.Name, submitted);
                return;
            case "UrlField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!Uri.TryCreate(submitted, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain an absolute HTTP or HTTPS URL.");
                _data.Set(field.Name, submitted);
                return;
            case "Select":
            case "ListBox":
            case "RadioGroup":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!field.Options.Contains(submitted, StringComparer.Ordinal))
                    throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' contains an unsupported option.");
                _data.Set(field.Name, submitted);
                return;
            default:
                if (!exists && submitted.Length == 0) return;
                _data.Set(field.Name, submitted);
                return;
        }
    }

    private string RenderWebForm()
    {
        var html = new System.Text.StringBuilder();
        html.Append("<form method=\"post\" class=\"xpscript-uiform\">");
        if (_bootImage.Length > 0)
            html.Append("<img class=\"xpscript-uiform-boot-image\" src=\"").Append(System.Net.WebUtility.HtmlEncode(_bootImage)).Append("\" alt=\"\" aria-hidden=\"true\">");
        if (_bootText.Length > 0)
            html.Append("<div class=\"xpscript-uiform-boot-text\">").Append(System.Net.WebUtility.HtmlEncode(_bootText)).Append("</div>");
        if (_title.Length > 0) html.Append("<h1>").Append(System.Net.WebUtility.HtmlEncode(_title)).Append("</h1>");
        foreach (var field in _fields)
        {
            var name = System.Net.WebUtility.HtmlEncode(field.Name);
            var value = field.Type == "MultiListBox" ? string.Empty : System.Net.WebUtility.HtmlEncode(GetFieldValueString(field.Name));
            if (field.Type == "HiddenField")
            {
                html.Append("<input type=\"hidden\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\">");
                continue;
            }

            var label = System.Net.WebUtility.HtmlEncode(field.Label);
            var required = field.Required ? " required" : string.Empty;
            var controlStyle = BuildWebControlStyle(field);
            var length = (field.MinLength.HasValue ? $" minlength=\"{field.MinLength.Value}\"" : string.Empty)
                + (field.MaxLength.HasValue ? $" maxlength=\"{field.MaxLength.Value}\"" : string.Empty);
            var range = (field.Minimum.HasValue ? $" min=\"{field.Minimum.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"" : string.Empty)
                + (field.Maximum.HasValue ? $" max=\"{field.Maximum.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"" : string.Empty);
            html.Append("<div class=\"xpscript-uiform-field\"").Append(controlStyle).Append(">").Append("<label for=\"xps_").Append(name).Append("\">").Append(label).Append("</label>");
            switch (field.Type)
            {
                case "TextArea": html.Append("<textarea id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\"").Append(controlStyle).Append(required).Append(length).Append(">").Append(value).Append("</textarea>"); break;
                case "NumberField": html.Append("<input type=\"number\" step=\"any\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append(range).Append(">"); break;
                case "RangeField": html.Append("<input type=\"range\" step=\"any\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append(range).Append(">"); break;
                case "CheckBox":
                case "Switch":
                    var checkedValue = GetFieldValue(field.Name) is bool b && b;
                    html.Append("<input type=\"checkbox\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"1\"");
                    if (checkedValue) html.Append(" checked");
                    html.Append(required).Append(">");
                    break;
                case "DateField": html.Append("<input type=\"date\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append(">"); break;
                case "TimeField": html.Append("<input type=\"time\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append(">"); break;
                case "DateTimeField": html.Append("<input type=\"datetime-local\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append(">"); break;
                case "MonthField": html.Append("<input type=\"month\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append(">"); break;
                case "ColorField": html.Append("<input type=\"color\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append(">"); break;
                case "EmailField": html.Append("<input type=\"email\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append(length).Append(">"); break;
                case "UrlField": html.Append("<input type=\"url\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append(length).Append(">"); break;
                case "PasswordField": html.Append("<input type=\"password\" autocomplete=\"new-password\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\"").Append(required).Append(length).Append(">"); break;
                case "Select":
                case "ListBox":
                    html.Append("<select id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\"");
                    if (field.Type == "ListBox") html.Append(" size=\"6\"");
                    html.Append(required).Append(">");
                    if (field.Type == "Select" && !field.Required) html.Append("<option value=\"\"></option>");
                    foreach (var option in field.Options)
                    {
                        var encodedOption = System.Net.WebUtility.HtmlEncode(option);
                        html.Append("<option value=\"").Append(encodedOption).Append("\"");
                        if (option == GetFieldValueString(field.Name)) html.Append(" selected");
                        html.Append(">").Append(encodedOption).Append("</option>");
                    }
                    html.Append("</select>");
                    break;
                case "MultiListBox":
                    var selectedValues = ReadSelectedValues(field.Name);
                    html.Append("<select multiple size=\"6\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\"").Append(required).Append(">");
                    foreach (var option in field.Options)
                    {
                        var encodedOption = System.Net.WebUtility.HtmlEncode(option);
                        html.Append("<option value=\"").Append(encodedOption).Append("\"");
                        if (selectedValues.Contains(option, StringComparer.Ordinal)) html.Append(" selected");
                        html.Append(">").Append(encodedOption).Append("</option>");
                    }
                    html.Append("</select>");
                    break;
                case "RadioGroup":
                    foreach (var option in field.Options)
                    {
                        var encodedOption = System.Net.WebUtility.HtmlEncode(option);
                        html.Append("<label><input type=\"radio\" name=\"").Append(name).Append("\" value=\"").Append(encodedOption).Append("\"");
                        if (option == GetFieldValueString(field.Name)) html.Append(" checked");
                        html.Append(required).Append(">").Append(encodedOption).Append("</label>");
                    }
                    break;
                default: html.Append("<input type=\"text\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(controlStyle).Append(required).Append(length).Append(">"); break;
            }
            html.Append("</div>");
        }
        html.Append("<button type=\"submit\" name=\"__xps_uiform_submit\" value=\"1\">OK</button></form>");
        return html.ToString();
    }

    private static string BuildWebControlStyle(XPScriptUIField field)
    {
        var css = new System.Text.StringBuilder();
        if (field.CornerRadius > 0) css.Append("border-radius:").Append(field.CornerRadius.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("px;");
        if (field.Width is > 0) css.Append("width:").Append(field.Width.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("px;");
        if (field.Height is > 0) css.Append("height:").Append(field.Height.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("px;");
        if (field.FontSize is > 0) css.Append("font-size:").Append(field.FontSize.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("px;");
        if (field.Font.Length > 0) css.Append("font-family:").Append(field.Font.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)).Append(";");
        return css.Length == 0 ? string.Empty : " style=\"" + css + "\"";
    }

    private static string NormalizeFieldName(object? value)
    {
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length is < 1 or > 128) throw new XPScriptRuntimeException(5, "UIForm field name must contain between 1 and 128 characters.");
        if (name.Any(c => !(char.IsLetterOrDigit(c) || c is '_' or '-' or '.'))) throw new XPScriptRuntimeException(5, "UIForm field name contains unsupported characters.");
        return name;
    }
}

internal static class XPScriptUIWebAdapter
{
    private const string BridgeTypeName = "XPScript.Web.Runtime.XpsUIWebRuntimeBridge, XPScript.Web.Runtime";
    private static Type? BridgeType => Type.GetType(BridgeTypeName, throwOnError: false, ignoreCase: false);
    public static bool IsAvailable
    {
        get
        {
            var type = BridgeType;
            if (type is null) return false;
            var method = type.GetMethod("IsAvailable", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            return method is not null && method.Invoke(null, null) is true;
        }
    }
    public static string Method => InvokeString("Method");
    public static string FormFirst(string name) => InvokeString("FormFirst", name);
    public static string[] FormValues(string name)
    {
        var type = BridgeType ?? throw new XPScriptRuntimeException(5, "XPScript web UI bridge is unavailable.");
        var method = type.GetMethod("FormValues", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new XPScriptRuntimeException(5, "XPScript web UI bridge is incomplete.");
        return method.Invoke(null, [name]) as string[] ?? Array.Empty<string>();
    }
    public static void WriteHtml(string html)
    {
        var type = BridgeType ?? throw new XPScriptRuntimeException(5, "XPScript web UI bridge is unavailable.");
        var method = type.GetMethod("WriteHtml", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new XPScriptRuntimeException(5, "XPScript web UI bridge is incomplete.");
        method.Invoke(null, [html]);
    }
    private static string InvokeString(string methodName, params object?[] args)
    {
        var type = BridgeType ?? throw new XPScriptRuntimeException(5, "XPScript web UI bridge is unavailable.");
        var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new XPScriptRuntimeException(5, "XPScript web UI bridge is incomplete.");
        return Convert.ToString(method.Invoke(null, args), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
""";
}
