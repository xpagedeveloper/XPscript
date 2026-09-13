namespace XPScript.Compiler;

internal static class SystemInventoryRuntimePostProcessor
{
    private const string Marker = "XPScriptSystemInventoryFactory.Create()";

    public static string Transform(string generated, string runtimeIdentifier)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (!generated.Contains(Marker, StringComparison.Ordinal)) return generated;

        if (runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase))
            throw new CompilerException("SystemInventory is not available for browser-wasm targets because browser sandboxes do not expose host hardware, registry, package databases, or machine inventory APIs.");

        if (generated.Contains("internal sealed class XPScriptSystemInventory", StringComparison.Ordinal)) return generated;
        return generated + Environment.NewLine + Environment.NewLine + SystemInventoryRuntimeSource.Code + Environment.NewLine;
    }
}
