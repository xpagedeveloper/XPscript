namespace XPScript.Compiler;

internal sealed class UIFormDesktopReactivePostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        generated = ReplaceRequired(generated,
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
""");

        generated = ReplaceRequired(generated,
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
""");

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIForm desktop event runtime.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
