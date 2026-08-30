namespace XPScript.Compiler;

internal sealed class UIFormDesktopReactivePostProcessor
{
    private const string MethodLookupSentinel = "types: [typeof(string), typeof(Func<string, string, string>)]";
    private const string InvokeSentinel = "new Func<string, string, string>(form.DispatchRegisteredEvent)";
    private const string ModernLookupSentinel = "ResolveShowDialog(Type type)";
    private const string ModernDispatchSentinel = "RegisterEventDispatcher(type, form);";
    private const string ModernCallbackSentinel = "form.DispatchRegisteredEvent(eventToken, submittedValue)";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        if (generated.Contains(ModernLookupSentinel, StringComparison.Ordinal) &&
            generated.Contains(ModernDispatchSentinel, StringComparison.Ordinal) &&
            generated.Contains(ModernCallbackSentinel, StringComparison.Ordinal))
            return generated;

        if (!generated.Contains(MethodLookupSentinel, StringComparison.Ordinal))
        {
            generated = ReplaceBetweenRequired(
                generated,
                "var method = type.GetMethod(\"ShowDialog\"",
                "?? throw new XPScriptRuntimeException(5, \"XPScript desktop UI bridge is incomplete.\");",
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
            generated = ReplaceBetweenRequired(
                generated,
                "resultJson = Convert.ToString(method.Invoke(null, [requestJson])",
                "?? string.Empty;",
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

    private static string ReplaceBetweenRequired(
        string source,
        string startToken,
        string endToken,
        string replacement,
        string stage)
    {
        var start = source.IndexOf(startToken, StringComparison.Ordinal);
        if (start < 0)
            throw new CompilerException($"Unable to install UIForm desktop event runtime ({stage}:start).");

        var end = source.IndexOf(endToken, start, StringComparison.Ordinal);
        if (end < 0)
            throw new CompilerException($"Unable to install UIForm desktop event runtime ({stage}:end).");
        end += endToken.Length;

        var lineStart = source.LastIndexOf('\n', Math.Max(0, start - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var indentation = source[lineStart..start];
        var formatted = string.Join("\n", replacement.Split('\n').Select((line, index) => index == 0 ? indentation + line : indentation + line));

        return source[..lineStart] + formatted + source[end..];
    }
}
