using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var fixture = Path.Combine(AppContext.BaseDirectory, "petstore.yaml");
var reimportFixture = Path.Combine(AppContext.BaseDirectory, "petstore-reimport.yaml");
var generator = new XpsOpenApiGenerator();
var result = generator.GenerateFile(fixture);
var clientResult = new XpsOpenApiClientGenerator().GenerateFile(fixture);
var securityClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Security Smoke, version: 1.0.0 }
components:
  securitySchemes:
    BearerAuth: { type: http, scheme: bearer }
    BasicAuth: { type: http, scheme: basic }
    ApiKey: { type: apiKey, in: header, name: X-API-Key }
security:
  - BearerAuth: []
  - BasicAuth: []
paths:
  /secure:
    get:
      operationId: secure
      security:
        - BearerAuth: []
          ApiKey: []
        - BasicAuth: []
      responses:
        '200': { description: ok }
  /public:
    get:
      operationId: publicCall
      security: []
      responses:
        '204': { description: ok }
""", "security.yaml").Source;
foreach (var marker in new[] { "request.SetBearerToken(AuthBearerAuth)", "request.SetAuthorization(AuthBasicAuthAuthorization)", "request.SetHeader(\"X-API-Key\", AuthApiKey)", "ElseIf", "Public Function PublicCall" })
    if (!securityClient.Contains(marker, StringComparison.Ordinal)) throw new Exception("Generated OpenAPI security client is missing marker: " + marker);
if (!securityClient.Contains("Private AuthBasicAuthAuthorization As String", StringComparison.Ordinal) ||
    !securityClient.Contains("AuthBasicAuthAuthorization = Http.BasicAuthorization(username, password)", StringComparison.Ordinal) ||
    securityClient.Contains("Public AuthBasicAuthAuthorization", StringComparison.Ordinal) ||
    securityClient.Contains("AuthBasicAuthUsername", StringComparison.Ordinal) ||
    securityClient.Contains("AuthBasicAuthPassword", StringComparison.Ordinal))
    throw new Exception("Generated Basic auth must store only a private precomputed Authorization value.");

var publicStart = securityClient.IndexOf("Public Function PublicCall", StringComparison.Ordinal);
var publicEnd = securityClient.IndexOf("End Function", publicStart, StringComparison.Ordinal);
var publicSource = securityClient[publicStart..publicEnd];
if (publicSource.Contains("SetBearerToken", StringComparison.Ordinal) || publicSource.Contains("SetBasicAuth", StringComparison.Ordinal) || publicSource.Contains("X-API-Key", StringComparison.Ordinal))
    throw new Exception("security: [] must not inherit authentication.");

try
{
    _ = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Conflict, version: 1.0.0 }
components:
  securitySchemes:
    BearerAuth: { type: http, scheme: bearer }
    BasicAuth: { type: http, scheme: basic }
security:
  - BearerAuth: []
    BasicAuth: []
paths:
  /bad:
    get:
      operationId: bad
      responses:
        '200': { description: ok }
""", "conflict.yaml");
    throw new Exception("Bearer+Basic AND security must be rejected.");
}
catch (XpsOpenApiGenerationException ex) when (ex.Message.Contains("Authorization header", StringComparison.OrdinalIgnoreCase)) { }

var unicodeClient = new XpsOpenApiClientGenerator().Generate("""
openapi: 3.1.0
info: { title: Unicode, version: 1.0.0 }
paths:
  /cities/{city}:
    get:
      operationId: unicodePath
      parameters:
        - { name: city, in: path, required: true, schema: { type: string } }
        - { name: q, in: query, schema: { type: string } }
      responses:
        '204': { description: ok }
""", "unicode.yaml").Source;
if (!unicodeClient.Contains("Http.EncodePath(City)", StringComparison.Ordinal) || !unicodeClient.Contains("Http.AddQuery(url, \"q\", Q)", StringComparison.Ordinal))
    throw new Exception("OpenAPI Unicode path/query values must flow through XPHttp UTF-8 encoding helpers.");

if (clientResult.Source.Contains("UIForm", StringComparison.OrdinalIgnoreCase) || clientResult.Source.Contains("XPScriptHttpUiFormHelpers", StringComparison.Ordinal))
    throw new Exception("Generated OpenAPI client must not depend on UIForm runtime.");
foreach (var marker in new[] { "XPHttpClient", "XPHttpRequest", "XPHttpResponse", "XPJsonDocument", "Http.Send(request)" })
    if (!clientResult.Source.Contains(marker, StringComparison.Ordinal)) throw new Exception("Generated OpenAPI client is missing core API marker: " + marker);

