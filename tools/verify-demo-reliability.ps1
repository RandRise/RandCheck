# Sequential fresh-volume trials. Never points at a host database or reuses a demo.
[CmdletBinding()]
param([ValidateRange(1,5)][int]$Cycles=3, [ValidateRange(300,900)][int]$ObserveSeconds=300)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$runId=[Guid]::NewGuid().ToString('N').Substring(0,12)
$reportPath=Join-Path $root "outputs/demo-reliability-$runId.json"
$report=@{startedUtc=[DateTime]::UtcNow.ToString('o');passed=$false;cycles=@();observationSeconds=$ObserveSeconds}
try {
    foreach($cycle in 1..$Cycles) {
        # Fixed loopback ports must be free before allocating an owned copy.
        foreach($port in @(3308,8308)) {
            if(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
                throw "Loopback demo port $port is already in use. Stop its owner before verification."
            }
        }
        $demo=& "$PSScriptRoot/prepare-local-demo.ps1"
        $entry=@{cycle=$cycle;sourceCopy=$demo;passed=$false;cleanupPassed=$false;startedUtc=[DateTime]::UtcNow.ToString('o')}
        $report.cycles+=,$entry
        $log=Join-Path $demo 'reliability.log'
        Write-Output "Cycle $cycle/${Cycles}: $demo"
        try {
            & "$demo/tools/local-demo.ps1" up *>> $log
            & "$demo/tools/verify-local-demo.ps1" Empty *>> $log
            & "$demo/tools/verify-local-demo.ps1" Upload *>> $log
            & "$demo/tools/verify-local-demo.ps1" Review *>> $log
            $watch=[Diagnostics.Stopwatch]::StartNew()
            $probes=0
            while($watch.Elapsed.TotalSeconds -lt $ObserveSeconds) {
                & "$demo/tools/local-demo.ps1" health *>> $log
                $null=Invoke-RestMethod 'http://127.0.0.1:8308/api/documents' -TimeoutSec 10
                $probes++
                Start-Sleep -Seconds 30
            }
            & "$demo/tools/local-demo.ps1" health *>> $log
            $entry.observedSeconds=[Math]::Round($watch.Elapsed.TotalSeconds,1)
            $entry.apiProbes=$probes
            & "$demo/tools/verify-local-demo.ps1" Restart *>> $log
            & "$demo/tools/local-demo.ps1" health *>> $log
            $entry.passed=$true
            Write-Output "Cycle ${cycle}: cold start, observation, review and restart persistence passed."
        } finally {
            # Capture available diagnostics before deleting only this trial's owned volumes.
            try { & "$demo/tools/local-demo.ps1" health *>> $log } catch { $_.Exception.Message | Add-Content $log }
            & "$demo/tools/local-demo.ps1" reset *>> $log
            $entry.cleanupPassed=$true
            $entry.completedUtc=[DateTime]::UtcNow.ToString('o')
            $report | ConvertTo-Json -Depth 8 | Set-Content $reportPath
        }
    }
    $report.passed=$true
} finally {
    $report.completedUtc=[DateTime]::UtcNow.ToString('o')
    $report | ConvertTo-Json -Depth 8 | Set-Content $reportPath
}
Write-Output "PASS: reliability trials; receipt: $reportPath"
