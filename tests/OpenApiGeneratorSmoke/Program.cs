using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var fixture = Path.Combine(AppContext.BaseDirectory, "petstore.yaml");
var reimportFixture = Path.Combine(AppContext.BaseDirectory, "petstore-reimport.yaml");
var generator = new XpsOpenApiGenerator();
var result = generator.GenerateFile(fixture);

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

    var userEdited = result.Source
        .Replace(
            "    ' TODO: implement this operation and set result.StatusCode/result.Data.\n    result.StatusCode = 501\n    HandleGetPet = result",
            "    ' USER HANDLER CODE MUST SURVIVE REIMPORT\n    result.StatusCode = 200\n    result.Data = \"custom\"\n    HandleGetPet = result",
            StringComparison.Ordinal)
        .Replace(
            "    Public Name As String\n\n    [Email]",
            "    Public Name As String\n\n    Public UserOwned As String\n\n    [Email]",
            StringComparison.Ordinal);

    if (!userEdited.Contains("USER HANDLER CODE MUST SURVIVE REIMPORT", StringComparison.Ordinal) ||
        !userEdited.Contains("Public UserOwned As String", StringComparison.Ordinal))
        throw new Exception("Smoke setup failed to create user-owned edits.");

    var importResult = new XpsOpenApiImporter().ImportFile(reimportFixture, userEdited);

    foreach (var preserved in new[]
    {
        "' USER HANDLER CODE MUST SURVIVE REIMPORT",
        "result.Data = \"custom\"",
        "Public UserOwned As String",
        "Public Name As String",
        "Sub EndpointGetPet([FromRoute:\"petId\"] pPetId As Long, [FromQuery:\"includeHistory\"] pIncludeHistory As Boolean, [FromHeader:\"X-Request-Id\"] pXRequestId As String)"
    })
    {
        if (!importResult.Source.Contains(preserved, StringComparison.Ordinal))
            throw new Exception("Additive import changed or removed existing source: " + preserved);
    }

    foreach (var added in new[]
    {
        "Public Microchip As String",
        "Public Source As String",
        "Public TraceId As String",
        "Public Expand As String",
        "Public Class UpdatePet",
        "Public Class UpdatePetRequest",
        "Public Class UpdatePetResponse",
        "Function HandleUpdatePet(request As UpdatePetRequest) As UpdatePetResponse",
        "Sub EndpointUpdatePet(",
        "Sub WriteUpdatePetResponse(result As UpdatePetResponse)"
    })
    {
        if (!importResult.Source.Contains(added, StringComparison.Ordinal))
            throw new Exception("Additive import did not add expected declaration: " + added);
    }

    if (importResult.Source.Contains("pExpand As String", StringComparison.Ordinal))
        throw new Exception("Additive import rewrote the existing GetPet endpoint signature.");
    if (!importResult.Warnings.Any(warning => warning.Contains("Pet.Name", StringComparison.Ordinal)))
        throw new Exception("Expected changed existing property type to produce a drift warning.");
    if (!importResult.Warnings.Any(warning => warning.Contains("EndpointGetPet", StringComparison.Ordinal)))
        throw new Exception("Expected changed existing endpoint signature to produce a drift warning.");

    var importedPath = Path.Combine(root, "petstore-imported.xps");
    await File.WriteAllTextAsync(importedPath, importResult.Source);
    await using (var importedUnit = await compiler.CompileAsync(importedPath, root))
    {
        if (!importedUnit.Routes.ContainsKey("EndpointGetPet") ||
            !importedUnit.Routes.ContainsKey("EndpointCreatePet") ||
            !importedUnit.Routes.ContainsKey("EndpointUpdatePet"))
            throw new Exception("Additively imported XPScript did not compile into all expected REST routes.");
    }

    Console.WriteLine("OPENAPI-3.0-GENERATOR=OK");
    Console.WriteLine("OPENAPI-3.1-YAML-GENERATOR=OK");
    Console.WriteLine("OPENAPI-GENERATED-XPS-COMPILE=OK");
    Console.WriteLine("OPENAPI-ADDITIVE-REIMPORT-PRESERVE=OK");
    Console.WriteLine("OPENAPI-ADDITIVE-REIMPORT-COMPILE=OK");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}
