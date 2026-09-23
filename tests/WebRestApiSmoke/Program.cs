using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-rest-api-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var apiPath = Path.Combine(root, "api.xps");

Directory.CreateDirectory(Path.Combine(root, "schemas"));
await File.WriteAllTextAsync(Path.Combine(root, "schemas", "create-user.schema.json"), """
{"type":"object","required":["name","email","age"],"properties":{"name":{"type":"string","minLength":1,"maxLength":40},"email":{"type":"string"},"age":{"type":"integer","minimum":18,"maximum":120}}}
""");
await File.WriteAllTextAsync(Path.Combine(root, "schemas", "invalid.schema.json"), "{not-json");

var outsideSchemaRoot = Path.Combine(Path.GetTempPath(), "xps-rest-api-schema-outside-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(outsideSchemaRoot);
await File.WriteAllTextAsync(Path.Combine(outsideSchemaRoot, "outside.schema.json"), "{}");
await File.WriteAllTextAsync(Path.Combine(root, "schemas", "runtime-link.schema.json"), "{}");
var schemaLinkPath = Path.Combine(root, "schemas", "outside-link.json");
try
{
    File.CreateSymbolicLink(schemaLinkPath, Path.Combine(outsideSchemaRoot, "outside.schema.json"));
    try
    {
        _ = XpsJsonSchemaPath.ResolveInsideRoot(root, "schemas/outside-link.json");
        throw new Exception("JSON Schema symlink escaping the web root was accepted.");
    }
    catch (ArgumentException ex) when (ex.Message.Contains("web root", StringComparison.OrdinalIgnoreCase))
    {
    }
    Console.WriteLine("WEB-REST-JSON-SCHEMA-SYMLINK-CONTAINMENT=OK");
}
catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
{
    Console.WriteLine("WEB-REST-JSON-SCHEMA-SYMLINK-CONTAINMENT=SKIPPED");
}

var outsideSchemaDirectory = Path.Combine(Path.GetTempPath(), "xps-rest-api-schema-dir-outside-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(outsideSchemaDirectory);
await File.WriteAllTextAsync(Path.Combine(outsideSchemaDirectory, "outside.schema.json"), "{}");
var schemaDirectoryLink = Path.Combine(root, "schemas", "outside-dir-link");
try
{
    Directory.CreateSymbolicLink(schemaDirectoryLink, outsideSchemaDirectory);
    try
    {
        _ = XpsJsonSchemaPath.ResolveInsideRoot(root, "schemas/outside-dir-link/outside.schema.json");
        throw new Exception("JSON Schema directory symlink escaping the web root was accepted.");
    }
    catch (ArgumentException ex) when (ex.Message.Contains("web root", StringComparison.OrdinalIgnoreCase))
    {
    }
    Console.WriteLine("WEB-REST-JSON-SCHEMA-DIRECTORY-SYMLINK-CONTAINMENT=OK");
}
catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
{
    Console.WriteLine("WEB-REST-JSON-SCHEMA-DIRECTORY-SYMLINK-CONTAINMENT=SKIPPED");
}

await File.WriteAllTextAsync(apiPath, """
Public Class CreateUserRequest
    [Required]
    [MaxLength:40]
    Public Name As String

    [Required]
    [Email]
    Public Email As String

    [Range:18;120]
    Public Age As Integer
End Class

[Anonymous]
[Get]
[Route:/api/users/{id}]
[Cors:https://example.com]
[RateLimit:2;1m]
Function GetUser(ByVal id As Integer) As String
    GetUser = "user-" + id
End Function

[Anonymous]
[Get]
[Route:/api/bind/{id}]
Sub BindSources([FromRoute] id As Integer, [FromQuery] verbose As Boolean, [FromHeader:"X-Tenant-ID"] tenantId As String)
    Response.Write(CStr(id))
    Response.Write("|")
    Response.Write(CStr(verbose))
    Response.Write("|")
    Response.Write(tenantId)
End Sub

[Anonymous]
[Post]
[Route:/api/users]
[Cors:*]
[JsonSchema:schemas/create-user.schema.json]
Sub CreateUser([FromBody] payload As CreateUserRequest)
    Response.OK(payload)
End Sub

[Anonymous]
[Post]
[Route:/api/schema-missing]
[JsonSchema:schemas/missing.schema.json]
Sub MissingSchema()
    Response.Write("HANDLER-RAN")
End Sub

[Anonymous]
[Post]
[Route:/api/schema-invalid]
[JsonSchema:schemas/invalid.schema.json]
Sub InvalidSchema()
    Response.Write("HANDLER-RAN")
End Sub

[Anonymous]
[Post]
[Route:/api/schema-runtime-link]
[JsonSchema:schemas/runtime-link.schema.json]
Sub RuntimeLinkedSchema()
    Response.Write("HANDLER-RAN")
End Sub

[Anonymous]
[Post]
[Route:/api/raw]
Sub RawBody()
    Response.OK(Body.Text())
End Sub

[Anonymous]
[Get]
[Route:/api/problem]
Sub ProblemRoute()
    Response.problem(400, "Example problem", "Example detail")
End Sub

[Anonymous]
[Get]
[Route:/api/attachment-export]
Sub AttachmentExport()
    Dim db As New XPDBSQLite("web-attachment-security.db")
    Dim files As Variant
    Dim saved As JsonObject
    Dim id As String
    Dim fileNo As Integer

    Call db.Execute("CREATE TABLE IF NOT EXISTS customers (id INTEGER PRIMARY KEY, name TEXT NOT NULL)")
    Call db.Execute("DELETE FROM customers")
    Call db.Execute("INSERT INTO customers(id,name) VALUES(42,'Web')")

    fileNo = FreeFile()
    Open "web-attachment-source.txt" For Output As #fileNo
    Print #fileNo, "attachment payload"
    Close #fileNo

    Set files = db.Attachments("customers", "id", 42)
    Set saved = files.SaveAs("web-attachment-source.txt", "contract.txt", "web-user")
    id = CStr(saved.Get("attachmentId"))
    Call files.SaveToDisk(id, "exports/contract.txt")
    Response.Write(id)
    Call db.Close()
End Sub

[Anonymous]
[Get]
[Route:/api/attachment-export-traversal]
Sub AttachmentExportTraversal()
    Dim db As New XPDBSQLite("web-attachment-security.db")
    Dim files As Variant
    Dim rows As JsonArray
    Dim item As JsonObject

    Set files = db.Attachments("customers", "id", 42)
    Set rows = files.GetMetadata()
    Set item = rows.Get(0)
    Call files.SaveToDisk(CStr(item.Get("attachmentId")), "../escape.txt")
    Call db.Close()
End Sub

[Anonymous]
[Get]
[Route:/api/attachment-export-absolute]
Sub AttachmentExportAbsolute()
    Dim db As New XPDBSQLite("web-attachment-security.db")
    Dim files As Variant
    Dim rows As JsonArray
    Dim item As JsonObject

    Set files = db.Attachments("customers", "id", 42)
    Set rows = files.GetMetadata()
    Set item = rows.Get(0)
    Call files.SaveToDisk(CStr(item.Get("attachmentId")), Application.TempFolder & "/escape.txt")
    Call db.Close()
End Sub

[Anonymous]
[Get]
[Route:/api/attachment-download]
Sub AttachmentDownload()
    Dim db As New XPDBSQLite("web-attachment-security.db")
    Dim files As Variant
    Dim rows As JsonArray
    Dim item As JsonObject

    Set files = db.Attachments("customers", "id", 42)
    Set rows = files.GetMetadata()
    Set item = rows.Get(0)
    Call files.SendToBrowser(CStr(item.Get("attachmentId")), "download-contract.txt")
    Call db.Close()
End Sub
""");

try
{
    var parser = new XpsWebRouteMetadataParser();
    var parsed = parser.Parse(await File.ReadAllTextAsync(apiPath));
    if (parsed.Routes["GetUser"].RouteTemplate != "/api/users/{id}") throw new Exception("[Route] metadata was not retained.");
    if (parsed.Routes["GetUser"].Cors is null || parsed.Routes["GetUser"].RateLimit is null) throw new Exception("CORS or rate limit metadata was not retained.");
    if (parsed.Routes["CreateUser"].ValidationRules?.Count != 5) throw new Exception("Model validation metadata was not collected.");
    if (parsed.Routes["CreateUser"].JsonSchema != "schemas/create-user.schema.json") throw new Exception("JSON Schema route metadata was not retained.");
    if (parsed.Routes["BindSources"].ParameterBindings?.Count != 3) throw new Exception("Explicit parameter bindings were not retained.");
    if (parsed.Source.Contains("[FromRoute]", StringComparison.OrdinalIgnoreCase)) throw new Exception("Parameter binding syntax was not stripped before compilation.");

    await using var dispatcher = new XpsWebDispatcher(root);
    var app = new XpsApplicationState();

    // Keep method-dispatch hardening first: a path match must never invoke a handler for another HTTP method.
    var wrongMethodGetRoute = await SendAsync(dispatcher, app, "POST", "/api/users/42");
    if (wrongMethodGetRoute.StatusCode != 405)
        throw new Exception($"POST reached or incorrectly resolved GET-only route; expected 405, got {wrongMethodGetRoute.StatusCode}.");
    if (BodyText(wrongMethodGetRoute).Contains("user-42", StringComparison.Ordinal))
        throw new Exception("POST executed the GET-only route handler.");

    var wrongMethodPostRoute = await SendAsync(dispatcher, app, "GET", "/api/users");
    if (wrongMethodPostRoute.StatusCode != 405)
        throw new Exception($"GET reached or incorrectly resolved POST-only route; expected 405, got {wrongMethodPostRoute.StatusCode}.");

    // Keep JSON parser hardening first so parser regressions fail before the broader REST suite.
    var earlyMalformedJson = await SendAsync(
        dispatcher, app, "POST", "/api/users", "{not-json", "application/json");
    if (earlyMalformedJson.StatusCode != 400)
        throw new Exception($"Malformed request JSON returned {earlyMalformedJson.StatusCode} instead of 400.");
    var earlyMalformedBody = BodyText(earlyMalformedJson);
    if (!earlyMalformedBody.Contains("body", StringComparison.OrdinalIgnoreCase) ||
        earlyMalformedBody.Contains("StackTrace", StringComparison.OrdinalIgnoreCase) ||
        earlyMalformedBody.Contains(".cs:", StringComparison.OrdinalIgnoreCase) ||
        earlyMalformedBody.Contains("/home/", StringComparison.OrdinalIgnoreCase) ||
        earlyMalformedBody.Contains("\\Users\\", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Malformed request JSON leaked parser diagnostics or paths.");

    var earlyDeepJson = new string('[', 65) + "0" + new string(']', 65);
    var deepJsonResponse = await SendAsync(
        dispatcher, app, "POST", "/api/users", earlyDeepJson, "application/json");
    if (deepJsonResponse.StatusCode != 400)
        throw new Exception($"Deeply nested request JSON returned {deepJsonResponse.StatusCode} instead of 400.");

    var duplicateJsonResponse = await SendAsync(
        dispatcher, app, "POST", "/api/users",
        "{\"name\":\"first\",\"name\":\"second\",\"email\":\"fredrik@example.com\",\"age\":42}",
        "application/json");
    if (duplicateJsonResponse.StatusCode != 200)
        throw new Exception($"Duplicate-property JSON returned {duplicateJsonResponse.StatusCode}.");
    using (var duplicateJsonDocument = JsonDocument.Parse(duplicateJsonResponse.Body))
    {
        if (duplicateJsonDocument.RootElement.GetProperty("name").GetString() != "second")
            throw new Exception("Duplicate JSON property behavior changed; last-value-wins is expected.");
    }

    var oversizedJsonBody = "{\"name\":\"" + new string('x', XpsRequestBody.DefaultMaxJsonBytes) +
                            "\",\"email\":\"fredrik@example.com\",\"age\":42}";
    var oversizedJsonResponse = await SendAsync(
        dispatcher, app, "POST", "/api/users", oversizedJsonBody, "application/json");
    if (oversizedJsonResponse.StatusCode != 400)
        throw new Exception($"Oversized REST JSON returned {oversizedJsonResponse.StatusCode} instead of 400.");
    var oversizedJsonProblem = BodyText(oversizedJsonResponse);
    if (!oversizedJsonProblem.Contains("exceeds the configured", StringComparison.OrdinalIgnoreCase) ||
        oversizedJsonProblem.Contains("StackTrace", StringComparison.OrdinalIgnoreCase) ||
        oversizedJsonProblem.Contains(".cs:", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Oversized REST JSON did not return the bounded generic validation error.");

    var get = await SendAsync(dispatcher, app, "GET", "/api/users/42", origin: "https://example.com");
    if (get.StatusCode != 200) throw new Exception($"REST GET returned {get.StatusCode}.");
    if (BodyText(get) != "\"user-42\"") throw new Exception("Function return value was not automatically serialized as JSON.");
    if (Header(get, "Access-Control-Allow-Origin") != "https://example.com") throw new Exception("CORS response header was missing.");

    var binding = await SendAsync(
        dispatcher,
        app,
        "GET",
        "/api/bind/9",
        queryString: "verbose=true",
        extraHeaders: new Dictionary<string, string> { ["X-Tenant-ID"] = "tenant-1" });
    if (binding.StatusCode != 200 || BodyText(binding) != "9|True|tenant-1")
        throw new Exception("FromRoute/FromQuery/FromHeader binding failed: " + BodyText(binding));

    var preflight = await SendAsync(
        dispatcher,
        app,
        "OPTIONS",
        "/api/users/42",
        origin: "https://example.com",
        extraHeaders: new Dictionary<string, string>
        {
            ["Access-Control-Request-Method"] = "GET",
            ["Access-Control-Request-Headers"] = "Content-Type, X-Test"
        });
    if (preflight.StatusCode != 204) throw new Exception($"CORS preflight returned {preflight.StatusCode}.");
    if (!Header(preflight, "Access-Control-Allow-Methods").Contains("GET", StringComparison.OrdinalIgnoreCase)) throw new Exception("CORS preflight did not advertise GET.");

    var create = await SendAsync(
        dispatcher,
        app,
        "POST",
        "/api/users",
        "{\"name\":\"Fredrik\",\"email\":\"fredrik@example.com\",\"age\":42}",
        "application/json",
        origin: "https://client.example");
    if (create.StatusCode != 200) throw new Exception($"JSON body binding returned {create.StatusCode}: {BodyText(create)}");
    using (var json = JsonDocument.Parse(create.Body))
    {
        if (!json.RootElement.TryGetProperty("name", out var name) || name.GetString() != "Fredrik")
            throw new Exception("Response.OK did not serialize bound XPScript model data.");
    }
    if (Header(create, "Access-Control-Allow-Origin") != "*") throw new Exception("Wildcard CORS response header was missing.");
    Console.WriteLine("WEB-REST-CLASS-JSON-BINDING=OK");

    var schemaInvalid = await SendAsync(
        dispatcher,
        app,
        "POST",
        "/api/users",
        "{\"name\":\"Fredrik\",\"email\":\"fredrik@example.com\",\"age\":12}",
        "application/json");
    if (schemaInvalid.StatusCode != 400) throw new Exception($"JSON Schema invalid body returned {schemaInvalid.StatusCode} instead of 400.");
    var schemaInvalidBody = BodyText(schemaInvalid);
    if (!schemaInvalidBody.Contains("$.age", StringComparison.Ordinal) || !schemaInvalidBody.Contains("JSON Schema validation failed", StringComparison.Ordinal))
        throw new Exception("JSON Schema Problem Details did not contain structured field path errors.");
    using (var schemaProblem = JsonDocument.Parse(schemaInvalid.Body))
    {
        var rootElement = schemaProblem.RootElement;
        if (!rootElement.TryGetProperty("validationErrors", out var validationErrors) || validationErrors.ValueKind != JsonValueKind.Array || validationErrors.GetArrayLength() == 0)
            throw new Exception("JSON Schema Problem Details did not contain validationErrors.");
        var ageError = validationErrors.EnumerateArray().FirstOrDefault(item =>
            item.TryGetProperty("path", out var pathValue) && pathValue.GetString() == "$.age");
        if (ageError.ValueKind != JsonValueKind.Object ||
            !ageError.TryGetProperty("schemaPath", out var schemaPathValue) || string.IsNullOrWhiteSpace(schemaPathValue.GetString()) ||
            !ageError.TryGetProperty("keyword", out var keywordValue) || keywordValue.GetString() != "minimum" ||
            !ageError.TryGetProperty("message", out var messageValue) || string.IsNullOrWhiteSpace(messageValue.GetString()) ||
            !ageError.TryGetProperty("expected", out var expectedValue) || expectedValue.GetString() != ">= 18" ||
            !ageError.TryGetProperty("actual", out var actualValue) || actualValue.GetString() != "12")
            throw new Exception("JSON Schema Problem Details did not preserve the full XPJsonValidationResult error.");
    }
    Console.WriteLine("WEB-REST-JSON-SCHEMA-VALIDATION=OK");

    try
    {
        File.Delete(Path.Combine(root, "schemas", "runtime-link.schema.json"));
        File.CreateSymbolicLink(
            Path.Combine(root, "schemas", "runtime-link.schema.json"),
            Path.Combine(outsideSchemaRoot, "outside.schema.json"));
        var runtimeLinkedSchema = await SendAsync(
            dispatcher,
            app,
            "POST",
            "/api/schema-runtime-link",
            "{}",
            "application/json");
        if (runtimeLinkedSchema.StatusCode != 500)
            throw new Exception($"Runtime JSON Schema symlink escape returned {runtimeLinkedSchema.StatusCode} instead of 500.");
        if (BodyText(runtimeLinkedSchema).Contains("HANDLER-RAN", StringComparison.Ordinal))
            throw new Exception("Route handler executed when runtime JSON Schema escaped the web root through a symlink.");
        Console.WriteLine("WEB-REST-JSON-SCHEMA-RUNTIME-SYMLINK-CONTAINMENT=OK");
    }
    catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
    {
        Console.WriteLine("WEB-REST-JSON-SCHEMA-RUNTIME-SYMLINK-CONTAINMENT=SKIPPED");
    }

    var malformedJson = await SendAsync(
        dispatcher,
        app,
        "POST",
        "/api/users",
        "{not-json",
        "application/json");
    if (malformedJson.StatusCode != 400)
        throw new Exception($"Malformed request JSON returned {malformedJson.StatusCode} instead of 400.");
    if (!BodyText(malformedJson).Contains("body", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Malformed request JSON did not return a body validation error.");

    var missingSchema = await SendAsync(
        dispatcher,
        app,
        "POST",
        "/api/schema-missing",
        "{}",
        "application/json");
    if (missingSchema.StatusCode != 500)
        throw new Exception($"Missing configured JSON Schema returned {missingSchema.StatusCode} instead of 500.");
    if (BodyText(missingSchema).Contains("HANDLER-RAN", StringComparison.Ordinal))
        throw new Exception("Route handler executed when configured JSON Schema was missing.");

    var invalidSchema = await SendAsync(
        dispatcher,
        app,
        "POST",
        "/api/schema-invalid",
        "{}",
        "application/json");
    if (invalidSchema.StatusCode != 500)
        throw new Exception($"Invalid configured JSON Schema returned {invalidSchema.StatusCode} instead of 500.");
    if (BodyText(invalidSchema).Contains("HANDLER-RAN", StringComparison.Ordinal))
        throw new Exception("Route handler executed when configured JSON Schema was invalid.");

    Console.WriteLine("WEB-REST-JSON-SCHEMA-ERROR-CLASSIFICATION=OK");

    var schemaFilePath = Path.Combine(root, "schemas", "create-user.schema.json");
    var originalSchemaTimestamp = File.GetLastWriteTimeUtc(schemaFilePath);
    var restrictiveSchema = """
{"type":"object","required":["name","email","age"],"properties":{"name":{"type":"string","minLength":1,"maxLength":40},"email":{"type":"string"},"age":{"type":"integer","minimum":18,"maximum":119}}}
""";
    var permissiveSchema = """
{"type":"object","required":["name","email","age"],"properties":{"name":{"type":"string","minLength":1,"maxLength":40},"email":{"type":"string"},"age":{"type":"integer","minimum":18,"maximum":120}}}
""";
    if (Encoding.UTF8.GetByteCount(restrictiveSchema) != Encoding.UTF8.GetByteCount(permissiveSchema))
        throw new Exception("JSON Schema cache regression requires same-length schema files.");

    await File.WriteAllTextAsync(schemaFilePath, restrictiveSchema);
    File.SetLastWriteTimeUtc(schemaFilePath, originalSchemaTimestamp);
    var reloadedSchema = await SendAsync(
        dispatcher,
        app,
        "POST",
        "/api/users",
        "{\"name\":\"Fredrik\",\"email\":\"fredrik@example.com\",\"age\":120}",
        "application/json");
    if (reloadedSchema.StatusCode != 400 || !BodyText(reloadedSchema).Contains("$.age", StringComparison.Ordinal))
        throw new Exception("Same-length, same-timestamp JSON Schema update was not reloaded from the cache.");

    await File.WriteAllTextAsync(schemaFilePath, permissiveSchema);
    File.SetLastWriteTimeUtc(schemaFilePath, originalSchemaTimestamp);
    var restoredSchema = await SendAsync(
        dispatcher,
        app,
        "POST",
        "/api/users",
        "{\"name\":\"Fredrik\",\"email\":\"fredrik@example.com\",\"age\":120}",
        "application/json");
    if (restoredSchema.StatusCode != 200)
        throw new Exception($"Same-length, same-timestamp JSON Schema restore was not reloaded from the cache: {restoredSchema.StatusCode}.");
    Console.WriteLine("WEB-REST-JSON-SCHEMA-CACHE-CONTENT-HASH=OK");

    var invalid = await SendAsync(
        dispatcher,
        app,
        "POST",
        "/api/users",
        "{\"name\":\"\",\"email\":\"not-an-email\",\"age\":12}",
        "application/json");
    if (invalid.StatusCode != 400) throw new Exception($"Invalid JSON model returned {invalid.StatusCode} instead of 400.");
    if (!invalid.ContentType!.StartsWith("application/problem+json", StringComparison.OrdinalIgnoreCase)) throw new Exception("Validation failure did not return Problem Details.");
    var invalidBody = BodyText(invalid);
    if (!invalidBody.Contains("Email", StringComparison.OrdinalIgnoreCase) || !invalidBody.Contains("Age", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Validation Problem Details did not contain field errors.");

    var raw = await SendAsync(dispatcher, app, "POST", "/api/raw", "hello-body", "text/plain");
    if (raw.StatusCode != 200 || BodyText(raw) != "\"hello-body\"") throw new Exception("Reserved Body object did not expose request text.");

    var problem = await SendAsync(dispatcher, app, "GET", "/api/problem");
    if (problem.StatusCode != 400 || !problem.ContentType!.StartsWith("application/problem+json", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Response.problem did not produce RFC Problem Details JSON.");

    var export = await SendAsync(dispatcher, app, "GET", "/api/attachment-export", serverRoot: root);
    if (export.StatusCode != 200) throw new Exception($"Attachment private export returned {export.StatusCode}: {BodyText(export)}");
    var siteHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(root))))[..24].ToLowerInvariant();
    var privateFile = Path.Combine(Path.GetTempPath(), "xpscript-private-attachments", siteHash, "exports", "contract.txt");
    if (!File.Exists(privateFile)) throw new Exception("Attachment private export was not written to the managed sandbox.");
    if (File.Exists(Path.Combine(root, "exports", "contract.txt"))) throw new Exception("Attachment export was written beneath the web root.");
    if ((await File.ReadAllTextAsync(privateFile)).TrimEnd('\r', '\n') != "attachment payload") throw new Exception("Attachment private export bytes did not match source content.");

    var traversal = await SendAsync(dispatcher, app, "GET", "/api/attachment-export-traversal", serverRoot: root);
    if (traversal.StatusCode < 400) throw new Exception("Attachment traversal export was not rejected.");
    if (File.Exists(Path.Combine(Path.GetTempPath(), "xpscript-private-attachments", "escape.txt"))) throw new Exception("Attachment traversal created an escaped file.");

    var absolute = await SendAsync(dispatcher, app, "GET", "/api/attachment-export-absolute", serverRoot: root);
    if (absolute.StatusCode < 400) throw new Exception("Attachment absolute-path export was not rejected.");

    var download = await SendAsync(dispatcher, app, "GET", "/api/attachment-download", serverRoot: root);
    if (download.StatusCode != 200) throw new Exception($"Attachment browser download returned {download.StatusCode}: {BodyText(download)}");
    if (!download.ContentType!.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase)) throw new Exception("Attachment browser download content type was incorrect.");
    var disposition = Header(download, "Content-Disposition");
    if (!disposition.StartsWith("attachment;", StringComparison.OrdinalIgnoreCase) || !disposition.Contains("download-contract.txt", StringComparison.Ordinal))
        throw new Exception("Attachment browser download Content-Disposition was incorrect: " + disposition);
    if (BodyText(download).TrimEnd('\r', '\n') != "attachment payload") throw new Exception("Attachment browser download bytes did not match source content.");

    var second = await SendAsync(dispatcher, app, "GET", "/api/users/43");
    if (second.StatusCode != 200) throw new Exception("Second rate-limited request should still be allowed.");
    var limited = await SendAsync(dispatcher, app, "GET", "/api/users/44");
    if (limited.StatusCode != 429) throw new Exception($"Third request returned {limited.StatusCode} instead of 429.");
    if (string.IsNullOrWhiteSpace(Header(limited, "Retry-After"))) throw new Exception("429 response did not include Retry-After.");

    Console.WriteLine("WEB-ATTACHMENT-PRIVATE-EXPORT=OK");
    Console.WriteLine("WEB-ATTACHMENT-BROWSER-DOWNLOAD=OK");
    Console.WriteLine("WEB-REST-API-SMOKE=OK");
}
finally
{
    var siteHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(root))))[..24].ToLowerInvariant();
    var privateRoot = Path.Combine(Path.GetTempPath(), "xpscript-private-attachments", siteHash);
    try { if (Directory.Exists(privateRoot)) Directory.Delete(privateRoot, true); } catch { }
    foreach (var file in new[] { "web-attachment-security.db", "web-attachment-source.txt" })
    {
        try { if (File.Exists(file)) File.Delete(file); } catch { }
    }
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}

static async Task<XpsWebResponse> SendAsync(
    IXpsWebRequestHandler handler,
    IXpsApplicationState app,
    string method,
    string path,
    string body = "",
    string? contentType = null,
    string? origin = null,
    IReadOnlyDictionary<string, string>? extraHeaders = null,
    string queryString = "",
    string? serverRoot = null)
{
    var headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
    if (origin is not null) headers["Origin"] = [origin];
    if (extraHeaders is not null)
        foreach (var pair in extraHeaders) headers[pair.Key] = [pair.Value];
    var bytes = Encoding.UTF8.GetBytes(body);
    var request = new XpsWebRequest(
        method,
        path,
        "",
        queryString,
        headers,
        contentType,
        bytes.Length,
        bytes,
        "localhost",
        "http",
        "127.0.0.1",
        "HTTP/1.1",
        new Dictionary<string, string>());
    var response = new XpsWebResponse();
    var context = new XpsWebContext(
        request,
        response,
        new XpsServerInfo("rest-smoke", serverRoot ?? Path.GetTempPath(), XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"),
        new XpsWebPrincipal(false),
        app);
    await handler.HandleAsync(context);
    return response;
}

static string BodyText(XpsWebResponse response) => Encoding.UTF8.GetString(response.Body.Span);

static string Header(XpsWebResponse response, string name) =>
    response.Headers.TryGetValue(name, out var values) ? values.FirstOrDefault() ?? string.Empty : string.Empty;
