using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Styling;

namespace XPScript.UI.Desktop;

public sealed record DesktopFormField(
    string Name,
    string Label,
    string Type,
    bool Required,
    string? Value,
    int? MinLength,
    int? MaxLength,
    decimal? Minimum,
    decimal? Maximum,
    IReadOnlyList<string> Options)
{
    public int LayoutRow { get; init; }
    public int LayoutColumn { get; init; }
    public int ColumnSpan { get; init; } = 1;
    public int RowSpan { get; init; } = 1;
    public string RegionId { get; init; } = string.Empty;
    public string RefreshTargetRegion { get; init; } = string.Empty;
    public string RefreshHandler { get; init; } = string.Empty;
    public string OnChangeHandler { get; init; } = string.Empty;
    public bool Visible { get; init; } = true;
    public bool Enabled { get; init; } = true;
    public bool ReadOnly { get; init; }
    public string Placeholder { get; init; } = string.Empty;
    public string Tooltip { get; init; } = string.Empty;
    public string RegexPattern { get; init; } = string.Empty;
    public string DateMinimum { get; init; } = string.Empty;
    public string DateMaximum { get; init; } = string.Empty;
    public string TimeMinimum { get; init; } = string.Empty;
    public string TimeMaximum { get; init; } = string.Empty;
    public string DateTimeMinimum { get; init; } = string.Empty;
    public string DateTimeMaximum { get; init; } = string.Empty;
    public string MonthMinimum { get; init; } = string.Empty;
    public string MonthMaximum { get; init; } = string.Empty;
    public IReadOnlyList<string> Values { get; init; } = Array.Empty<string>();
}

public sealed record DesktopFormButton(
    string Name,
    string Label,
    string Style,
    int LayoutRow,
    int LayoutColumn,
    int ColumnSpan,
    int RowSpan,
    bool Visible,
    bool Enabled);

public sealed record DesktopFormRequest(
    string Title,
    int? Width,
    int? Height,
    bool Resizable,
    IReadOnlyList<DesktopFormField> Fields)
{
    public string Theme { get; init; } = "System";
    public bool ShowValidationErrors { get; init; } = true;
    public int GridColumns { get; init; } = 1;
    public IReadOnlyList<DesktopFormButton> Buttons { get; init; } = Array.Empty<DesktopFormButton>();
    public string ApplicationTitle { get; init; } = string.Empty;
    public string ApplicationIcon { get; init; } = string.Empty;
}

public sealed record DesktopFormResult(
    string Result,
    IReadOnlyDictionary<string, JsonElement> Values);

public static class XpsUIDesktopRuntimeBridge
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static bool IsSupportedPlatform() =>
        OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    public static DesktopFormRequest ParseRequest(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var request = JsonSerializer.Deserialize<DesktopFormRequest>(json, JsonOptions)
            ?? throw new InvalidOperationException("Desktop UIForm request is empty.");

        ApplyTheme(request.Theme);

        var fields = request.Fields.Select(field => field.Type switch
        {
            "Separator" => field with
            {
                Type = "RadioGroup",
                Label = "────────────────────────────────────────",
                Required = false,
                ReadOnly = true,
                Options = Array.Empty<string>()
            },
            "Spacer" => field with
            {
                Type = "RadioGroup",
                Label = string.Empty,
                Required = false,
                ReadOnly = true,
                Options = Array.Empty<string>()
            },
            _ => field
        }).ToArray();

        return request with { Fields = fields };
    }

    private static void ApplyTheme(string theme)
    {
        if (!IsSupportedPlatform()) return;

        var variant = theme.Trim().ToLowerInvariant() switch
        {
            "" or "system" => ThemeVariant.Default,
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => throw new InvalidOperationException("Desktop UIForm theme must be System, Light, or Dark.")
        };

        if (Application.Current is null)
        {
            AppBuilder.Configure<XpsDesktopApplication>()
                .UsePlatformDetect()
                .SetupWithoutStarting();
        }

        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = variant;
    }

    public static string SerializeResult(DesktopFormResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
