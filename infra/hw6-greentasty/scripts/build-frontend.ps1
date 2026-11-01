# Local build only; requires the real output after a separately approved deployment.
param([string]$Npm = 'npm')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$repository = Split-Path (Split-Path $root -Parent) -Parent
$endpoint = & terraform "-chdir=$root" output -raw backend_url
if ($LASTEXITCODE -ne 0 -or $endpoint -notmatch '^https://[a-z0-9]{10}\.execute-api\.eu-north-1\.amazonaws\.com/demo$' -or $endpoint -match '0000000000') {
    throw 'A real hw6 backend_url Terraform output is required. Do not publish a validation bundle.'
}
$env:BACKEND_URL = $endpoint
Push-Location (Join-Path $repository 'frontend/restaurant-frontend-app')
try {
    & $Npm ci
    if ($LASTEXITCODE -ne 0) { throw 'npm ci failed' }
    & $Npm run build
    if ($LASTEXITCODE -ne 0) { throw 'Production build failed' }
} finally { Pop-Location }
