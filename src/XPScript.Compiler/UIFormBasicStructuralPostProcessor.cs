namespace XPScript.Compiler;

internal static class UIFormBasicStructuralPostProcessor
{
    public static string Transform(string generated)
    {
        if (generated.Contains("public XPScriptUIField AddSeparator(object? name)", StringComparison.Ordinal))
            return generated;

        const string marker = "    public void AddOption(object? name, object? value)\n";
        const string additions = """
    public XPScriptUIField AddSeparator(object? name) => AddField(name, string.Empty, "Separator");
    public XPScriptUIField AddSpacer(object? name) => AddField(name, string.Empty, "Spacer");

    public void AddOption(object? name, object? value)
""";
        if (!generated.Contains(marker, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIForm basic structural controls.");
        return generated.Replace(marker, additions, StringComparison.Ordinal);
    }
}
