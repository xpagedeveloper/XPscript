namespace XPScript.Compiler;

public static class JsonNodesSerializerShimSource
{
    public static readonly string Code = """
namespace System.Text.Json.Nodes
{
    internal static class JsonSerializer
    {
        public static JsonNode? SerializeToNode(object value) => global::System.Text.Json.JsonSerializer.SerializeToNode(value);
    }
}
""" + "\n\n" + NativeXmlRuntimeSource.Code;
}
