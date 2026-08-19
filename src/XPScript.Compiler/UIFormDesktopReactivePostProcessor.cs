namespace XPScript.Compiler;

internal sealed class UIFormDesktopReactivePostProcessor
{
    private const string MethodLookupSentinel = "types: [typeof(string), typeof(Func<string, string, string>)]";
    private const string InvokeSentinel = "new Func<string, string, string>(form.DispatchRegisteredEvent)";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        if (!generated.Contains(MethodLookupSentinel, StringComparison.Ordinal))
        {
            generated = ReplaceRequired(
                generated,
                """
        var method = type.GetMethod("ShowDialog", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new XPScriptRuntimeException(5, "XPScript desktop UI bridge is incomplete.");
""",
                """
        var method = type.GetMethod(
                "ShowDialog",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                binder: null,
                types: [typeof(string), typeof(Func<string, string, string>)],
                modifiers: null)
            ?? type.GetMethod("ShowDialog", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new XPScriptRuntimeException(5, "XPScript desktop UI bridge is incomplete.");
""",
                "method-lookup");
        }

        if (!generated.Contains(InvokeSentinel, StringComparison.Ordinal))
        {
            generated = ReplaceRequired(
                generated,
                """
            resultJson = Convert.ToString(method.Invoke(null, [requestJson]), System.Globalization.CultureInfo.InvariantCulture)
                ?? string.Empty;
""",
                """
            var invokeArgs = method.GetParameters().Length == 2
                ? new object?[] { requestJson, new Func<string, string, string>(form.DispatchRegisteredEvent) }
                : new object?[] { requestJson };
            resultJson = Convert.ToString(method.Invoke(null, invokeArgs), System.Globalization.CultureInfo.InvariantCulture)
                ?? string.Empty;
""",
                "invoke");
        }

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to install UIForm desktop event runtime ({stage}).");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
