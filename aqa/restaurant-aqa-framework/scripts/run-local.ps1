param(
    [string]$Dotnet = 'dotnet',
    [string]$CredentialFile,
    [string]$Filter = '/*/*/*/*[Category=hw8]',
    [string]$Results = 'TestResults/local'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    if ($CredentialFile) {
        foreach ($identity in (Import-Clixml -LiteralPath $CredentialFile)) {
            $prefix = $identity.Role.ToUpperInvariant()
            if ($prefix -notin @('OWNER', 'OTHER', 'WAITER')) { continue }
            [Environment]::SetEnvironmentVariable($prefix + '_USERNAME', $identity.Credential.UserName, 'Process')
            [Environment]::SetEnvironmentVariable($prefix + '_PASSWORD', $identity.Credential.GetNetworkCredential().Password, 'Process')
        }
    }
    $env:ARTIFACTS_DIR = [IO.Path]::GetFullPath($Results)
    & $Dotnet build --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & $Dotnet bin/Debug/net10.0/restaurant-aqa-framework.dll --report-trx --results-directory $env:ARTIFACTS_DIR --treenode-filter $Filter
    $testExit = $LASTEXITCODE
    exit $testExit
} finally {
    foreach ($role in @('OWNER','OTHER','WAITER')) {
        [Environment]::SetEnvironmentVariable($role + '_USERNAME', $null, 'Process')
        [Environment]::SetEnvironmentVariable($role + '_PASSWORD', $null, 'Process')
    }
    Pop-Location
}
