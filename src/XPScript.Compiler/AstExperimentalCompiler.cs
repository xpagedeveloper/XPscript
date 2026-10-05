using System.Text;
using XPScript.Compiler.Binding;
using XPScript.Compiler.Emission;
using XPScript.Compiler.Syntax;

namespace XPScript.Compiler;

/// <summary>Experimental CLI bridge for the currently supported AST compilation slice.</summary>
internal static class AstExperimentalCompiler
{
    public static async Task<string> CompileAsync(string sourcePath, string outputDirectory, CancellationToken cancellationToken = default)
    {
        var source = await File.ReadAllTextAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var declaration = new DeclarationParser(source).ParseDeclaration();
        if (declaration is not SubDeclarationSyntax sub || !sub.Identifier.Text.Equals("Main", StringComparison.OrdinalIgnoreCase))
            throw new CompilerException("AST experimental compilation currently requires a top-level Sub Main().", "XPS3001", "ast");

        var symbols = new SymbolTable();
        symbols.Declare(new FunctionSymbol("AstPrint", typeof(void), []));
        var binder = new StatementBinder(symbols);
        var bound = sub.Statements.Select(binder.Bind).OfType<BoundStatement>().ToArray();
        if (binder.Diagnostics.Count > 0)
            throw new CompilerException(string.Join(Environment.NewLine, binder.Diagnostics.Select(d => d.Message)), binder.Diagnostics[0].Code, "semantic");

        var body = new BoundMethodEmitter().Emit("Main", typeof(void), bound);
        var generated = $$"""
using System;
internal static class Program
{
    public static void AstPrint() => Console.WriteLine("AST_XPS_COMPILE_OK");
{{body}}
}
""";
        return await RunRoslynCompiler.CompileAsync(generated, outputDirectory, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
