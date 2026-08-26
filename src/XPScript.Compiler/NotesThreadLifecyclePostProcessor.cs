using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XPScript.Compiler;

internal static class NotesThreadLifecyclePostProcessor
{
    public static string Apply(string source)
    {
        source = ReplaceRequired(source,
            "        _initialized = true;",
            "        _initialized = true;\n        MarkProcessInitializationThread();",
            "process-initialization-thread");

        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var rewritten = new ThreadScopeRewriter().Visit(root);
        return rewritten?.ToFullString() ?? source;
    }

    private sealed class ThreadScopeRewriter : CSharpSyntaxRewriter
    {
        private int _scopeIndex;

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var visited = (MethodDeclarationSyntax?)base.VisitMethodDeclaration(node) ?? node;
            if (visited.Body is null) return visited;
            if (visited.Parent is not ClassDeclarationSyntax parent || parent.Identifier.ValueText != "XPScriptNotesNativeApi")
                return visited;

            var name = visited.Identifier.ValueText;
            if (name is "Initialize" or "Terminate" or "Dispose" or "EnterNotesThread" or "ExitNotesThread" or "MarkProcessInitializationThread" or "ResolveRaw")
                return visited;

            var bodyText = visited.Body.ToString();
            var performsNativeWork =
                bodyText.Contains("EnsureInitialized()", StringComparison.Ordinal) ||
                bodyText.Contains("Resolve<", StringComparison.Ordinal) ||
                bodyText.Contains("TryResolve<", StringComparison.Ordinal);

            if (!performsNativeWork || bodyText.Contains("EnterNotesThread()", StringComparison.Ordinal))
                return visited;

            var statement = SyntaxFactory.ParseStatement(
                "using var __notesThreadScope" + _scopeIndex++ + " = EnterNotesThread();\n");
            return visited.WithBody(visited.Body.WithStatements(visited.Body.Statements.Insert(0, statement)));
        }
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string name)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new InvalidOperationException("Notes thread lifecycle source marker not found: " + name);
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