if (result.OpenApiVersion != "3.1.0") throw new Exception("OpenAPI version was not retained.");
if (result.Operations.Count != 2 || !result.Operations.Contains("GetPet") || !result.Operations.Contains("CreatePet"))
    throw new Exception("Expected OpenAPI operations were not generated.");
if (!result.Models.Contains("Pet") || !result.Models.Contains("CreatePet") || !result.Models.Contains("ApiError"))
    throw new Exception("Expected OpenAPI component schemas were not generated.");

foreach (var marker in new[]
{
    "Public Class Pet",
    "[Required]",
    "[MaxLength:100]",
    "[Email]",
    "[Range:0;40]",
    "Public Class GetPetRequest",
    "Public Class GetPetResponse",
    "Function HandleGetPet(request As GetPetRequest) As GetPetResponse",
    "Function HandleCreatePet(request As CreatePetRequest) As CreatePetResponse",
    "Dim request As GetPetRequest",
    "Set request = New GetPetRequest",
    "Dim result As GetPetResponse",
    "Set result = New GetPetResponse",
    "Sub EndpointGetPet(",
    "Sub EndpointCreatePet(",
    "[Route:/pets/{petId}]",
    "[FromRoute:\"petId\"] pPetId As Long",
    "[FromQuery:\"includeHistory\"] pIncludeHistory As Boolean",
    "[FromHeader:\"X-Request-Id\"] pXRequestId As String",
    "[FromBody] payload As CreatePet",
    "Set result = HandleGetPet(request)",
    "Response.Json(result.StatusCode, result.Data)"
})
{
    if (!result.Source.Contains(marker, StringComparison.Ordinal))
        throw new Exception("Generated XPScript is missing expected marker: " + marker);
}

var openApi30 = generator.Generate("""
openapi: 3.0.3
info:
  title: Compatibility Smoke
  version: 1.0.0
paths:
  /health:
    get:
      operationId: health
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema:
                type: string
""", "openapi30.yaml");
if (openApi30.OpenApiVersion != "3.0.3" || !openApi30.Operations.Contains("Health"))
    throw new Exception("OpenAPI 3.0 compatibility generation failed.");

