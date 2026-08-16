namespace XPScript.Web.Runtime;

public static class XpsWebRuntimeObjects
{
    public static XpsWebRequest Request => XpsWebContextAccessor.Current.Request;
    public static XpsWebResponse Response => XpsWebContextAccessor.Current.Response;
    public static XpsWebServer Server => new(XpsWebContextAccessor.Current.Server);
}
