# Dot-source this file to select the verified runtimes for the current shell only.
$projectRoot = Split-Path $PSScriptRoot -Parent
$baseline = Get-Content (Join-Path $PSScriptRoot 'runtime-baseline.json') -Raw | ConvertFrom-Json
$dotnetDirectory = Join-Path $projectRoot '.dotnet'
$nodeDirectory = Join-Path $projectRoot '.node'
if (-not (Test-Path (Join-Path $dotnetDirectory "sdk/$($baseline.DotNet.Sdk)")) -or
    -not (Test-Path (Join-Path $dotnetDirectory "shared/Microsoft.AspNetCore.App/$($baseline.DotNet.Runtime)")) -or
    -not (Test-Path (Join-Path $nodeDirectory 'node.exe'))) {
    throw 'Project runtimes are missing. Run tools/install-project-runtimes.ps1 first.'
}
if ((& (Join-Path $nodeDirectory 'node.exe') --version) -ne "v$($baseline.Node.Version)") {
    throw 'Project Node version does not match the reviewed baseline.'
}
$env:DOTNET_ROOT = $dotnetDirectory
$env:DOTNET_ROOT_X64 = $dotnetDirectory
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:PATH = "$dotnetDirectory;$nodeDirectory;$env:PATH"
