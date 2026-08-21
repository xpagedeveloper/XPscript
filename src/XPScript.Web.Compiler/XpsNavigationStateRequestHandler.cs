using System.Text.Json;
using XPScript.Web.Runtime;

namespace XPScript.Web.Compiler;

public sealed class XpsNavigationStateRequestHandler : IXpsWebRequestHandler, IXpsWebMetricsProvider
{
    private readonly IXpsWebRequestHandler _inner;

    public XpsNavigationStateRequestHandler(IXpsWebRequestHandler inner)
        => _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public async Task HandleAsync(XpsWebContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (XpsNavigationStateHandoff.IsStageEndpoint(context.Request.Path))
        {
            if (!context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 405;
                context.Response.SetHeader("Allow", "POST");
                context.Response.Complete();
                return;
            }

            if (context.Request.ContentType is null ||
                !context.Request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 415;
                context.Response.Complete();
                return;
            }

            try
            {
                XpsNavigationStateHandoff.StageJson(context.Request, context.Response);
                context.Response.StatusCode = 204;
                context.Response.ContentType = null;
                context.Response.Complete();
            }
            catch (Exception ex) when (ex is InvalidOperationException or JsonException or FormatException or OverflowException)
            {
                context.Response.Clear();
                context.Response.StatusCode = 400;
                context.Response.ContentType = "text/plain; charset=utf-8";
                context.Response.Write("Invalid Request.State navigation handoff.");
                context.Response.Complete();
            }
            return;
        }

        XpsNavigationStateHandoff.ConsumeInto(context.RequestScope, context.Request, context.Response);
        await _inner.HandleAsync(context).ConfigureAwait(false);
    }

    public string RenderPrometheusMetrics()
        => (_inner as IXpsWebMetricsProvider)?.RenderPrometheusMetrics() ?? string.Empty;
}
