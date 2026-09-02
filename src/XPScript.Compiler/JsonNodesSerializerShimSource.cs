namespace XPScript.Compiler;

public static class JsonNodesSerializerShimSource
{
    public const string ShimCode = """
namespace System.Text.Json.Nodes
{
    internal static class JsonSerializer
    {
        public static JsonNode? SerializeToNode(object value) => global::System.Text.Json.JsonSerializer.SerializeToNode(value);
    }
}
""";

    public static readonly string Code = ShimCode + "\n\n" + NativeXmlRuntimeSource.Code + "\n\n" + NativeCsvRuntimeSource.Code;
}
