param([switch]$ExecutePaid, [switch]$LowReasoning, [switch]$LowContinuation, [switch]$SourceContext, [switch]$SourceRemaining, [switch]$SourceCountContinuation, [switch]$SourceTlsContinuation)
$ErrorActionPreference = 'Stop'
if ($SourceTlsContinuation) { $SourceCountContinuation=$true }
if (-not $ExecutePaid) { throw 'Explicit -ExecutePaid is required for the authorized synthetic evaluation.' }
if ($SourceRemaining -and $SourceCountContinuation) { throw "Choose one continuation." }
if ($SourceRemaining -or $SourceCountContinuation) { $SourceContext=$true }
if ($SourceContext -and ($LowReasoning -or $LowContinuation)) { throw 'SourceContext selects its own pinned low-reasoning plan.' }
if ($SourceContext) { $LowReasoning=$true }
if ($LowContinuation) { $LowReasoning=$true }
$followupRepo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$followupDll = Join-Path $followupRepo 'apps/backend/tools/UaeDocumentIntelligence.SyntheticPilot/bin/Debug/net8.0/UaeDocumentIntelligence.SyntheticPilot.dll'
$followupCases = 1..30 | ForEach-Object { 'p5-q-{0:d3}' -f $_ }
if ($SourceRemaining) { $followupCases = 2..30 | ForEach-Object { 'p5-q-{0:d3}' -f $_ } }
if ($SourceCountContinuation) { $followupCases = 5..30 | ForEach-Object { 'p5-q-{0:d3}' -f $_ } }
if ($SourceTlsContinuation) { $followupCases = 6..30 | ForEach-Object { 'p5-q-{0:d3}' -f $_ } }
$followupOutput = Join-Path $followupRepo $(if($SourceContext){'outputs/phase-5/answer-source-20260911'}elseif($LowContinuation){'outputs/phase-5/answer-low-fixed-20260910'}elseif($LowReasoning){'outputs/phase-5/answer-low-20260910'}else{'outputs/phase-5/answer-followup-20260910'})
$followupVerifyMode = if($SourceContext){'--verify-source-case'}elseif($LowContinuation){'--verify-low-fixed-case'}elseif($LowReasoning){'--verify-low-case'}else{'--verify-followup-case'}
$followupMode = if($SourceContext){'--source-case'}elseif($LowContinuation){'--low-fixed-case'}elseif($LowReasoning){'--low-case'}else{'--followup-case'}
New-Item -ItemType Directory -Force $followupOutput | Out-Null
if ($SourceRemaining) {
    $followupContinuation = Get-Content -Raw (Join-Path $followupRepo 'samples/phase-5/answer-source-v0.4.0/continuation.json') | ConvertFrom-Json
    $followupPlanHash = (Get-FileHash (Join-Path $followupRepo 'samples/phase-5/answer-source-v0.4.0/plan.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    $followupFailedHash = (Get-FileHash (Join-Path $followupOutput 'p5-q-001-generation.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($followupPlanHash -ne '56fda3a2d5317cea14700f00965b4a808dc855e9d1d6934d48b8190cce7c08be' -or
        $followupContinuation.planSha256 -ne $followupPlanHash -or
        $followupContinuation.failedOutputSha256 -ne $followupFailedHash -or
        ($followupContinuation.remainingCaseIds -join ',') -ne ($followupCases -join ',') -or
        [decimal]$followupContinuation.retainedUnknownUsd -ne .002 -or
        [decimal]$followupContinuation.maximumNewReservedUsd -ne .058) { throw 'Continuation manifest mismatch.' }
    $followupHold = (& dotnet $followupDll '--retain-source-generation' 'p5-q-001') | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $followupHold.outcome -ne 'source-generation-cost-retained') { throw 'Unknown cost was not retained; do not dispatch.' }
    $followupHold | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $followupOutput 'retained-cost.json')
}
foreach ($followupCase in $followupCases) {
    if (Test-Path -LiteralPath (Join-Path $followupOutput "$followupCase-generation.json")) { throw 'Existing attempt: use offline replay.' }
    & dotnet $followupDll $followupVerifyMode $followupCase | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Manifest verification failed before key access.' }
}
if ($SourceCountContinuation) {
    $followupCountPlan = Get-Content -Raw (Join-Path $followupRepo $(if($SourceTlsContinuation){'samples/phase-5/answer-source-v0.4.0/tls-continuation.json'}else{'samples/phase-5/answer-source-v0.4.0/count-continuation.json'})) | ConvertFrom-Json
    if ((Get-FileHash (Join-Path $followupRepo 'samples/phase-5/answer-source-v0.4.0/plan.json')).Hash.ToLowerInvariant() -ne '56fda3a2d5317cea14700f00965b4a808dc855e9d1d6934d48b8190cce7c08be' -or
        ($followupCountPlan.remainingCaseIds -join ',') -ne ($followupCases -join ',') -or
        [decimal]$followupCountPlan.maximumNewReservedUsd -ne $(if($SourceTlsContinuation){.050}else{.052}) -or
        [decimal]$followupCountPlan.maximumTotalReservedUsd -ne .06) { throw 'Count continuation scope mismatch.' }
    foreach ($followupPrior in $followupCountPlan.priorOutputs) {
        if ((Get-FileHash (Join-Path $followupOutput $followupPrior.file)).Hash.ToLowerInvariant() -ne $followupPrior.sha256) { throw 'Prior output changed.' }
    }
    $followupCountHold = (& dotnet $followupDll '--retain-source-count' 'p5-q-004') | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $followupCountHold.outcome -ne 'source-count-cost-retained') { throw 'Count failure reservation was not retained.' }
    if (-not $SourceTlsContinuation) { $followupCountHold | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $followupOutput 'p5-q-004-retained-count.json') }
    if ($SourceTlsContinuation) {
        $followupConnectionHold = (& dotnet $followupDll '--retain-source-generation' 'p5-q-005') | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0 -or $followupConnectionHold.outcome -ne 'source-generation-cost-retained') { throw 'Connection failure reservation was not retained.' }
        $followupConnectionHold | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $followupOutput 'p5-q-005-retained-generation.json')
    }
}
$followupPython = $null
$followupRunner = $null
try {
    $followupLines = @(Get-Content -LiteralPath (Join-Path $followupRepo '.env') | Where-Object { $_ -match '^\s*OPENAI_API_KEY\s*=' })
    if ($followupLines.Count -ne 1) { throw 'A single local API key entry is required.' }
    $followupKey = ($followupLines[0] -replace '^\s*OPENAI_API_KEY\s*=\s*','').Trim().Trim('"').Trim("'")
    if ($followupKey -notmatch '^sk-[A-Za-z0-9_-]{17,509}$') { throw 'Invalid key entry.' }
    $followupSecret = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $followupProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,28157)
    try { $followupProbe.Start() } finally { $followupProbe.Stop() }
    $followupStart = [Diagnostics.ProcessStartInfo]::new((Join-Path $followupRepo '.venv/Scripts/python.exe'))
    $followupStart.UseShellExecute = $false
    $followupStart.CreateNoWindow = $true
    $followupStart.RedirectStandardOutput = $true
    $followupStart.RedirectStandardError = $true
    $followupStart.WorkingDirectory = $followupRepo
    foreach ($followupArg in @('-m','uvicorn','app.cited_synthetic_pilot:create_app','--factory','--host','127.0.0.1','--port','28157','--no-access-log','--log-level','warning')) { $followupStart.ArgumentList.Add($followupArg) }
    $followupStart.Environment['PYTHONPATH'] = Join-Path $followupRepo 'apps/api'
    $followupStart.Environment['OPENAI_API_KEY'] = $followupKey
    $followupStart.Environment['WORKER_SHARED_SECRET'] = $followupSecret
    $followupStart.Environment['CITED_SYNTHETIC_PILOT'] = if($LowReasoning){'nano-low-v1'}else{'nano-v1'}
    $followupPython = [Diagnostics.Process]::Start($followupStart)
    $followupPythonOut = $followupPython.StandardOutput.ReadToEndAsync()
    $followupPythonErr = $followupPython.StandardError.ReadToEndAsync()
    $followupReady = $false
    for ($followupAttempt=0; $followupAttempt -lt 100; $followupAttempt++) {
        if ($followupPython.HasExited) { throw 'Private service failed to start.' }
        $followupSocket = [Net.Sockets.TcpClient]::new()
        try { $followupSocket.Connect('127.0.0.1',28157); $followupReady=$true; break }
        catch { Start-Sleep -Milliseconds 100 }
        finally { $followupSocket.Dispose() }
    }
    if (-not $followupReady) { throw 'Private service did not become ready.' }
    foreach ($followupCase in $followupCases) {
        $followupFile = [IO.File]::Open((Join-Path $followupOutput "$followupCase-generation.json"),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
        try {
            $followupRunStart = [Diagnostics.ProcessStartInfo]::new('dotnet')
            $followupRunStart.UseShellExecute = $false
            $followupRunStart.CreateNoWindow = $true
            $followupRunStart.RedirectStandardOutput = $true
            $followupRunStart.RedirectStandardError = $true
            $followupRunStart.WorkingDirectory = $followupRepo
            $followupRunStart.Environment['WORKER_SHARED_SECRET'] = $followupSecret
            [void]$followupRunStart.Environment.Remove('OPENAI_API_KEY')
            foreach ($followupArg in @($followupDll,$followupMode,$followupCase)) { $followupRunStart.ArgumentList.Add($followupArg) }
            $followupRunner = [Diagnostics.Process]::Start($followupRunStart)
            $followupOut = $followupRunner.StandardOutput.ReadToEndAsync()
            $followupErr = $followupRunner.StandardError.ReadToEndAsync()
            if (-not $followupRunner.WaitForExit(75000)) { throw 'Deadline exceeded; inspect the saved ledger.' }
            $followupText = $followupOut.GetAwaiter().GetResult()
            $followupResult = $followupText | ConvertFrom-Json
            $followupBytes = [Text.Encoding]::UTF8.GetBytes($followupText)
            $followupFile.Write($followupBytes,0,$followupBytes.Length)
            $followupFile.Flush()
            [pscustomobject]@{case=$followupCase;outcome=$followupResult.outcome;code=$followupResult.code;status=$followupResult.Validation.Status} | ConvertTo-Json -Compress
            if ($SourceCountContinuation -and $followupResult.code -eq 'generation_outcome_uncertain') {
                $followupCountHold = (& dotnet $followupDll '--retain-source-count' $followupCase) | ConvertFrom-Json
                if ($LASTEXITCODE -ne 0 -or $followupCountHold.outcome -ne 'source-count-cost-retained') { throw 'Failure is not eligible count-stage recovery; stopped.' }
                $followupCountHold | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $followupOutput "$followupCase-retained-count.json")
                $followupRunner.Dispose()
                $followupRunner=$null
                continue
            }
            # Known settled validation failures count as failures; never repeat them.
            if ($followupRunner.ExitCode -ne 0 -and $followupResult.code -ne 'answer_proposal_not_accepted') { throw 'Evaluation stopped; inspect ledger before any further calls.' }
            $followupRunner.Dispose()
            $followupRunner = $null
        } finally { $followupFile.Dispose() }
    }
} finally {
    foreach ($followupProcess in @($followupRunner,$followupPython)) {
        if ($null -ne $followupProcess) {
            if (-not $followupProcess.HasExited) { $followupProcess.Kill($true) }
            $followupProcess.WaitForExit()
            $followupProcess.Dispose()
        }
    }
    $followupKey=$null
    $followupLines=$null
    $followupSecret=$null
}
