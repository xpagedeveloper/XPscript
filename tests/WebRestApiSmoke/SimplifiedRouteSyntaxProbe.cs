using System.Runtime.CompilerServices;
using XPScript.Web.Runtime;

internal static class SimplifiedRouteSyntaxProbe
{
    [ModuleInitializer]
    internal static void Run()
    {
        var parser = new XpsWebRouteMetadataParser();
        var parsed = parser.Parse("""
[RoutePrefix:/api/users]
[Authenticated]
[Role:admin;user]

[Get:/]
Function ListUsers() As String
    ListUsers = "ok"
End Function

[Get:/{id}]
[Role:editor]
Function GetUser(id As Integer) As String
    GetUser = CStr(id)
End Function

[Delete:/{id}]
[Role:]
Sub DeleteUser(id As Integer)
End Sub

[Get:/public]
[Anonymous]
Function PublicUsers() As String
    PublicUsers = "public"
End Function
""");

        var list = parsed.Routes["ListUsers"];
        if (list.RouteTemplate != "/api/users") throw new Exception("RoutePrefix root combination failed.");
        if (list.Policy.AllowAnonymous) throw new Exception("File-level Authenticated was not inherited.");
        if (list.Policy.RequiredRoles is null || !list.Policy.RequiredRoles.Contains("admin", StringComparer.OrdinalIgnoreCase) || !list.Policy.RequiredRoles.Contains("user", StringComparer.OrdinalIgnoreCase))
            throw new Exception("File-level roles were not inherited.");

        var get = parsed.Routes["GetUser"];
        if (get.RouteTemplate != "/api/users/{id}") throw new Exception("RoutePrefix parameter combination failed.");
        if (get.Policy.AllowAnonymous) throw new Exception("Function role override unexpectedly changed inherited authentication.");
        if (get.Policy.RequiredRoles is null || get.Policy.RequiredRoles.Count != 1 || !get.Policy.RequiredRoles.Contains("editor", StringComparer.OrdinalIgnoreCase))
            throw new Exception("Function Role did not replace file-level roles.");

        var delete = parsed.Routes["DeleteUser"];
        if (delete.Policy.RequiredRoles is null || delete.Policy.RequiredRoles.Count != 0 || delete.Policy.ForbiddenRoles is null || delete.Policy.ForbiddenRoles.Count != 0)
            throw new Exception("Empty function [Role:] did not clear inherited roles.");
        if (delete.Policy.AllowAnonymous) throw new Exception("Clearing roles must not clear inherited authentication.");

        var publicRoute = parsed.Routes["PublicUsers"];
        if (!publicRoute.Policy.AllowAnonymous) throw new Exception("Function Anonymous did not replace file-level Authenticated.");
        if (publicRoute.Policy.RequiredRoles is null || publicRoute.Policy.RequiredRoles.Count != 2)
            throw new Exception("Function auth override must not implicitly replace inherited roles.");

        var legacy = parser.Parse("""
[Anonymous]
[Get:/legacy/full]
Function Legacy() As String
    Legacy = "ok"
End Function
""").Routes["Legacy"];
        if (legacy.RouteTemplate != "/legacy/full") throw new Exception("Full method route path changed when RoutePrefix is absent.");
        if (!legacy.Policy.AllowAnonymous) throw new Exception("Legacy function-level Anonymous behavior changed.");

        var multiMethod = parser.Parse("""
[Anonymous]
[Get:/api/ping]
[Post:/api/ping]
Function Ping() As String
    Ping = "pong"
End Function
""").Routes["Ping"];
        if (!multiMethod.Policy.Methods.Contains("GET") || !multiMethod.Policy.Methods.Contains("POST") || multiMethod.RouteTemplate != "/api/ping")
            throw new Exception("Combined method/path shorthand did not support multiple methods on one route.");

        Console.WriteLine("WEB-REST-SIMPLIFIED-SYNTAX=OK");
    }
}
