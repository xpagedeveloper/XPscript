using System.Runtime.CompilerServices;

namespace XPScript.Web.Compiler;

internal static class BrowserWasmPublishEnvironment
{
#pragma warning disable CA2255 // XPscript must configure the child MSBuild environment before browser-WASM publish.
    [ModuleInitializer]
    internal static void Initialize()
#pragma warning restore CA2255
    {
        // XPscript-generated browser code relies on dynamic dispatch. The WebAssembly
        // trimmer cannot safely analyze Microsoft.CSharp.RuntimeBinder call sites and
        // can remove members required at runtime. MSBuild imports environment variables
        // as properties, so child `dotnet publish` processes inherit this setting.
        Environment.SetEnvironmentVariable("PublishTrimmed", "false");
    }
}