var root = Path.Combine(Path.GetTempPath(), "xps-openapi-generator-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var clientPath = Path.Combine(root, "petstore-client.xps");
    await File.WriteAllTextAsync(clientPath, clientResult.Source);

    var sourcePath = Path.Combine(root, "petstore.xps");
    await File.WriteAllTextAsync(sourcePath, result.Source);

    var parsed = new XpsWebRouteMetadataParser().Parse(result.Source);
    if (parsed.Routes["EndpointGetPet"].RouteTemplate != "/pets/{petId}")
        throw new Exception("Generated GET route metadata did not match the OpenAPI path.");
    if (parsed.Routes["EndpointGetPet"].ParameterBindings?.Count != 3)
        throw new Exception("Generated GET parameter bindings did not match the OpenAPI parameters.");
    if (parsed.Routes["EndpointCreatePet"].ParameterBindings?.Count != 1)
        throw new Exception("Generated POST body binding did not match the OpenAPI requestBody.");

    var compiler = new XpsWebCompiler();
    await using (var unit = await compiler.CompileAsync(sourcePath, root))
    {
        if (!unit.Routes.ContainsKey("EndpointGetPet") || !unit.Routes.ContainsKey("EndpointCreatePet"))
            throw new Exception("Generated XPScript did not compile into the expected REST routes.");
    }

    const string getPetOriginal = "    result.StatusCode = 501\n    HandleGetPet = result";
    const string getPetEdited = "    Print \"fråga funktionen GetPet\"\n    result.StatusCode = 200\n    result.Data = \"custom-get\"\n    HandleGetPet = result";
    const string createPetOriginal = "    result.StatusCode = 501\n    HandleCreatePet = result";
    const string createPetEdited = "    Print \"fråga funktionen CreatePet\"\n    result.StatusCode = 201\n    result.Data = \"custom-create\"\n    HandleCreatePet = result";

    var userEdited = result.Source
        .Replace(getPetOriginal, getPetEdited, StringComparison.Ordinal)
        .Replace(createPetOriginal, createPetEdited, StringComparison.Ordinal);

    foreach (var printMarker in new[]
    {
        "Print \"fråga funktionen GetPet\"",
        "Print \"fråga funktionen CreatePet\""
    })
    {
        if (!userEdited.Contains(printMarker, StringComparison.Ordinal))
            throw new Exception("Smoke setup failed to add generated handler print line: " + printMarker);
    }

    await File.WriteAllTextAsync(sourcePath, userEdited);
    await using (var editedUnit = await compiler.CompileAsync(sourcePath, root))
    {
        if (!editedUnit.Routes.ContainsKey("EndpointGetPet") || !editedUnit.Routes.ContainsKey("EndpointCreatePet"))
            throw new Exception("Manually edited generated XPScript did not compile before reimport.");
    }

    var importResult = new XpsOpenApiImporter().ImportFile(reimportFixture, userEdited);

    foreach (var preserved in new[]
    {
        "Print \"fråga funktionen GetPet\"",
        "Print \"fråga funktionen CreatePet\"",
        "result.Data = \"custom-get\"",
        "result.Data = \"custom-create\"",
        "Public name As String",
        "Sub EndpointGetPet([FromRoute:\"petId\"] pPetId As Long, [FromQuery:\"includeHistory\"] pIncludeHistory As Boolean, [FromHeader:\"X-Request-Id\"] pXRequestId As String)"
    })
    {
        if (!importResult.Source.Contains(preserved, StringComparison.Ordinal))
            throw new Exception("Additive import changed or removed existing source: " + preserved);
    }

    foreach (var added in new[]
    {
        "Public microchip As String",
        "Public status As String",
        "Public source As String",
        "Public externalId As String",
        "Public traceId As String",
        "Public Expand As String",
        "Public Class UpdatePet",
        "Public Class UpdatePetRequest",
        "Public Class UpdatePetResponse",
        "Function HandleUpdatePet(request As UpdatePetRequest) As UpdatePetResponse",
        "Sub EndpointUpdatePet(",
        "Function HandleListPets(request As ListPetsRequest) As ListPetsResponse",
        "Sub EndpointListPets(",
        "Function HandleDeletePet(request As DeletePetRequest) As DeletePetResponse",
        "Sub EndpointDeletePet(",
        "Sub WriteUpdatePetResponse(result As UpdatePetResponse)",
        "Sub WriteListPetsResponse(result As ListPetsResponse)",
        "Sub WriteDeletePetResponse(result As DeletePetResponse)"
    })
    {
        if (!importResult.Source.Contains(added, StringComparison.Ordinal))
            throw new Exception("Additive import did not add expected declaration: " + added);
    }

    if (importResult.Source.Contains("pExpand As String", StringComparison.Ordinal))
        throw new Exception("Additive import rewrote the existing GetPet endpoint signature.");
    if (!importResult.Warnings.Any(warning => warning.Contains("Pet.name", StringComparison.OrdinalIgnoreCase)))
        throw new Exception("Expected changed existing property type to produce a drift warning.");
    if (!importResult.Warnings.Any(warning => warning.Contains("EndpointGetPet", StringComparison.Ordinal)))
        throw new Exception("Expected changed existing endpoint signature to produce a drift warning.");

    var importedPath = Path.Combine(root, "petstore-imported.xps");
    await File.WriteAllTextAsync(importedPath, importResult.Source);
    await using (var importedUnit = await compiler.CompileAsync(importedPath, root))
    {
        foreach (var route in new[]
        {
            "EndpointGetPet",
            "EndpointCreatePet",
            "EndpointUpdatePet",
            "EndpointListPets",
            "EndpointDeletePet"
        })
        {
            if (!importedUnit.Routes.ContainsKey(route))
                throw new Exception("Additively imported XPScript did not compile expected REST route: " + route);
        }
    }

    foreach (var marker in new[] { "OPENAPI-CLIENT-SECURITY=OK", "OPENAPI-CLIENT-CORE-ONLY=OK" })
        Console.WriteLine(marker);
    Console.WriteLine("OPENAPI-CLIENT-COMPILE=OK");
    Console.WriteLine("OPENAPI-3.0-GENERATOR=OK");
    Console.WriteLine("OPENAPI-3.1-YAML-GENERATOR=OK");
    Console.WriteLine("OPENAPI-GENERATED-XPS-COMPILE=OK");
    Console.WriteLine("OPENAPI-EDITED-HANDLERS-COMPILE=OK");
    Console.WriteLine("OPENAPI-PRINT-PRESERVATION=OK");
    Console.WriteLine("OPENAPI-ADDITIVE-REIMPORT-PRESERVE=OK");
    Console.WriteLine("OPENAPI-ADDITIVE-REIMPORT-NEW-OPERATIONS=OK");
    Console.WriteLine("OPENAPI-ADDITIVE-REIMPORT-COMPILE=OK");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}
