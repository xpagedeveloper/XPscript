using System.Text.Json.Nodes;

namespace XPScript.Web.Compiler;

// Allocate declarations and reference names together. A schema's wire name is
// case-sensitive, while its generated XPscript identifier is case-insensitive.
internal sealed class XpsOpenApiModelCatalog
{
    internal XpsOpenApiGenerationPlan Plan { get; private set; } = null!;
    internal Dictionary<string, JsonObject> Models { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, string> TypeNames { get; } = new(StringComparer.Ordinal);

    internal static XpsOpenApiModelCatalog Collect(JsonObject root, Func<string, HashSet<string>, string> allocateName)
    {
        var plan = XpsOpenApiGenerationPlan.Build(root, allocateName);
        var catalog = new XpsOpenApiModelCatalog { Plan = plan };
        foreach (var pair in plan.Schemas)
        {
            catalog.Models.Add(pair.Key, pair.Value.Schema);
            catalog.TypeNames.Add(pair.Value.WireName, pair.Key);
        }
        return catalog;
    }

    private XpsOpenApiModelCatalog() { }
}
