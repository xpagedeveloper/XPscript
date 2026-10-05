using System.Text;
using XPScript.Compiler.Binding;
using XPScript.Compiler.Syntax;
using SymbolDisplay = Microsoft.CodeAnalysis.CSharp.SymbolDisplay;

namespace XPScript.Compiler.Emission;

internal sealed class BoundEmissionContext(IReadOnlyList<BoundStatement> statements, string? source = null, string? sourcePath = null)
{
    private readonly StringBuilder _output = new();
    private readonly List<GeneratedSourceMapping> _mappings = [];
    private readonly HashSet<string> _names = statements.SelectMany(Names).ToHashSet(StringComparer.OrdinalIgnoreCase);
    private int _nextTemporary;
    private int _line = 1;

    public string Temporary()
    {
        string name;
        do { name = "__xpsEmit" + _nextTemporary++; } while (!_names.Add(name));
        return name;
    }

    public void Write(string text, int indent, TextSpan? span = null)
    {
        if (source is not null && sourcePath is not null && span is { } location)
        {
            var position = SourceTextMap.GetPosition(source, location.Start);
            _output.Append("#line ").Append(position.Line).Append(' ').Append(SymbolDisplay.FormatLiteral(sourcePath, true)).Append('\n');
            _line++;
            _mappings.Add(new GeneratedSourceMapping(_line, sourcePath, location, position));
        }
        else if (source is not null)
        {
            _output.Append("#line hidden\n");
            _line++;
        }
        _output.Append(' ', indent * 4).Append(text).Append('\n');
        _line++;
    }

    public BoundEmissionResult Finish()
    {
        if (source is not null)
            _output.Append("#line default\n");
        return new BoundEmissionResult(_output.ToString(), _mappings.ToArray());
    }

    private static IEnumerable<string> Names(BoundNode node)
    {
        if (node is BoundNameExpression name)
            yield return name.Symbol.Name;
        if (node is BoundCallExpression call)
            yield return call.Function.Name;
        foreach (var child in Children(node))
            foreach (var identifier in Names(child))
                yield return identifier;
    }

    private static IEnumerable<BoundNode> Children(BoundNode node) => node switch
    {
        BoundAssignmentStatement value => [value.Target, value.Expression],
        BoundVariableDeclarationStatement value => value.Initializer is null ? [] : [value.Initializer],
        BoundExpressionStatement value => [value.Expression],
        BoundPrintStatement value => [value.Expression],
        BoundReturnStatement value => value.Expression is null ? [] : [value.Expression],
        BoundIfStatement value => new BoundNode[] { value.Condition }.Concat(value.ThenStatements).Concat(value.ElseIfClauses.SelectMany(clause => new BoundNode[] { clause.Condition }.Concat(clause.Statements))).Concat(value.ElseStatements),
        BoundForStatement value => new BoundNode[] { value.Variable, value.FromExpression, value.ToExpression }.Concat(value.StepExpression is null ? [] : [value.StepExpression]).Concat(value.Statements),
        BoundForAllStatement value => new BoundNode[] { value.Variable, value.Collection }.Concat(value.Statements),
        BoundWhileStatement value => new BoundNode[] { value.Condition }.Concat(value.Statements),
        BoundDoStatement value => (value.Condition is null ? Enumerable.Empty<BoundNode>() : [value.Condition]).Concat(value.Statements),
        BoundSelectStatement value => new BoundNode[] { value.Expression }.Concat(value.Cases.SelectMany(clause => (clause.LowerExpression is null ? Enumerable.Empty<BoundNode>() : [clause.LowerExpression]).Concat(clause.UpperExpression is null ? [] : [clause.UpperExpression]).Concat(clause.Statements))),
        BoundConversionExpression value => [value.Expression],
        BoundUnaryExpression value => [value.Operand],
        BoundBinaryExpression value => [value.Left, value.Right],
        BoundMemberAccessExpression value => [value.Receiver],
        BoundIndexExpression value => [value.Expression, value.Index],
        BoundCallExpression value => (value.Target is null ? Enumerable.Empty<BoundNode>() : [value.Target]).Concat(value.Arguments),
        BoundNewExpression value => value.Arguments,
        BoundIndexedPropertyExpression value => new BoundNode[] { value.Receiver }.Concat(value.Arguments),
        _ => []
    };
}
