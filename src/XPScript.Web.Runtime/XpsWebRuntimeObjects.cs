namespace XPScript.Web.Runtime;

public static class XpsWebRuntimeObjects
{
    private static readonly XpsProcessState SharedProcessState = new();
    private static readonly XpsWebProcess SharedProcess = new(SharedProcessState);

    public static XpsWebRequest Request => XpsWebContextAccessor.Current.Request;
    public static XpsWebResponse Response => XpsWebContextAccessor.Current.Response;
    public static XpsRequestBody Body => new(XpsWebContextAccessor.Current.Request);
    public static XpsWebServer Server => new(XpsWebContextAccessor.Current.Server);
    public static IXpsRequestState RequestScope => XpsWebContextAccessor.Current.RequestScope;
    public static XpsWebProcess Process => SharedProcess;
    public static IXpsApplicationState Application => XpsWebContextAccessor.Current.Application;
    public static IXpsSession Session =>
        XpsWebContextAccessor.Current.Session ??
        throw new InvalidOperationException("Session support is not enabled for this XPScript site.");
}
