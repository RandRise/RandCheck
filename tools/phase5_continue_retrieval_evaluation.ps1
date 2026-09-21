param([switch]$ExecutePaid)
$ErrorActionPreference = 'Stop'
if (-not $ExecutePaid) { throw 'Explicit -ExecutePaid is required for the approved synthetic embedding pilot.' }
$embeddingRepo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$embeddingRunnerDll = Join-Path $embeddingRepo 'apps/backend/tools/UaeDocumentIntelligence.EmbeddingPilot/bin/Debug/net8.0/UaeDocumentIntelligence.EmbeddingPilot.dll'
& dotnet $embeddingRunnerDll --evaluation-continue-verify
if ($LASTEXITCODE -ne 0) { throw 'Frozen plan verification failed before key access.' }
$embeddingOutput = Join-Path $embeddingRepo 'outputs/phase-5/retrieval-evaluation-20260910/continued-embeddings.jsonl'
New-Item -ItemType Directory -Force (Split-Path $embeddingOutput) | Out-Null
if (Test-Path -LiteralPath $embeddingOutput) { throw 'Pilot attempt is already recorded. Use the offline replay command.' }
$embeddingReport = [IO.File]::Open($embeddingOutput,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
$embeddingPython = $null
$embeddingRunner = $null
$embeddingKey = $null
$embeddingKeyLines = $null
try {
    $embeddingKeyLines = @(Get-Content -LiteralPath (Join-Path $embeddingRepo '.env') | Where-Object { $_ -match '^\s*OPENAI_API_KEY\s*=' })
    if ($embeddingKeyLines.Count -ne 1) { throw 'A single local API key entry is required.' }
    $embeddingKey = ($embeddingKeyLines[0] -replace '^\s*OPENAI_API_KEY\s*=\s*','').Trim().Trim('"').Trim("'")
    if ($embeddingKey -notmatch '^sk-[A-Za-z0-9_-]{17,509}$') { throw 'Invalid local API key entry.' }
    $embeddingSecret = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $embeddingProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,28158)
    try { $embeddingProbe.Start() } finally { $embeddingProbe.Stop() }
    $embeddingStart = [Diagnostics.ProcessStartInfo]::new((Join-Path $embeddingRepo '.venv/Scripts/python.exe'))
    $embeddingStart.UseShellExecute = $false
    $embeddingStart.CreateNoWindow = $true
    $embeddingStart.RedirectStandardOutput = $true
    $embeddingStart.RedirectStandardError = $true
    $embeddingStart.WorkingDirectory = $embeddingRepo
    foreach ($embeddingArg in @('-m','uvicorn','app.cited_embedding_pilot:create_app','--factory','--host','127.0.0.1','--port','28158','--no-access-log','--log-level','warning')) {
        $embeddingStart.ArgumentList.Add($embeddingArg)
    }
    $embeddingStart.Environment['PYTHONPATH'] = Join-Path $embeddingRepo 'apps/api'
    $embeddingStart.Environment['OPENAI_API_KEY'] = $embeddingKey
    $embeddingStart.Environment['WORKER_SHARED_SECRET'] = $embeddingSecret
    $embeddingStart.Environment['CITED_EMBEDDING_PILOT'] = 'small-v1'
    $embeddingStart.Environment['EMBEDDING_VOCABULARY'] = Join-Path $embeddingRepo '.venv/share/tiktoken/cl100k_base.tiktoken'
    $embeddingPython = [Diagnostics.Process]::Start($embeddingStart)
    $embeddingPythonOut = $embeddingPython.StandardOutput.ReadToEndAsync()
    $embeddingPythonErr = $embeddingPython.StandardError.ReadToEndAsync()
    $embeddingReady = $false
    for ($embeddingAttempt=0; $embeddingAttempt -lt 100; $embeddingAttempt++) {
        if ($embeddingPython.HasExited) { throw 'Private embedding service failed to start.' }
        $embeddingSocket = [Net.Sockets.TcpClient]::new()
        try { $embeddingSocket.Connect('127.0.0.1',28158); $embeddingReady=$true; break }
        catch { Start-Sleep -Milliseconds 100 }
        finally { $embeddingSocket.Dispose() }
    }
    if (-not $embeddingReady) { throw 'Private embedding service did not become ready.' }
    $embeddingRunStart = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $embeddingRunStart.UseShellExecute = $false
    $embeddingRunStart.CreateNoWindow = $true
    $embeddingRunStart.RedirectStandardOutput = $true
    $embeddingRunStart.RedirectStandardError = $true
    $embeddingRunStart.WorkingDirectory = $embeddingRepo
    $embeddingRunStart.Environment['WORKER_SHARED_SECRET'] = $embeddingSecret
    [void]$embeddingRunStart.Environment.Remove('OPENAI_API_KEY')
    $embeddingRunStart.ArgumentList.Add($embeddingRunnerDll)
    $embeddingRunStart.ArgumentList.Add('--evaluation-continue')
    $embeddingRunner = [Diagnostics.Process]::Start($embeddingRunStart)
    $embeddingRunOut = $embeddingRunner.StandardOutput.ReadToEndAsync()
    $embeddingRunErr = $embeddingRunner.StandardError.ReadToEndAsync()
    if (-not $embeddingRunner.WaitForExit(900000)) { throw 'Embedding pilot deadline exceeded; inspect ledger before further work.' }
    $embeddingResults = $embeddingRunOut.GetAwaiter().GetResult()
    foreach ($embeddingLine in ($embeddingResults -split "`n" | Where-Object { $_.Trim() })) {
        $embeddingLine | ConvertFrom-Json | Out-Null
    }
    $embeddingBytes = [Text.Encoding]::UTF8.GetBytes($embeddingResults)
    $embeddingReport.Write($embeddingBytes,0,$embeddingBytes.Length)
    $embeddingReport.Flush()
    Write-Output $embeddingResults
    if ($embeddingRunner.ExitCode -ne 0) { throw 'Embedding pilot stopped; no automatic retry or release was performed.' }
}
finally {
    foreach ($embeddingProcess in @($embeddingRunner,$embeddingPython)) {
        if ($null -ne $embeddingProcess) {
            if (-not $embeddingProcess.HasExited) { $embeddingProcess.Kill($true) }
            $embeddingProcess.WaitForExit()
            $embeddingProcess.Dispose()
        }
    }
    $embeddingReport.Dispose()
    $embeddingKey=$null
    $embeddingKeyLines=$null
    $embeddingSecret=$null
}
