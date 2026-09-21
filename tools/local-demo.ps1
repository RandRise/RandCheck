[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('up','down','reset','status','logs','health')][string]$Action)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$markerPath = Join-Path $root '.demo-owner.json'
if (!(Test-Path -LiteralPath $markerPath -PathType Leaf)) { throw 'Run prepare-local-demo.ps1 first; operate only inside its isolated copy.' }
$marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
$compose = Join-Path $root 'tools/demo/compose.yml'
if ($marker.schema -ne 1 -or $marker.project -notmatch '^uaedi-demo-[a-f0-9]{12}$' -or
    $marker.root -cne $root -or (Get-FileHash -LiteralPath $compose).Hash -ne $marker.composeSha256) {
    throw 'Demo ownership/configuration mismatch; refusing operation.'
}
foreach ($item in @($root, (Split-Path $root), $markerPath, $compose)) {
    if ((Get-Item -LiteralPath $item).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse demo path refused.' }
}
if ($env:DOCKER_HOST -and $env:DOCKER_HOST -notmatch '^npipe://') { throw 'Demo requires the local Docker Desktop daemon.' }
$contextName = & docker context show
if ($LASTEXITCODE -ne 0) { throw 'Docker context unavailable.' }
$dockerContext = (& docker context inspect $contextName | ConvertFrom-Json)[0]
if ($LASTEXITCODE -ne 0 -or $dockerContext.Endpoints.docker.Host -notmatch '^npipe://') { throw 'Demo requires a local Windows Docker Desktop context.' }
$arguments = @('compose', '--project-name', $marker.project, '--project-directory', $root,
    '--env-file', (Join-Path $root 'tools/demo/empty.env'), '-f', $compose)
function Invoke-Compose([string[]]$Command) {
    & docker @arguments @Command
    if ($LASTEXITCODE -ne 0) { throw "Demo Compose failed ($LASTEXITCODE). Use logs; down preserves data." }
}
. (Join-Path $PSScriptRoot 'demo/database-health.ps1')
switch ($Action) {
    up {
        if ((Get-FileHash (Join-Path $root 'source-inventory.json')).Hash -ne $marker.inventorySha256) { throw 'Source inventory changed.' }
        foreach ($entry in (Get-Content (Join-Path $root 'source-inventory.json') -Raw | ConvertFrom-Json)) {
            if ((Get-FileHash -LiteralPath (Join-Path $root $entry.path)).Hash -ne $entry.sha256) { throw "Snapshot changed: $($entry.path). Prepare a fresh copy." }
        }
        # Bound first-build memory pressure on developer laptops.
        foreach ($service in @('ai','web','backend','migrate')) { Invoke-Compose -Command @('build', $service) }
        Invoke-Compose -Command @('up', '-d', '--no-build')
        foreach ($binding in @(@('backend','8080/tcp','8308'), @('web','3000/tcp','3308'))) {
            $container = (& docker inspect "$($marker.project)-$($binding[0])-1" | ConvertFrom-Json)[0]
            $port = $container.NetworkSettings.Ports.($binding[1])
            if ($LASTEXITCODE -ne 0 -or @($port).Count -ne 1 -or $port[0].HostIp -ne '127.0.0.1' -or $port[0].HostPort -ne $binding[2]) { throw 'Demo loopback port publication failed.' }
        }
        $ready = $false
        for ($attempt = 0; $attempt -lt 90; $attempt++) {
            try {
                $null = Invoke-RestMethod 'http://127.0.0.1:8308/api/documents' -TimeoutSec 3
                $null = Invoke-WebRequest 'http://127.0.0.1:3308' -TimeoutSec 5
                $ready = $true; break
            } catch { Start-Sleep -Seconds 2 }
        }
        if (!$ready) { throw 'Demo readiness timed out. Inspect logs, then down; data is preserved.' }
        Assert-DemoDatabaseHealthy -Project $marker.project -Root $root
        Write-Output 'Local synthetic demo ready: http://127.0.0.1:3308 (Development identity).'
    }
    down { Invoke-Compose -Command @('down', '--timeout', '20') }
    reset {
        # Refuse unexpected project resources BEFORE any destructive action.
        $volumeNames = @(& docker volume ls --filter "label=com.docker.compose.project=$($marker.project)" --format '{{.Name}}')
        if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect demo volumes.' }
        $allVolumes = @(& docker volume ls --format '{{.Name}}')
        if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect volume names.' }
        foreach ($expected in @("$($marker.project)_database", "$($marker.project)_uploads")) {
            if ($expected -in $allVolumes -and $expected -notin $volumeNames) { throw 'Expected volume is not owned by this demo; refusing reset.' }
        }
        foreach ($name in $volumeNames) {
            if ($name -notin @("$($marker.project)_database", "$($marker.project)_uploads")) { throw 'Unexpected project volume; refusing reset.' }
            $info = (& docker volume inspect $name | ConvertFrom-Json)[0]
            if ($LASTEXITCODE -ne 0 -or $info.Labels.'com.docker.compose.project' -ne $marker.project -or
                $info.Labels.'com.docker.compose.volume' -notin @('database','uploads')) { throw 'Volume ownership mismatch.' }
        }
        $containers = @(& docker ps -aq --filter "label=com.docker.compose.project=$($marker.project)")
        if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect demo containers.' }
        foreach ($container in $containers) {
            $info = (& docker inspect $container | ConvertFrom-Json)[0]
            if ($LASTEXITCODE -ne 0 -or $info.Config.Labels.'com.docker.compose.service' -notin @('db','migrate','ai','backend','web')) { throw 'Unexpected project container; refusing reset.' }
            foreach ($mount in $info.Mounts) {
                if ($mount.Type -ne 'volume' -or $mount.Name -notin $volumeNames) { throw 'Unexpected project mount; refusing reset.' }
            }
        }
        Invoke-Compose -Command @('down', '--volumes', '--timeout', '20')
        Write-Output 'Only this disposable demo database and upload volumes were reset. Source/evidence preserved.'
    }
    status { Invoke-Compose -Command @('ps', '-a') }
    logs { Invoke-Compose -Command @('logs', '--tail', '100', '--no-color') }
    health { Assert-DemoDatabaseHealthy -Project $marker.project -Root $root }
}

