#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

fetch() {
  local name="$1"
  local url="$2"
  echo "Fetching $name"
  curl --fail --location --retry 3 --retry-delay 2 --connect-timeout 20 "$url" -o "$name"
}

fetch github.json "https://raw.githubusercontent.com/github/rest-api-description/main/descriptions/api.github.com/api.github.com.json"
fetch stripe.json "https://raw.githubusercontent.com/stripe/openapi/master/openapi/spec3.json"
fetch digitalocean.yaml "https://api-engineering.nyc3.digitaloceanspaces.com/spec-ci/DigitalOcean-public.v2.yaml"
fetch openai.yaml "https://raw.githubusercontent.com/openai/openai-openapi/master/openapi.yaml"
fetch scb.json "https://statistikdatabasen.scb.se/swagger/v2/swagger.json"
fetch skogsstyrelsen.json "https://api.skogsstyrelsen.se/sksapi/swagger/skogliga%20grunddata_v1.0/openapi.json"
fetch riksantikvarieambetet.yaml "https://pub.raa.se/datauttag/swagger/swagger.yaml"
fetch vinnova.yaml "https://gdpswagger.vinnova.se/oas/openapi.yaml"
fetch swedac.json "https://api2.swedac.se/openapi/v1.json"

echo "OpenAPI specs downloaded."
