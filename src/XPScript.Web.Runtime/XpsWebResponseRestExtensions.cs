using System.Text.Json;

public static class XpsWebResponseRestExtensions
{
    public static void Json(this XPScript.Web.Runtime.XpsWebResponse response, object? data)
        => WriteJson(response, 200, data, "application/json; charset=utf-8");

    public static void OK(this XPScript.Web.Runtime.XpsWebResponse response, object? data)
        => WriteJson(response, 200, data, "application/json; charset=utf-8");

    public static void Ok(this XPScript.Web.Runtime.XpsWebResponse response, object? data)
        => response.OK(data);

    public static void Created(this XPScript.Web.Runtime.XpsWebResponse response, string location, object? data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        WriteJson(response, 201, data, "application/json; charset=utf-8");
        response.SetHeader("Location", location);
    }

    public static void NoContent(this XPScript.Web.Runtime.XpsWebResponse response)
    {
        response.Clear();
        response.StatusCode = 204;
        response.ContentType = null;
    }

    public static void BadRequest(this XPScript.Web.Runtime.XpsWebResponse response, string detail)
        => response.Problem(400, "Bad Request", detail);

    public static void NotFound(this XPScript.Web.Runtime.XpsWebResponse response, string detail = "Resource was not found.")
        => response.Problem(404, "Not Found", detail);

    public static void Unauthorized(this XPScript.Web.Runtime.XpsWebResponse response, string detail = "Authentication is required.")
        => response.Problem(401, "Unauthorized", detail);

    public static void Forbidden(this XPScript.Web.Runtime.XpsWebResponse response, string detail = "Access is forbidden.")
        => response.Problem(403, "Forbidden", detail);

    public static void Conflict(this XPScript.Web.Runtime.XpsWebResponse response, string detail)
        => response.Problem(409, "Conflict", detail);

    public static void Problem(this XPScript.Web.Runtime.XpsWebResponse response, int status, string title, string detail)
        => Problem(response, status, title, detail, null);

    public static void problem(this XPScript.Web.Runtime.XpsWebResponse response, int status, string title, string detail)
        => Problem(response, status, title, detail, null);

    public static void Problem(
        this XPScript.Web.Runtime.XpsWebResponse response,
        int status,
        string title,
        string detail,
        IReadOnlyDictionary<string, string[]>? errors)
    {
        if (status is < 400 or > 599) throw new ArgumentOutOfRangeException(nameof(status), "Problem status must be between 400 and 599.");
        var payload = errors is null
            ? new Dictionary<string, object?>
            {
                ["type"] = "about:blank",
                ["title"] = title ?? string.Empty,
                ["status"] = status,
                ["detail"] = detail ?? string.Empty
            }
            : new Dictionary<string, object?>
            {
                ["type"] = "about:blank",
                ["title"] = title ?? string.Empty,
                ["status"] = status,
                ["detail"] = detail ?? string.Empty,
                ["errors"] = errors
            };
        WriteJson(response, status, payload, "application/problem+json; charset=utf-8");
    }

    private static void WriteJson(
        XPScript.Web.Runtime.XpsWebResponse response,
        int status,
        object? data,
        string contentType)
    {
        ArgumentNullException.ThrowIfNull(response);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, XPScript.Web.Runtime.XpsRestJson.Options);
        if (bytes.Length > response.MaxBodyBytes)
            throw new InvalidOperationException($"JSON response exceeds the configured {response.MaxBodyBytes} byte limit.");
        response.Clear();
        response.StatusCode = status;
        response.ContentType = contentType;
        response.WriteBinary(bytes);
    }
}
