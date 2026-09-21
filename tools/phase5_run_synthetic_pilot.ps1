param([switch]$ExecutePaid,
    [ValidateSet('smoke-v2','p5-q-007','p5-q-009','p5-q-014','p5-q-016','p5-q-019','p5-q-025','p5-q-026','p5-q-028','p5-q-029')]
    [string]$Case='smoke-v2')
$ErrorActionPreference = 'Stop'
if (-not $ExecutePaid) { throw 'Explicit -ExecutePaid is required for the synthetic pilot.' }
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskReport = if($Case -ne 'smoke-v2'){Join-Path $taskRepo "outputs/phase-5/live-quality-20260909/$Case-generation.json"}else{$null}
if($taskReport -and (Test-Path -LiteralPath $taskReport)){throw 'This quality attempt is already recorded; use offline replay instead.'}
$taskMode=if($Case -eq 'smoke-v2'){'--synthetic-only'}else{'--quality-case'}
$taskCaseArgument=if($Case -eq 'smoke-v2'){'94b3d1a9-e377-41b2-83c5-6fc960e71563'}else{$Case}
$taskKeyLines = @(Get-Content -LiteralPath (Join-Path $taskRepo '.env') | Where-Object { $_ -match '^\s*OPENAI_API_KEY\s*=' })
if ($taskKeyLines.Count -ne 1) { throw 'A single local API key entry is required.' }
$taskKey = ($taskKeyLines[0] -replace '^\s*OPENAI_API_KEY\s*=\s*', '').Trim().Trim('"').Trim("'")
if ($taskKey -notmatch '^sk-[A-Za-z0-9_-]{17,509}$') { throw 'The local API key entry is invalid.' }
$taskSecret = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$taskProbe = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 28157)
try { $taskProbe.Start() } finally { $taskProbe.Stop() }
$taskPython = $null
$taskRunner = $null
try {
    $taskPythonStart = [System.Diagnostics.ProcessStartInfo]::new((Join-Path $taskRepo '.venv/Scripts/python.exe'))
    $taskPythonStart.UseShellExecute = $false
    $taskPythonStart.CreateNoWindow = $true
    $taskPythonStart.RedirectStandardOutput = $true
    $taskPythonStart.RedirectStandardError = $true
    $taskPythonStart.WorkingDirectory = $taskRepo
    foreach ($taskArg in @('-m','uvicorn','app.cited_synthetic_pilot:create_app','--factory','--host','127.0.0.1','--port','28157','--no-access-log','--log-level','warning')) {
        $taskPythonStart.ArgumentList.Add($taskArg)
    }
    $taskPythonStart.Environment['PYTHONPATH'] = Join-Path $taskRepo 'apps/api'
    $taskPythonStart.Environment['OPENAI_API_KEY'] = $taskKey
    $taskPythonStart.Environment['WORKER_SHARED_SECRET'] = $taskSecret
    $taskPythonStart.Environment['CITED_SYNTHETIC_PILOT'] = 'nano-v1'
    $taskPython = [System.Diagnostics.Process]::Start($taskPythonStart)
    # Drain but never print child diagnostics, which could contain provider/runtime details.
    $taskPythonOut = $taskPython.StandardOutput.ReadToEndAsync()
    $taskPythonErr = $taskPython.StandardError.ReadToEndAsync()
    $taskReady = $false
    for ($taskAttempt = 0; $taskAttempt -lt 60; $taskAttempt++) {
        if ($taskPython.HasExited) { throw 'Private pilot service failed to start.' }
        $taskSocket = [System.Net.Sockets.TcpClient]::new()
        try { $taskSocket.Connect('127.0.0.1',28157); $taskReady = $true; break }
        catch { Start-Sleep -Milliseconds 100 }
        finally { $taskSocket.Dispose() }
    }
    if (-not $taskReady) { throw 'Private pilot service was not ready.' }
    $taskRunnerStart = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
    $taskRunnerStart.UseShellExecute = $false
    $taskRunnerStart.CreateNoWindow = $true
    $taskRunnerStart.RedirectStandardOutput = $true
    $taskRunnerStart.RedirectStandardError = $true
    $taskRunnerStart.WorkingDirectory = $taskRepo
    $taskRunnerStart.Environment['WORKER_SHARED_SECRET'] = $taskSecret
    [void]$taskRunnerStart.Environment.Remove('OPENAI_API_KEY')
    foreach ($taskArg in @((Join-Path $taskRepo 'apps/backend/tools/UaeDocumentIntelligence.SyntheticPilot/bin/Debug/net8.0/UaeDocumentIntelligence.SyntheticPilot.dll'),
            $taskMode,$taskCaseArgument)) { $taskRunnerStart.ArgumentList.Add($taskArg) }
    $taskRunner = [System.Diagnostics.Process]::Start($taskRunnerStart)
    $taskRunnerOut = $taskRunner.StandardOutput.ReadToEndAsync()
    $taskRunnerErr = $taskRunner.StandardError.ReadToEndAsync()
    if (-not $taskRunner.WaitForExit(75000)) { throw 'Pilot deadline exceeded; inspect the ledger before any further work.' }
    $taskResultText=$taskRunnerOut.GetAwaiter().GetResult()
    if($taskReport){
        # Raw proposal remains in the canonical journal; save the bounded validation
        # or failure result separately, never overwrite a previous quality attempt.
        $taskResultText | ConvertFrom-Json | Out-Null
        $taskResultText | Set-Content -LiteralPath $taskReport -Encoding utf8
        $taskSummary=$taskResultText | ConvertFrom-Json
        [pscustomobject]@{case_id=$Case;outcome=$taskSummary.outcome;code=$taskSummary.code;
            status=$taskSummary.Validation.Status;claims=($taskSummary.Validation.Claims.Text -join ' ');reason=$taskSummary.Validation.ReasonCode}
    }else{Write-Output $taskResultText}
    if ($taskRunner.ExitCode -ne 0) { throw 'Pilot stopped; no automatic retry or budget release was performed.' }
}
finally {
    foreach ($taskProcess in @($taskRunner,$taskPython)) {
        if ($null -ne $taskProcess) {
            if (-not $taskProcess.HasExited) { $taskProcess.Kill($true) }
            $taskProcess.WaitForExit()
            $taskProcess.Dispose()
        }
    }
    $taskKey = $null
    $taskKeyLines = $null
    $taskSecret = $null
}
