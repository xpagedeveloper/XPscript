namespace LSLite.Compiler;

public static class JsonNodesSerializerShimSource
{
    public const string Code = """
namespace System.Text.Json.Nodes
{
    internal static class JsonSerializer
    {
        public static JsonNode? SerializeToNode(object value) => global::System.Text.Json.JsonSerializer.SerializeToNode(value);
    }
}
""";
}
