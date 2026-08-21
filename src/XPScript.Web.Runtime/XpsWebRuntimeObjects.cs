namespace XPScript.Web.Runtime;

public static class XpsWebRuntimeObjects
{
    private static readonly XpsProcessState SharedProcessState = new();
    private static readonly XpsWebProcess SharedProcess = new(SharedProcessState);

    public static XpsWebRequest Request => XpsWebContextAccessor.Current.Request;
    public static XpsWebResponse Response => XpsWebContextAccessor.Current.Response;
    public static XpsRequestBody Body => new(XpsWebContextAccessor.Current.Request);
    public static XpsWebServer Server => new(XpsWebContextAccessor.Current.Server);
    public static IXpsRequestState RequestScope
    {
        get
        {
            var context = XpsWebContextAccessor.Current;
            var inherited = XpsNavigationStateHandoff.TryConsume(context.Request, context.Response);
            if (inherited is not null)
            {
                foreach (var key in inherited.Keys)
                    context.RequestScope.Set(key, inherited.Get(key));
            }
            return context.RequestScope;
        }
    }
    public static XpsWebProcess Process => SharedProcess;
    public static IXpsApplicationState Application => XpsWebContextAccessor.Current.Application;
    public static IXpsSession Session =>
        XpsWebContextAccessor.Current.Session ??
        throw new InvalidOperationException("Session support is not enabled for this XPScript site.");

    public static void StageRequestStateForNavigation()
        => XpsNavigationStateHandoff.StageCurrent();

    public static bool TryStageRequestStateForNavigation()
    {
        try
        {
            XpsNavigationStateHandoff.StageCurrent();
            return true;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No XPScript web request context is active", StringComparison.Ordinal))
        {
            return false;
        }
    }
}
