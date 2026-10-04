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
