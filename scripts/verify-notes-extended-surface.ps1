$ErrorActionPreference = 'Stop'
dotnet build ./src/XPScript.Compiler/XPScript.Compiler.csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Compiler build failed.' }
./scripts/compile-notes-extended-samples.ps1
