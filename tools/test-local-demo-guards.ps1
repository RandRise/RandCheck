# Integration probes against only the generated, disposable demo copy.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& "$PSScriptRoot/verify-local-demo.ps1" Empty
$ownerPath = Join-Path $root '.demo-owner.json'
$owner = Get-Content $ownerPath -Raw | ConvertFrom-Json
$compose = Join-Path $root 'tools/demo/compose.yml'
function Expect-Refusal([string]$Pattern) {
    try { & "$PSScriptRoot/local-demo.ps1" reset; throw 'Reset unexpectedly succeeded.' }
    catch { if ($_.Exception.Message -notlike $Pattern) { throw } }
}
$bytes = [IO.File]::ReadAllBytes($compose)
try {
    [IO.File]::AppendAllText($compose, "`n# configuration mutation probe`n")
    Expect-Refusal 'Demo ownership/configuration mismatch*'
} finally { [IO.File]::WriteAllBytes($compose, $bytes) }
$bytes = [IO.File]::ReadAllBytes($ownerPath)
try {
    $owner.root = "$root-mismatch"
    $owner | ConvertTo-Json | Set-Content $ownerPath
    Expect-Refusal 'Demo ownership/configuration mismatch*'
} finally { [IO.File]::WriteAllBytes($ownerPath, $bytes) }
$probe = "$($owner.project)_guard_probe"
$existing = @(& docker volume ls --format '{{.Name}}')
if ($LASTEXITCODE -ne 0 -or $probe -in $existing) { throw 'Probe volume must not already exist.' }
& docker volume create --label "com.docker.compose.project=$($owner.project)" $probe | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Cannot create disposable guard probe.' }
try {
    Expect-Refusal 'Unexpected project volume*'
    & "$PSScriptRoot/verify-local-demo.ps1" Empty
} finally {
    & docker volume rm $probe | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Probe cleanup failed.' }
}
@{configurationTamperRefused=$true;foreignCopyPathRefused=$true;unexpectedVolumeRefused=$true;
  demoStillRunning=$true;checkedUtc=[DateTime]::UtcNow.ToString('o')} |
    ConvertTo-Json | Set-Content (Join-Path $root 'guard-verification.json')
Write-Output 'PASS: three destructive-operation guards refused reset and preserved the running empty demo.'
