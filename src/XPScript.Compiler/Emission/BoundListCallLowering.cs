using System.Security.Cryptography;
using System.Text;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler.Emission;

/// <summary>Named wrappers allow expression calls to copy back without capturing ref parameters in lambdas.</summary>
internal static class BoundListCallLowering
{
    internal static bool IsElement(BoundExpression argument) => argument is BoundIndexExpression { Expression.SemanticType.IsList: true };
    internal static bool IsAlias(BoundExpression argument) => argument is BoundNameExpression { Symbol: LocalSymbol { IsListAlias: true } };
    internal static bool RequiresWrapper(BoundCallExpression call) => call.Target is null && call.Function.ReturnType != typeof(void) &&
        call.Arguments.Where((argument, index) => call.Function.ByRefParameters?[index] == true).Any(argument => IsElement(argument) || IsAlias(argument));

    internal static string Name(BoundCallExpression call)
    {
        var signature = call.Function.Name + "(" + string.Join(",", call.Arguments.Select((argument, index) =>
            (call.Function.SemanticParameterTypes?[index].Name ?? call.Function.ParameterTypes[index].FullName) + ":" + call.Function.ByRefParameters?[index] + ":" +
            (IsElement(argument) ? "element" : IsAlias(argument) ? "alias" : "value"))) + ")";
        return "__xpsListCall_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)))[..16];
    }

    internal static string EmitWrapper(BoundCallExpression call)
    {
        var parameters = new List<string>(); var arguments = new List<string>();
        var after = new List<string>();
        for (var i = 0; i < call.Arguments.Count; i++)
        {
            var argument = call.Arguments[i];
            var byRef = call.Function.ByRefParameters?[i] == true;
            var parameterType = call.Function.SemanticParameterTypes?[i];
            var type = parameterType?.IsList == true
                ? $"LSList<{BoundMethodEmitter.CSharpType(parameterType.ElementType!)}>"
                : BoundMethodEmitter.CSharpType(call.Function.ParameterTypes[i]);
            var name = "a" + i;
            if (byRef && (IsElement(argument) || IsAlias(argument)))
            {
                parameters.Add($"LSListByRef<{type}> {name}");
                after.Add($"{name}.CopyBack();");
                arguments.Add($"ref {name}.Value");
            }
            else
            {
                parameters.Add((byRef ? "ref " : "") + type + " " + name);
                arguments.Add((byRef ? "ref " : "") + name);
            }
        }
        return $"private static {BoundMethodEmitter.CSharpType(call.Function.ReturnType)} {Name(call)}({string.Join(", ", parameters)})\n{{\ntry {{ return {call.Function.Name}({string.Join(", ", arguments)}); }}\nfinally {{ {string.Join(" ", after)} }}\n}}\n";
    }

    internal static IEnumerable<BoundCallExpression> Calls(BoundNode node)
    {
        if (node is BoundCallExpression call && RequiresWrapper(call)) yield return call;
        foreach (var child in BoundEmissionContext.Children(node))
            foreach (var nested in Calls(child)) yield return nested;
    }
}
