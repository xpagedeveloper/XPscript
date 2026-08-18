using System.Text.Json;
using System.Text.Json.Serialization;

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
    IReadOnlyList<string> Options);

public sealed record DesktopFormRequest(
    string Title,
    int? Width,
    int? Height,
    bool Resizable,
    IReadOnlyList<DesktopFormField> Fields);

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
        return JsonSerializer.Deserialize<DesktopFormRequest>(json, JsonOptions)
            ?? throw new InvalidOperationException("Desktop UIForm request is empty.");
    }

    public static string SerializeResult(DesktopFormResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
