#!/usr/bin/env bash
set -euo pipefail

mkdir -p ./out

compile_server_host() {
  local source="$1"
  local host="$2"
  local output="$3"

  # Standalone web route metadata belongs to the web host, not the core compiler.
  # Keep inline parameter metadata such as [FromBody] intact.
  # Strip standalone web/validation metadata that the core compiler does not own.
  # Inline parameter bindings such as [FromBody] remain part of their declaration.
  grep -Ev '^[[:space:]]*\[(Anonymous|Authorize|Required|Route|Get|Post|Put|Patch|Delete|Head|Options|Trace)(\([^]]*\))?\][[:space:]]*
  printf '\nSub Main()\nEnd Sub\n' >> "$host"

  dotnet run --project ./src/XPScript.Compiler/XPScript.Compiler.csproj -c Release --no-build -- "$host" -o "$output" --runtime=false
}

dotnet run --project ./src/XPScript.Cli/XPScript.Cli.csproj -c Release -p:SkipUnifiedPublish=true -- openapi generate ./tests/OpenApiGeneratorSmoke/petstore.yaml -o ./out/generated-openapi.xps --force
test -f ./out/generated-openapi.xps
compile_server_host ./out/generated-openapi.xps ./out/generated-openapi-server-host.xps ./out/generated-openapi-server

dotnet run --project ./src/XPScript.Cli/XPScript.Cli.csproj -c Release -p:SkipUnifiedPublish=true -- openapi import ./tests/OpenApiGeneratorSmoke/petstore-reimport.yaml -o ./out/generated-openapi.xps
test -f ./out/generated-openapi.xps
compile_server_host ./out/generated-openapi.xps ./out/imported-openapi-server-host.xps ./out/imported-openapi-server

dotnet run --project ./src/XPScript.Cli/XPScript.Cli.csproj -c Release -p:SkipUnifiedPublish=true -- openapi generate ./tests/OpenApiGeneratorSmoke/petstore.json -o ./out/generated-openapi-json.xps --force
test -f ./out/generated-openapi-json.xps
compile_server_host ./out/generated-openapi-json.xps ./out/generated-openapi-json-server-host.xps ./out/generated-openapi-json-server
 "$source" > "$host"
  printf '\nSub Main()\nEnd Sub\n' >> "$host"

  dotnet run --project ./src/XPScript.Compiler/XPScript.Compiler.csproj -c Release --no-build -- "$host" -o "$output" --runtime=false
}

dotnet run --project ./src/XPScript.Cli/XPScript.Cli.csproj -c Release -p:SkipUnifiedPublish=true -- openapi generate ./tests/OpenApiGeneratorSmoke/petstore.yaml -o ./out/generated-openapi.xps --force
test -f ./out/generated-openapi.xps
compile_server_host ./out/generated-openapi.xps ./out/generated-openapi-server-host.xps ./out/generated-openapi-server

dotnet run --project ./src/XPScript.Cli/XPScript.Cli.csproj -c Release -p:SkipUnifiedPublish=true -- openapi import ./tests/OpenApiGeneratorSmoke/petstore-reimport.yaml -o ./out/generated-openapi.xps
test -f ./out/generated-openapi.xps
compile_server_host ./out/generated-openapi.xps ./out/imported-openapi-server-host.xps ./out/imported-openapi-server

dotnet run --project ./src/XPScript.Cli/XPScript.Cli.csproj -c Release -p:SkipUnifiedPublish=true -- openapi generate ./tests/OpenApiGeneratorSmoke/petstore.json -o ./out/generated-openapi-json.xps --force
test -f ./out/generated-openapi-json.xps
compile_server_host ./out/generated-openapi-json.xps ./out/generated-openapi-json-server-host.xps ./out/generated-openapi-json-server
