namespace XPScript.Web.Runtime;

public interface IXpsWebRequestHandler
{
    Task HandleAsync(XpsWebContext context);
}
