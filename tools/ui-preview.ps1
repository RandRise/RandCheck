[CmdletBinding()]
param([ValidateSet('start','stop','status')][string]$Action = 'start', [switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
$previewRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$previewUrl = 'http://127.0.0.1:3410'
$previewOutput = Join-Path $previewRoot 'outputs/ui-preview'
$previewMetadata = Join-Path $previewOutput 'runtime.json'
$sha = [Security.Cryptography.SHA256]::Create()
try { $workspace = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($previewRoot.ToLowerInvariant())))).Replace('-', '').ToLowerInvariant() } finally { $sha.Dispose() }
function Get-PreviewHealth {
    try { return Invoke-RestMethod "$previewUrl/__preview/health" -TimeoutSec 2 } catch { return $null }
}
function Assert-OurPreview($health) {
    if ($health.kind -ne 'uaedi-ui-preview' -or $health.workspace -ne $workspace) { throw 'Port 3410 belongs to another service or workspace. Nothing was stopped.' }
}
$health = Get-PreviewHealth
if ($Action -eq 'status') {
    if ($health) { Assert-OurPreview $health; Write-Output "Preview running: $previewUrl (server memory: $($health.memoryMb) MB)" }
    else { Write-Output 'Preview is not running.' }
    return
}
if ($Action -eq 'stop') {
    if (!$health) { Write-Output 'Preview is not running. No process was stopped.'; return }
    Assert-OurPreview $health
    if (!(Test-Path -LiteralPath $previewMetadata)) { throw 'Preview ownership file is missing; refusing to stop an unidentified process.' }
    $owner = Get-Content -LiteralPath $previewMetadata -Raw | ConvertFrom-Json
    if ($owner.workspace -ne $workspace -or $owner.pid -ne $health.pid) { throw 'Preview ownership does not match. Nothing was stopped.' }
    $null = Invoke-WebRequest "$previewUrl/__preview/stop" -Method Post -Headers @{ Authorization = "Bearer $($owner.controlToken)" } -UseBasicParsing -TimeoutSec 5
    for ($attempt = 0; $attempt -lt 30; $attempt++) { if (!(Get-PreviewHealth)) { Write-Output 'Preview stopped. Sample changes were discarded.'; return }; Start-Sleep -Milliseconds 200 }
    throw 'Preview did not finish stopping. Check outputs/ui-preview/server.err.log.'
}
if ($health) {
    Assert-OurPreview $health
    Write-Output "Preview already running: $previewUrl. Existing sample changes preserved."
    if (!$NoBrowser) { Start-Process $previewUrl }
    return
}
# Probe first; never kill an occupied port or launch a second copy over it.
$probe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 3410)
try { $probe.Start() } catch { throw 'Port 3410 is occupied. Close its owning application or stop the existing preview; no process was killed.' } finally { $probe.Stop() }
$node = Join-Path $previewRoot '.node/node.exe'
if (!(Test-Path -LiteralPath $node)) { throw 'Project Node is missing. Run tools/install-project-runtimes.ps1 once.' }
$baseline = Get-Content (Join-Path $PSScriptRoot 'runtime-baseline.json') -Raw | ConvertFrom-Json
if ((& $node --version) -ne "v$($baseline.Node.Version)") { throw 'Project Node differs from the reviewed runtime baseline.' }
foreach ($dependency in @('docs/node_modules/vite/package.json', 'apps/web/node_modules/react/package.json', 'apps/web/node_modules/@azure/msal-browser/package.json')) {
    if (!(Test-Path -LiteralPath (Join-Path $previewRoot $dependency))) { throw 'Install dependencies once: use the project Node runtime, then npm ci --prefix apps/web and npm ci --prefix docs.' }
}
New-Item -ItemType Directory -Force -Path $previewOutput | Out-Null
$previewScript = Join-Path $PSScriptRoot 'ui-preview/server.mjs'
$timer = [Diagnostics.Stopwatch]::StartNew()
$process = Start-Process -FilePath $node -ArgumentList ('"' + $previewScript + '"') -WorkingDirectory $previewRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $previewOutput 'server.log') -RedirectStandardError (Join-Path $previewOutput 'server.err.log')
for ($attempt = 0; $attempt -lt 90; $attempt++) {
    $health = Get-PreviewHealth
    if ($health) {
        Assert-OurPreview $health
        Write-Output "Preview ready in $([Math]::Round($timer.Elapsed.TotalSeconds, 1)) seconds: $previewUrl"
        Write-Output 'Leave it running while editing. Saved sample changes survive refresh; stopping resets them.'
        if (!$NoBrowser) { Start-Process $previewUrl }
        return
    }
    $process.Refresh()
    if ($process.HasExited) { throw "Preview exited. See $previewOutput/server.err.log" }
    Start-Sleep -Milliseconds 300
}
throw "Preview startup timed out. See $previewOutput/server.err.log. Use the stop command if it becomes available."
