using System.Globalization;
using System.Text;
using XPScript.Web.Runtime;

namespace XPScript.Web.Kestrel;

internal static class XpsSessionMetrics
{
    public static string Render(XpsWebTelemetry telemetry, XpsSessionStore? sessions, long activeConnections)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        var builder = new StringBuilder(telemetry.RenderPrometheus());

        builder.Append("# TYPE xpscript_web_active_connections gauge\n");
        builder.Append("# HELP xpscript_web_active_connections Current active Kestrel connections.\n");
        builder.Append("xpscript_web_active_connections ")
            .Append(Math.Max(0, activeConnections).ToString(CultureInfo.InvariantCulture))
            .Append('\n');

        if (sessions is null) return builder.ToString();

        builder.Append("# TYPE xpscript_web_sessions_active gauge\n");
        builder.Append("# HELP xpscript_web_sessions_active Current active in-memory sessions.\n");
        builder.Append("xpscript_web_sessions_active ")
            .Append(sessions.Count.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
        return builder.ToString();
    }
}
