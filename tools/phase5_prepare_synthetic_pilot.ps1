param([switch]$CitationV2Case,[switch]$QualityCases)
$ErrorActionPreference = 'Stop'
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskProcesses = [Collections.Generic.List[System.Diagnostics.Process]]::new()
$taskDrains = [Collections.Generic.List[object]]::new()
function Start-PilotFixtureProcess([string]$Executable,[string[]]$Arguments,[hashtable]$Environment,[string]$Directory) {
    $taskStart = [System.Diagnostics.ProcessStartInfo]::new($Executable)
    $taskStart.WorkingDirectory = $Directory
    $taskStart.UseShellExecute = $false
    $taskStart.CreateNoWindow = $true
    $taskStart.RedirectStandardOutput = $true
    $taskStart.RedirectStandardError = $true
    foreach ($taskArg in $Arguments) { $taskStart.ArgumentList.Add($taskArg) }
    foreach ($taskName in $Environment.Keys) { $taskStart.Environment[$taskName] = $Environment[$taskName] }
    [void]$taskStart.Environment.Remove('OPENAI_API_KEY')
    $taskProcess = [System.Diagnostics.Process]::Start($taskStart)
    $taskProcesses.Add($taskProcess)
    $taskDrains.Add($taskProcess.StandardOutput.ReadToEndAsync())
    $taskDrains.Add($taskProcess.StandardError.ReadToEndAsync())
}
try {
    foreach ($taskPort in @(28007,28086)) {
        $taskProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$taskPort)
        try { $taskProbe.Start() } finally { $taskProbe.Stop() }
    }
    $taskUploads = Join-Path $taskRepo 'tmp/phase5-e2e-20260908/uploads'
    Start-PilotFixtureProcess (Join-Path $taskRepo 'tmp/phase5-native-runtime-20260908/Scripts/python.exe') @(
        '-m','uvicorn','app.main:app','--app-dir','apps/api','--host','127.0.0.1','--port','28007','--no-access-log','--log-level','warning') @{
        'WORKER_SHARED_SECRET'='synthetic-fixture-local-only';'STORAGE_SHARED_ROOT'=$taskUploads;'EXTRACTION_PROVIDER'='local'
    } $taskRepo
    Start-PilotFixtureProcess 'dotnet' @((Join-Path $taskRepo 'apps/backend/src/UaeDocumentIntelligence.Api/bin/Debug/net8.0/UaeDocumentIntelligence.Api.dll'),
        '--urls','http://127.0.0.1:28086') @{
        'ConnectionStrings__Database'='Host=127.0.0.1;Port=25434;Database=phase5_e2e_20260908;Username=document_intelligence;Password=local_development_only';
        'AiService__BaseUrl'='http://127.0.0.1:28007';'AiService__WorkerToken'='synthetic-fixture-local-only';
        'Storage__UploadDirectory'=$taskUploads;'ExtractionWorker__Enabled'='true';'DevelopmentData__Seed'='false';
        'ASPNETCORE_ENVIRONMENT'='Development';'Logging__LogLevel__Default'='Warning'
    } (Join-Path $taskRepo 'apps/backend/src/UaeDocumentIntelligence.Api')
    foreach ($taskPort in @(28007,28086)) {
        $taskReady = $false
        for ($taskAttempt=0; $taskAttempt -lt 100; $taskAttempt++) {
            if (@($taskProcesses | Where-Object HasExited).Count -ne 0) { throw 'Synthetic fixture service failed to start.' }
            try { Invoke-RestMethod -Uri "http://127.0.0.1:$taskPort/health" -TimeoutSec 1 | Out-Null; $taskReady=$true; break }
            catch { Start-Sleep -Milliseconds 100 }
        }
        if (-not $taskReady) { throw 'Synthetic fixture service was not ready.' }
    }
    if ($QualityCases) {
        if ($CitationV2Case) { throw 'Choose one preparation mode.' }
        & (Join-Path $taskRepo 'tools/phase5_prepare_quality_cases.ps1')
    } elseif ($CitationV2Case) {
        # Separate, explicitly versioned evaluation of the SAME eligible synthetic
        # source. Preserve the original failed operation, ledger and quarantine.
        $taskReport = Join-Path $taskRepo 'outputs/phase-5/live-pilot-20260909/citation-v2-source.json'
        if (Test-Path -LiteralPath $taskReport) { throw 'The citation-v2 case is already recorded.' }
        $taskBody = @{
            question='What amount is due on the invoice?'; topK=5;
            indexIds=@('704af047-040c-4274-b1f2-052a97c61e7f');
            requestKey='6bb02ed3-0721-4aa2-b8ef-5e31aa3007ce'
        } | ConvertTo-Json -Compress
        $taskCase = Invoke-RestMethod -Uri 'http://127.0.0.1:28086/api/cited-questions' -Method Post -ContentType 'application/json' -Body $taskBody
        if ($taskCase.stage -ne 'EvidenceReady' -or $taskCase.answerGenerationEnabled -or
            @($taskCase.retrieval.hits).Count -ne 3 -or
            @($taskCase.retrieval.hits | Where-Object documentId -ne '107d0304-1c58-489f-9c80-31e1f62481d7').Count -ne 0) {
            throw 'The citation-v2 source did not match the reviewed synthetic fixture.'
        }
        [ordered]@{ case='citation-pair-schema-v2'; recorded_at_utc=[DateTimeOffset]::UtcNow.ToString('o');
            previous_retrieval_id='d299b50e-8ef7-423d-9942-e40de003ea5c';
            request=($taskBody | ConvertFrom-Json); result=$taskCase; paid_api_calls=0
        } | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $taskReport -Encoding utf8
        [pscustomobject]@{ retrieval_id=$taskCase.retrieval.id; hit_count=@($taskCase.retrieval.hits).Count; paid_api_calls=0 }
    } else {
        & (Join-Path $taskRepo 'tools/phase5_evidence_acceptance.ps1') -BaseUri 'http://127.0.0.1:28086' -PrivateBaseUri 'http://127.0.0.1:28007' `
            -ReportPath 'outputs/phase-5/live-pilot-20260909/source-preparation.json'
    }
}
finally {
    foreach ($taskProcess in $taskProcesses) {
        if (-not $taskProcess.HasExited) { $taskProcess.Kill($true) }
        $taskProcess.WaitForExit()
        $taskProcess.Dispose()
    }
}
