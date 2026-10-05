using XPScript.Web.Compiler;

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
