[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$validationRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$validationOutput = Join-Path $validationRoot 'outputs/scale-integration-20261023'
$owner = Get-Content (Join-Path $validationOutput 'processes.json') -Raw | ConvertFrom-Json
if ($owner.root -ne $validationRoot -or $owner.database -ne 'uaedi_validation_20261023') { throw 'Validation ownership mismatch; nothing stopped.' }
$snapshot = @(Get-CimInstance Win32_Process)
$owned = @()
foreach ($kind in @('api', 'web', 'python')) {
    $processId = $owner.($kind + 'Pid')
    if (!$processId) { continue }
    $process = $snapshot | Where-Object ProcessId -EQ $processId
    if (!$process) { continue }
    $expectedExecutable = switch ($kind) { 'api' { '.dotnet/dotnet.exe' } 'web' { '.node/node.exe' } 'python' { '.venv/Scripts/python.exe' } }
    $signature = switch ($kind) { 'api' { 'UaeDocumentIntelligence.Api.dll' } 'web' { 'next/dist/bin/next' } 'python' { 'uvicorn' } }
    $started = $owner.($kind + 'StartedUtc')
    if (!$started -or $process.ExecutablePath -ne (Join-Path $validationRoot $expectedExecutable) -or
        !$process.CommandLine.Contains($signature) -or
        [Math]::Abs(($process.CreationDate.ToUniversalTime() - [DateTime]::Parse($started).ToUniversalTime()).TotalSeconds) -gt 1) {
        throw "Unrecognized $kind process; nothing stopped."
    }
    $owned += $process
    $parents = @($process.ProcessId)
    while ($parents.Count) {
        $children = @($snapshot | Where-Object { $_.ParentProcessId -in $parents -and $_.CreationDate -ge $process.CreationDate })
        if ($children.Count -eq 0) { break }
        $owned += $children
        $parents = @($children.ProcessId)
    }
}
# Recheck creation time immediately before stopping; never stop a reused process ID.
foreach ($process in ($owned | Sort-Object ProcessId -Unique)) {
    $current = Get-CimInstance Win32_Process -Filter "ProcessId=$($process.ProcessId)"
    if ($current -and $current.CreationDate -eq $process.CreationDate) { Stop-Process -Id $process.ProcessId -Force }
}
Write-Output 'Owned validation application processes stopped. Retained preview was not targeted.'
