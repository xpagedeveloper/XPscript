using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var fixture = Path.Combine(AppContext.BaseDirectory, "petstore.yaml");
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
    "[Route:/pets/{petId}]",
    "[FromRoute:\"petId\"] pPetId As Long",
    "[FromQuery:\"includeHistory\"] pIncludeHistory As Boolean",
    "[FromHeader:\"X-Request-Id\"] pXRequestId As String",
    "[FromBody] payload As CreatePet",
    "Response.Json(result.StatusCode, result.Data)"
})
{
    if (!result.Source.Contains(marker, StringComparison.Ordinal))
        throw new Exception("Generated XPScript is missing expected marker: " + marker);
}

var root = Path.Combine(Path.GetTempPath(), "xps-openapi-generator-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var sourcePath = Path.Combine(root, "petstore.xps");
    await File.WriteAllTextAsync(sourcePath, result.Source);

    var parsed = new XpsWebRouteMetadataParser().Parse(result.Source);
    if (parsed.Routes["GetPet"].RouteTemplate != "/pets/{petId}")
        throw new Exception("Generated GET route metadata did not match the OpenAPI path.");
    if (parsed.Routes["GetPet"].ParameterBindings?.Count != 3)
        throw new Exception("Generated GET parameter bindings did not match the OpenAPI parameters.");
    if (parsed.Routes["CreatePet"].ParameterBindings?.Count != 1)
        throw new Exception("Generated POST body binding did not match the OpenAPI requestBody.");

    var compiler = new XpsWebCompiler();
    await using var unit = await compiler.CompileAsync(sourcePath, root);
    if (!unit.Routes.ContainsKey("GetPet") || !unit.Routes.ContainsKey("CreatePet"))
        throw new Exception("Generated XPScript did not compile into the expected REST routes.");

    Console.WriteLine("OPENAPI-YAML-GENERATOR=OK");
    Console.WriteLine("OPENAPI-GENERATED-XPS-COMPILE=OK");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}
