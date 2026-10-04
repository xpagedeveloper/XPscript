#!/usr/bin/env bash
set -euo pipefail

mkdir -p ./out

# DigitalOcean's repository source is multi-file. Test the official bundled artifact
# that DigitalOcean publishes from the same source tree so every external $ref is present.
curl --fail --location --retry 3 --retry-delay 2 \
  "https://api-engineering.nyc3.digitaloceanspaces.com/spec-ci/DigitalOcean-public.v2.yaml" \
  -o ./test/openapi/digitalocean.yaml

# REST output is web XPScript: Response, route metadata and parameter bindings are
# provided by XpsWebCompiler. The OpenApiGeneratorSmoke project already compiles
# generated and imported sources through that real web compiler. Keep this CI
# runner focused on exercising the CLI file paths before invoking the web smoke.

dotnet run --project ./src/XPScript.Cli/XPScript.Cli.csproj -c Release -p:SkipUnifiedPublish=true -- openapi generate ./tests/OpenApiGeneratorSmoke/petstore.yaml -o ./out/generated-openapi.xps --force
test -f ./out/generated-openapi.xps

dotnet run --project ./src/XPScript.Cli/XPScript.Cli.csproj -c Release -p:SkipUnifiedPublish=true -- openapi generate ./tests/OpenApiGeneratorSmoke/petstore.json -o ./out/generated-openapi-json.xps --force
test -f ./out/generated-openapi-json.xps

# XpsWebCompiler wraps CompilerException. Temporarily adapt the smoke source so the
# focused CI job reports the exact generated DigitalOcean line instead of only the
# outer web-compilation exception. Remove this once the current regression is fixed.
python3 - <<'PY'
from pathlib import Path
path = Path("tests/OpenApiGeneratorSmoke/Program.cs")
text = path.read_text()
old = "catch (CompilerException ex) when (ex.GeneratedDiagnostics.Count > 0)"
new = "catch (XpsWebCompilationException webEx) when (webEx.InnerException is CompilerException ex && ex.GeneratedDiagnostics.Count > 0)"
# Only the first catch is the web-server compile. The later client catch must stay direct.
text = text.replace(old, new, 1)
path.write_text(text)
PY

dotnet run --project ./tests/OpenApiGeneratorSmoke/OpenApiGeneratorSmoke.csproj -c Release
