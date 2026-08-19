using System.Runtime.InteropServices.JavaScript;

namespace XPScript.UI.Browser;

public static partial class BrowserFormHost
{
    public static string ShowDialog(string requestJson)
    {
        ArgumentNullException.ThrowIfNull(requestJson);
        return RenderForm(requestJson);
    }

    [JSImport("renderForm", "xpscript-browser")]
    private static partial string RenderForm(string requestJson);
}
