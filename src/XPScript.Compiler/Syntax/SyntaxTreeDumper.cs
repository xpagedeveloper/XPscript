using System.Text;

namespace XPScript.Compiler.Syntax;

/// <summary>
/// Produces a compact, deterministic representation of a syntax tree for
/// diagnostics and focused parser tests. Tokens are shown as leaves; optional
/// children are omitted.
/// </summary>
public static class SyntaxTreeDumper
{
    public static string Dump(SyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var builder = new StringBuilder();
        WriteNode(builder, node, 0);
        return builder.ToString();
    }

    private static void WriteNode(StringBuilder builder, SyntaxNode node, int depth)
    {
        builder.Append(' ', depth * 2)
            .Append(node.Kind)
            .Append(' ')
            .Append(node.Span)
            .AppendLine();

        foreach (var property in node.GetType().GetProperties())
        {
            if (property.Name is nameof(SyntaxNode.Kind) or nameof(SyntaxNode.Span))
                continue;

            var value = property.GetValue(node);
            if (value is SyntaxToken)
                continue;

            if (value is SyntaxNode child)
            {
                WriteNode(builder, child, depth + 1);
                continue;
            }

            if (value is IEnumerable<SyntaxNode> children)
            {
                foreach (var childNode in children)
                    WriteNode(builder, childNode, depth + 1);
            }
        }
    }
}
