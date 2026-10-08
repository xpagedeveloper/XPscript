using System.Text;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler.Emission;

/// <summary>A procedure whose signature and body have already been bound.</summary>
public sealed record BoundMethodDefinition(string Name, Type ReturnType,
    IReadOnlyList<ParameterSymbol> Parameters, IReadOnlyList<BoundStatement> Statements);

/// <summary>Emits a console compilation unit without parsing XPscript source.</summary>
public sealed class BoundCompilationUnitEmitter
{
    public string Emit(IReadOnlyList<BoundMethodDefinition> methods)
    {
        ArgumentNullException.ThrowIfNull(methods);
        var output = new StringBuilder("using System;\npublic static class Program\n{\n");
        var emitter = new BoundMethodEmitter();
        foreach (var method in methods)
            output.Append(emitter.Emit(method.Name, method.ReturnType, method.Parameters, method.Statements));
        return output.Append("}\n").ToString();
    }
}
