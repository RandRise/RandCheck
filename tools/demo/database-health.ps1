# Pure log classification: normal initialization shutdown is not crash recovery.
function Get-DemoDatabaseFaults([string]$Log) {
    $patterns = @{
        abnormalProcessExit = 'server process .* (exited with exit code [1-9]|was terminated by signal)'
        crashReinitialization = 'all server processes terminated; reinitializing'
        automaticRecovery = 'database system was not properly shut down; automatic recovery in progress'
        panic = '(?m)\bPANIC:'
    }
    $faults = @{}
    foreach ($entry in $patterns.GetEnumerator()) {
        $count = [regex]::Matches($Log, $entry.Value).Count
        if ($count -gt 0) { $faults[$entry.Key] = $count }
    }
    return $faults
}

function Assert-DemoDatabaseHealthy([string]$Project, [string]$Root) {
    $container = "$Project-db-1"
    $raw = & docker inspect $container
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the owned demo database container; it may not have started.' }
    $info = @($raw | ConvertFrom-Json)[0]
    if ($null -eq $info -or $info.Config.Labels.'com.docker.compose.project' -ne $Project -or
        $info.Config.Labels.'com.docker.compose.service' -ne 'db') { throw 'Cannot verify demo database ownership.' }
    $log = (& docker logs $container 2>&1 | ForEach-Object { "$_" }) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect demo database logs.' }
    $faults = Get-DemoDatabaseFaults $log
    $healthy = $info.State.Running -and $info.State.Health.Status -eq 'healthy' -and
        !$info.State.OOMKilled -and $info.RestartCount -eq 0 -and $faults.Count -eq 0
    $receipt = @{
        checkedUtc = [DateTime]::UtcNow.ToString('o'); project = $Project
        containerId = $info.Id; startedAt = $info.State.StartedAt
        running = $info.State.Running; health = $info.State.Health.Status
        oomKilled = $info.State.OOMKilled; restartCount = $info.RestartCount
        faults = $faults; passed = [bool]$healthy
    }
    $directory = Join-Path $Root 'diagnostics'
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffffffZ')
    $receipt | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $directory "$stamp-database-health.json")
    # Raw logs remain in the ignored disposable copy, never in published receipts.
    $log | Set-Content (Join-Path $directory "$stamp-database.log")
    if (!$healthy) { throw 'Demo database health/recovery check failed. Diagnostics preserved; use down to stop without deleting data.' }
    Write-Output 'PASS: demo database healthy; no abnormal process exit or crash recovery in this container lifetime.'
}
