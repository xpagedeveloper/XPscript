namespace XPScript.Web.Runtime;

public static class XpsWebRuntimeObjects
{
    public static XpsWebRequest Request => XpsWebContextAccessor.Current.Request;
    public static XpsWebResponse Response => XpsWebContextAccessor.Current.Response;
    public static XpsRequestBody Body => new(XpsWebContextAccessor.Current.Request);
    public static XpsWebServer Server => new(XpsWebContextAccessor.Current.Server);
    public static IXpsRequestState RequestScope => XpsWebContextAccessor.Current.RequestScope;
    public static IXpsApplicationState Application => XpsWebContextAccessor.Current.Application;
    public static IXpsSession Session =>
        XpsWebContextAccessor.Current.Session ??
        throw new InvalidOperationException("Session support is not enabled for this XPScript site.");
}
