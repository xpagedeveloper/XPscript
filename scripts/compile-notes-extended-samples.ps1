$ErrorActionPreference = 'Stop'

$project = './src/XPScript.Compiler/XPScript.Compiler.csproj'
$samples = @(
  'samples/notes-note-collection-runtime-test.xps',
  'samples/notes-note-collection-dxl-runtime-test.xps',
  'samples/notes-stream-runtime-test.xps',
  'samples/notes-agent-runtime-test.xps',
  'samples/notes-extended-surface.xps'
)

New-Item -ItemType Directory -Force './out/notes-extended' | Out-Null
foreach ($sample in $samples) {
  $name = [IO.Path]::GetFileNameWithoutExtension($sample)
  dotnet run --project $project -c Release -- $sample --framework-dependent -o "./out/notes-extended/$name"
  if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $sample" }
}
