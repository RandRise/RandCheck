[CmdletBinding()]
param([ValidateSet('all','workflow','backend','web','python','fixture','audits')][string]$Group='all', [switch]$ExportBrowserReview)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location $root
$hosted=$env:GITHUB_ACTIONS -eq 'true' -and $env:RUNNER_ENVIRONMENT -eq 'github-hosted'
if (!$hosted) {
    $markerPath=Join-Path $root '.ci-owner.json'
    if (!(Test-Path $markerPath)) { throw 'Local CI requires prepare-ci-source.ps1 and its isolated copy.' }
    $owner=Get-Content $markerPath -Raw | ConvertFrom-Json
    if ($owner.schema -ne 1 -or $owner.root -cne $root -or (Get-FileHash source-inventory.json).Hash -ne $owner.inventorySha256) { throw 'CI snapshot ownership mismatch.' }
    foreach ($entry in (Get-Content source-inventory.json -Raw | ConvertFrom-Json)) {
        if ((Get-FileHash -LiteralPath $entry.path).Hash -ne $entry.sha256) { throw "CI source changed: $($entry.path). Prepare a fresh copy." }
    }
    if ($IsWindows) {
        if ($env:DOCKER_HOST -and $env:DOCKER_HOST -notmatch '^npipe://') { throw 'Local CI requires local Docker Desktop.' }
        $context=(& docker context inspect | ConvertFrom-Json)[0]
        if ($LASTEXITCODE -ne 0 -or $context.Endpoints.docker.Host -notmatch '^npipe://') { throw 'Remote Docker context refused.' }
    } else { throw 'Local reproduction is supported on Windows Docker Desktop; Linux execution is hosted CI only.' }
}
$id=[Guid]::NewGuid().ToString('N').Substring(0,12)
$project="uaedi-ci-$id"
$results=Join-Path $root "outputs/ci/$id"
New-Item -ItemType Directory -Path $results -Force | Out-Null
$emptyEnv=Join-Path $results 'empty.env'
New-Item -ItemType File -Path $emptyEnv | Out-Null
$oldProject=$env:CI_PROJECT; $oldResults=$env:CI_RESULTS_DIRECTORY
$oldBrowserExport=$env:CI_EXPORT_BROWSER_REVIEW
$env:CI_PROJECT=$project; $env:CI_RESULTS_DIRECTORY=$results
$env:CI_EXPORT_BROWSER_REVIEW=if($ExportBrowserReview){'1'}else{'0'}
$compose=@('compose','--project-name',$project,'--project-directory',$root,'--env-file',$emptyEnv,'-f',(Join-Path $PSScriptRoot 'compose.yml'))
function Compose([string[]]$Arguments) {
    & docker @compose @Arguments
    if ($LASTEXITCODE -ne 0) { throw "CI Compose operation failed ($LASTEXITCODE)." }
}
$summary=@{group=$Group;project=$project;startedUtc=[DateTime]::UtcNow.ToString('o');passed=$false;cleanupPassed=$false}
$groups=if($Group -eq 'all'){@('workflow','backend','web','python','fixture','audits')}else{@($Group)}
$built=[Collections.Generic.HashSet[string]]::new()
$dockerUsed=$false
try {
    foreach($stage in $groups) {
        if($stage -eq 'workflow') {
            & "$PSScriptRoot/lint-workflow.ps1" -Results $results
            & "$PSScriptRoot/../demo/test-database-health.ps1" -Results $results
            continue
        }
        if (!$dockerUsed) {
            $existing=@(& docker ps -aq --filter "label=com.docker.compose.project=$project")
            if ($LASTEXITCODE -ne 0 -or $existing.Count) { throw 'CI project must be new and Docker available.' }
            $dockerUsed=$true
        }
        $services=if($stage -eq 'audits'){@('backend','web','python')}else{@($stage)}
        foreach($service in $services) {
            if($built.Add($service)) { Compose -Arguments @('build',$service) }
        }
        if($stage -eq 'backend') { Compose -Arguments @('up','-d','--wait','db') }
        foreach($service in $services) {
            $target=if($stage -eq 'audits'){"$service-audit"}else{$service}
            Compose -Arguments @('run','--rm','--no-deps',$target)
        }
    }
    $summary.passed=$true
} finally {
    try {
        if($dockerUsed) { Compose -Arguments @('down','--volumes','--timeout','20') }
        $summary.cleanupPassed=$true
    } finally {
        $summary.completedUtc=[DateTime]::UtcNow.ToString('o')
        $summary | ConvertTo-Json | Set-Content (Join-Path $results 'summary.json')
        $env:CI_PROJECT=$oldProject; $env:CI_RESULTS_DIRECTORY=$oldResults
        $env:CI_EXPORT_BROWSER_REVIEW=$oldBrowserExport
    }
}
Write-Output "PASS: $Group; evidence: $results"
