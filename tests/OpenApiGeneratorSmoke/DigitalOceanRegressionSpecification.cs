using YamlDotNet.RepresentationModel;

internal static class DigitalOceanRegressionSpecification
{
    internal static void AssertModelReferenceClosure(string source)
    {
        var declared = System.Text.RegularExpressions.Regex.Matches(source,
            @"(?m)^(?:Public Class|Enum) ([A-Za-z_]\w*)\s*$")
            .Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        declared.UnionWith(new[] { "Variant", "String", "Boolean", "Integer", "Long", "Single", "Double", "Date",
            "XPJsonObject", "XPJsonArray", "XPHttpClient", "XPHttpResponse", "XPJsonDocument", "XPJsonValidationResult" });
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(source,
            @"(?m)^\s*Public [A-Za-z_]\w*(?:\(\))? As ([A-Za-z_]\w*)\s*$"))
            if (!declared.Contains(match.Groups[1].Value))
                throw new Exception("DigitalOcean model field references an undeclared type: " + match.Value.Trim());
    }

    internal static string Create(string specification)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(specification));
        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        var paths = (YamlMappingNode)root.Children[new YamlScalarNode("paths")];
        var selectedPaths = new YamlMappingNode();
        foreach (var path in new[] { "/v2/action-gateway/actors/{actor_id}/limits:clear", "/v2/action-gateway/connections", "/v2/apps" })
        {
            var original = (YamlMappingNode)paths.Children[new YamlScalarNode(path)];
            var selected = new YamlMappingNode();
            if (original.Children.TryGetValue(new YamlScalarNode("parameters"), out var parameters))
                selected.Add("parameters", parameters);
            selected.Add("post", original.Children[new YamlScalarNode("post")]);
            selectedPaths.Add(path, selected);
        }

        var result = new YamlMappingNode();
        foreach (var key in new[] { "openapi", "info", "servers", "security" })
            if (root.Children.TryGetValue(new YamlScalarNode(key), out var value)) result.Add(key, value);
        result.Add("paths", selectedPaths);

        var components = new YamlMappingNode();
        var originalComponents = (YamlMappingNode)root.Children[new YamlScalarNode("components")];
        if (originalComponents.Children.TryGetValue(new YamlScalarNode("securitySchemes"), out var security))
            components.Add("securitySchemes", security);
        var pending = new Queue<string>();
        CollectReferences(selectedPaths, pending);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (pending.TryDequeue(out var reference))
        {
            if (!visited.Add(reference)) continue;
            var segments = reference.Split('/');
            if (segments.Length != 4 || segments[0] != "#" || segments[1] != "components")
                throw new Exception("Unexpected DigitalOcean regression reference: " + reference);
            var sectionName = segments[2];
            var name = segments[3].Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            var originalSection = (YamlMappingNode)originalComponents.Children[new YamlScalarNode(sectionName)];
            var node = originalSection.Children[new YamlScalarNode(name)];
            if (!components.Children.TryGetValue(new YamlScalarNode(sectionName), out var section))
            {
                section = new YamlMappingNode();
                components.Add(sectionName, section);
            }
            ((YamlMappingNode)section).Add(name, node);
            CollectReferences(node, pending);
        }
        result.Add("components", components);
        using var writer = new StringWriter();
        new YamlStream(new YamlDocument(result)).Save(writer, assignAnchors: false);
        return writer.ToString();
    }

    private static void CollectReferences(YamlNode node, Queue<string> pending)
    {
        if (node is YamlMappingNode mapping)
            foreach (var pair in mapping.Children)
            {
                if (pair.Key is YamlScalarNode { Value: "$ref" } && pair.Value is YamlScalarNode reference)
                    pending.Enqueue(reference.Value ?? throw new Exception("Empty DigitalOcean reference."));
                else CollectReferences(pair.Value, pending);
            }
        else if (node is YamlSequenceNode sequence)
            foreach (var child in sequence.Children) CollectReferences(child, pending);
    }
}
