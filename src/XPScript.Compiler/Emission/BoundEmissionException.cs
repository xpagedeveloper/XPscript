using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Emission;

internal sealed class BoundEmissionException(string message, TextSpan? span, Exception inner) : Exception(message, inner)
{
    public TextSpan? Span { get; } = span;
}
