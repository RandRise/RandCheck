[CmdletBinding()]
param([string]$Results)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'database-health.ps1')
$cases = @(
    @{name='normal initialization'; log="LOG: received fast shutdown request`nLOG: background worker `"logical replication launcher`" (PID 128) exited with exit code 1`nLOG: database system is shut down`nLOG: database system is ready to accept connections"; keys=@()},
    @{name='backend exit'; log='LOG: server process (PID 596) exited with exit code 2'; keys=@('abnormalProcessExit')},
    @{name='backend signal'; log='LOG: server process (PID 596) was terminated by signal 9: Killed'; keys=@('abnormalProcessExit')},
    @{name='recovered crash'; log="LOG: all server processes terminated; reinitializing`nLOG: database system was not properly shut down; automatic recovery in progress`nLOG: database system is ready to accept connections"; keys=@('automaticRecovery','crashReinitialization')},
    @{name='panic'; log='2026-09-17 00:00:00 UTC [1] PANIC: synthetic fault'; keys=@('panic')}
)
foreach ($case in $cases) {
    $actual = Get-DemoDatabaseFaults $case.log
    $actualKeys = ($actual.Keys | Sort-Object) -join ','
    $expectedKeys = ($case.keys | Sort-Object) -join ','
    if ($actualKeys -cne $expectedKeys) {
        throw "Database health classification failed: $($case.name)"
    }
}
if ($Results) {
    @{passed=$true;tests=$cases.Count;cases=@($cases.name)} | ConvertTo-Json | Set-Content (Join-Path $Results 'demo-health-tests.json')
}
Write-Output "PASS: $($cases.Count) database health classification tests."
