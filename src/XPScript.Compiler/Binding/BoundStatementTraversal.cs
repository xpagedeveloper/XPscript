namespace XPScript.Compiler.Binding;

/// <summary>Walks statement bodies and clause bodies without reflection.</summary>
internal static class BoundStatementTraversal
{
    public static IEnumerable<BoundStatement> Descendants(IEnumerable<BoundStatement> statements)
    {
        foreach (var statement in statements)
        {
            yield return statement;
            IEnumerable<BoundStatement> children = statement switch
            {
                BoundIfStatement value => value.ThenStatements.Concat(value.ElseIfClauses.SelectMany(clause => clause.Statements)).Concat(value.ElseStatements),
                BoundWhileStatement value => value.Statements,
                BoundDoStatement value => value.Statements,
                BoundForStatement value => value.Statements,
                BoundForAllStatement value => value.Statements,
                BoundSelectStatement value => value.Cases.SelectMany(clause => clause.Statements),
                _ => []
            };
            foreach (var child in Descendants(children)) yield return child;
        }
    }
}
