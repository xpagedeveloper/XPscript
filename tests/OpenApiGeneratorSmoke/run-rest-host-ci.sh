#!/usr/bin/env bash
set -euo pipefail

mkdir -p ./out

# Run focused model-name, keyword and request-body regressions first
# before the full DigitalOcean generation and compilation.
dotnet run --project ./tests/OpenApiRequestBodyAssignmentSmoke/OpenApiRequestBodyAssignmentSmoke.csproj -c Release
dotnet run --project ./tests/OpenApiGeneratorSmoke/OpenApiGeneratorSmoke.csproj -c Release -- --regeneration-only

# The checked-in DigitalOcean fixture is the bundled definition. Keep test input
# deterministic instead of replacing it with a moving upstream download.

# REST output is web XPScript: Response, route metadata and parameter bindings are
# provided by XpsWebCompiler. The OpenApiGeneratorSmoke project already compiles
# generated and imported sources through that real web compiler. Keep this CI
# runner focused on exercising the CLI file paths before invoking the web smoke.

dotnet run --project ./src/XPScript.Cli/XPScript.Cli.csproj -c Release -p:SkipUnifiedPublish=true -- openapi generate ./tests/OpenApiGeneratorSmoke/petstore.yaml -o ./out/generated-openapi.xps --force
test -f ./out/generated-openapi.xps

dotnet run --project ./src/XPScript.Cli/XPScript.Cli.csproj -c Release -p:SkipUnifiedPublish=true -- openapi generate ./tests/OpenApiGeneratorSmoke/petstore.json -o ./out/generated-openapi-json.xps --force
test -f ./out/generated-openapi-json.xps

digitalocean_out=$(mktemp -d ./out/digitalocean-cli-XXXXXX)
dotnet run --project ./src/XPScript.Cli/XPScript.Cli.csproj -c Release -p:SkipUnifiedPublish=true -- openapi generate ./test/openapi/digitalocean.yaml -o "$digitalocean_out/full-server.xps"
dotnet run --project ./src/XPScript.Cli/XPScript.Cli.csproj -c Release -p:SkipUnifiedPublish=true -- openapi client generate ./test/openapi/digitalocean.yaml -o "$digitalocean_out/full-client.xps"

# The complete client fits the compiler's 16 MiB input limit. Keep the smaller
# selector as a fast representative check for the model and import regressions.
dotnet run --project ./tests/OpenApiGeneratorSmoke/OpenApiGeneratorSmoke.csproj -c Release -- --write-digitalocean-regression "$digitalocean_out/regression.yaml"
dotnet run --project ./src/XPScript.Cli/XPScript.Cli.csproj -c Release -p:SkipUnifiedPublish=true -- openapi generate "$digitalocean_out/regression.yaml" -o "$digitalocean_out/server.xps"
dotnet run --project ./src/XPScript.Cli/XPScript.Cli.csproj -c Release -p:SkipUnifiedPublish=true -- openapi import "$digitalocean_out/regression.yaml" -o "$digitalocean_out/imported-server.xps"
dotnet run --project ./src/XPScript.Cli/XPScript.Cli.csproj -c Release -p:SkipUnifiedPublish=true -- openapi client generate "$digitalocean_out/regression.yaml" -o "$digitalocean_out/client.xps"
cp "$digitalocean_out/client.xps" "$digitalocean_out/client-host.xps"
printf '\nSub Main()\nEnd Sub\n' >> "$digitalocean_out/client-host.xps"
dotnet run --project ./src/XPScript.Cli/XPScript.Cli.csproj -c Release -p:SkipUnifiedPublish=true -- compile "$digitalocean_out/client-host.xps" -o "$digitalocean_out/client-host" --runtime false
echo "OPENAPI-DIGITALOCEAN-CLI-GENERATE-IMPORT-COMPILE=OK"

dotnet run --project ./tests/OpenApiGeneratorSmoke/OpenApiGeneratorSmoke.csproj -c Release
