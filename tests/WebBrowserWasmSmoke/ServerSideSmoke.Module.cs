using System.Runtime.CompilerServices;

internal static class ServerSideSmokeModule
{
    [ModuleInitializer]
    public static void Initialize()
    {
        var root = Path.Combine(Path.GetTempPath(), "xps-browser-wasm-server-side-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { ServerSideSmoke.RunAsync(root).GetAwaiter().GetResult(); }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
