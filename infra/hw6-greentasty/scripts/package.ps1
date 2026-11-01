param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'dotnet/APIHandler.csproj'
$build = Join-Path $root 'build'
# Use a fresh publish directory to avoid stale libraries in a subsequent package.
$publish = Join-Path $build ('publish-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $publish | Out-Null
& $Dotnet publish $project -c Release -r linux-x64 --self-contained false -p:PublishReadyToRun=false -p:DebugType=None -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
foreach ($file in @('APIHandler.dll', 'APIHandler.deps.json', 'APIHandler.runtimeconfig.json')) {
    if (!(Test-Path (Join-Path $publish $file))) { throw "Missing Lambda package file: $file" }
}
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath (Join-Path $build 'api-handler.zip') -Force
Get-FileHash (Join-Path $build 'api-handler.zip') -Algorithm SHA256
