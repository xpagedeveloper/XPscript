using XPScript.Web.Compiler;
using XPScript.Compiler;

const string modelArraysOpenApi = """
openapi: 3.0.3
info: { title: Model arrays, version: 1.0.0 }
paths:
  /models:
    get:
      operationId: getModels
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Envelope' }
components:
  schemas:
    Child:
      type: object
      properties:
        name: { type: string }
    Envelope:
      type: object
      properties:
        children: { type: array, items: { $ref: '#/components/schemas/Child' } }
        names: { type: array, items: { type: string } }
        counts: { type: array, items: { type: integer, format: int32 } }
        error: { $ref: '#/components/schemas/error' }
        state: { $ref: '#/components/schemas/State' }
    error:
      type: object
      properties:
        children: { type: string }
    State:
      type: string
      enum: [ERROR, CHILDREN, active]
""";
var arrayRoot = Path.Combine(Path.GetTempPath(), "openapi-model-arrays-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(arrayRoot);
try
{
    var client = new XpsOpenApiClientGenerator().Generate(modelArraysOpenApi, "model-arrays.yaml");
    var sourcePath = Path.Combine(arrayRoot, "client.xps");
    await File.WriteAllTextAsync(sourcePath, client.Source + "\nSub Main()\nEnd Sub\n");
    var compiled = await new CompilerDriver().CompileWithResultAsync(sourcePath, Path.Combine(arrayRoot, "client"), false);
    if (!compiled.Success)
        throw new Exception("Generated model arrays must compile through the CLI compiler: " + System.Text.Json.JsonSerializer.Serialize(compiled));
    Console.WriteLine("OPENAPI-CLIENT-MODEL-ARRAYS-COMPILE=OK");
    Console.WriteLine("OPENAPI-CLIENT-ENUM-MODEL-SCOPE-COMPILE=OK");
}
finally
{
    Directory.Delete(arrayRoot, recursive: true);
}

const string referencedModelOpenApi = """
openapi: 3.0.3
info: { title: Referenced model names, version: 1.0.0 }
paths:
  /models:
    post:
      operationId: createModels
      requestBody:
        content:
          application/json:
            schema: { $ref: '#/components/schemas/clear_actor_limits' }
      responses:
        '200':
          description: ok
          content:
            application/json:
              schema: { $ref: '#/components/schemas/default' }
components:
  schemas:
    clear_actor_limits:
      type: object
      properties:
        connection: { $ref: '#/components/schemas/connection_create' }
        reserved: { $ref: '#/components/schemas/default' }
        escaped: { $ref: '#/components/schemas/request~1payload' }
    connection_create:
      type: object
      properties:
        name: { type: string }
    default:
      type: object
      properties:
        value: { type: string }
    request/payload:
      type: object
      properties:
        name: { type: string }
""";

var referencedModelRoot = Path.Combine(Path.GetTempPath(), "openapi-model-names-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(referencedModelRoot);
try
{
    var modelServer = new XpsOpenApiGenerator().Generate(referencedModelOpenApi, "referenced-models.yaml");
    foreach (var marker in new[] { "Public Class clear_actor_limits", "Public connection As connection_create", "Public reserved As ApiDefault", "Public escaped As RequestPayload", "Public Payload As clear_actor_limits" })
        if (!modelServer.Source.Contains(marker, StringComparison.Ordinal))
            throw new Exception("Server schema references must use the declared model name: " + marker);
    var imported = new XpsOpenApiImporter().Import(referencedModelOpenApi, "", "referenced-models.yaml");
    foreach (var (name, source) in new[] { ("generated", modelServer.Source), ("imported", imported.Source) })
    {
        var path = Path.Combine(referencedModelRoot, name + ".xps");
        await File.WriteAllTextAsync(path, source);
        await using var unit = await new XpsWebCompiler().CompileAsync(path, referencedModelRoot);
    }
    var modelClient = new XpsOpenApiClientGenerator().Generate(referencedModelOpenApi, "referenced-models.yaml");
    foreach (var marker in new[] { "Public Class clear_actor_limits", "As connection_create", "As default", "As RequestPayload" })
        if (!modelClient.Source.Contains(marker, StringComparison.Ordinal))
            throw new Exception("Client schema references must use the declared model name: " + marker);
    _ = new XPScriptTranspiler().TranspileRestricted(
        modelClient.Source + "\nSub Main()\nEnd Sub\n",
        Path.Combine(referencedModelRoot, "client.xps"),
        CompilerDriver.CurrentRuntimeIdentifier(), [referencedModelRoot]);
    Console.WriteLine("OPENAPI-SERVER-REFERENCED-MODEL-NAMES=OK");
    Console.WriteLine("OPENAPI-CLIENT-REFERENCED-MODEL-NAMES=OK");
}
finally
{
    Directory.Delete(referencedModelRoot, recursive: true);
}

const string openApi = """
openapi: 3.0.3
info:
  title: Composed request body assignment regression
  version: 1.0.0
paths:
  /widgets:
    post:
      operationId: createWidget
      requestBody:
        required: true
        content:
          application/json:
            schema:
              allOf:
                - $ref: '#/components/schemas/WidgetPayload'
      responses:
        '200':
          description: ok
components:
  schemas:
    WidgetPayload:
      type: object
      properties:
        name:
          type: string
""";

var generated = new XpsOpenApiGenerator().Generate(openApi, "composed-request-body.yaml");
if (!generated.Source.Contains("Set request.Payload = payload", StringComparison.Ordinal))
    throw new Exception("Composed model request bodies must use Set when assigned to the generated request object.");

Console.WriteLine("OPENAPI-SERVER-COMPOSED-REQUEST-BODY-SET=OK");

const string csharpKeywordOpenApi = """
openapi: 3.0.3
info:
  title: CSharp keyword identifier regression
  version: 1.0.0
paths:
  /keywords:
    get:
      operationId: operator
      parameters:
        - name: namespace
          in: query
          schema:
            type: string
      responses:
        '200':
          description: ok
components:
  schemas:
    default:
      type: object
      properties:
        operator:
          type: string
        namespace:
          type: string
        default:
          type: string
""";

var keywordGenerated = new XpsOpenApiGenerator().Generate(csharpKeywordOpenApi, "csharp-keywords.yaml");
foreach (var forbidden in new[] { "Public Class default", "Public operator As", "Public namespace As", "Public default As", "Sub Endpointoperator(" })
    if (keywordGenerated.Source.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
        throw new Exception("OpenAPI identifiers that are C# keywords must be renamed before XPScript-to-C# compilation: " + forbidden);

var keywordPath = Path.Combine(Path.GetTempPath(), "openapi-csharp-keywords-server.xps");
await File.WriteAllTextAsync(keywordPath, keywordGenerated.Source);
await using (var keywordUnit = await new XpsWebCompiler().CompileAsync(keywordPath, Path.GetTempPath()))
{
}
Console.WriteLine("OPENAPI-SERVER-CSHARP-KEYWORDS=OK");
