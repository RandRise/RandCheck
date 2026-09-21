$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskOutput = Join-Path $taskRoot 'outputs/phase-5/live-quality-20260909'
if (Test-Path -LiteralPath $taskOutput) { throw 'Existing quality preparation is preserved; inspect it instead of re-uploading.' }
$taskPlanPath = Join-Path $taskRoot 'samples/phase-5/live-quality-v0.1.0/plan.json'
$taskPlan = Get-Content -LiteralPath $taskPlanPath -Raw | ConvertFrom-Json
$taskSetPath = Join-Path $taskRoot $taskPlan.question_set
if ((Get-FileHash -LiteralPath $taskSetPath).Hash.ToLowerInvariant() -ne $taskPlan.question_set_sha256) { throw 'Question-set hash changed.' }
$taskSet = Get-Content -LiteralPath $taskSetPath -Raw | ConvertFrom-Json
if (-not $taskSet.synthetic -or $taskSet.claim_eligible -or $taskPlan.question_ids.Count -ne 10) { throw 'Unapproved evaluation plan.' }
$taskQuestions = @($taskPlan.question_ids | ForEach-Object {
    $taskFound = @($taskSet.questions | Where-Object question_id -eq $_)
    if ($taskFound.Count -ne 1) { throw 'Unknown evaluation question.' }
    $taskFound[0]
})
$taskSourceIds = @($taskQuestions.scope.allowed_source_ids | Sort-Object -Unique)
$taskSources = @($taskSet.sources | Where-Object source_id -in $taskSourceIds)
if ($taskSources.Count -ne 3 -or @($taskSources | Where-Object organization_fixture -ne 'org-primary').Count -ne 0) { throw 'Unexpected source scope.' }
foreach ($taskSource in $taskSources) {
    if ((Get-FileHash -LiteralPath (Join-Path $taskRoot $taskSource.path)).Hash.ToLowerInvariant() -ne $taskSource.sha256) { throw 'Source file hash changed.' }
}
New-Item -ItemType Directory -Path $taskOutput | Out-Null
function Save-QualityReport([string]$Name, $Value) {
    $taskTarget=Join-Path $taskOutput $Name
    if(Test-Path -LiteralPath $taskTarget){throw 'Existing report preserved.'}
    $Value | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $taskTarget -Encoding utf8
}
function Quality-Json([string]$Path,[string]$Method='GET',$Body=$null,[string]$Etag='') {
    $taskParams=@{Uri="http://127.0.0.1:28086$Path";Method=$Method;TimeoutSec=60}
    if($Etag){$taskParams.Headers=@{'If-Match'=$Etag}}
    if($null -ne $Body){$taskParams.ContentType='application/json';$taskParams.Body=$Body | ConvertTo-Json -Depth 10 -Compress}
    $taskReply=Invoke-WebRequest @taskParams
    [pscustomobject]@{Value=($taskReply.Content | ConvertFrom-Json);ETag=[string]$taskReply.Headers.ETag}
}
$taskSourceMap=@{}
foreach($taskSource in $taskSources) {
    $taskClient=[Net.Http.HttpClient]::new()
    $taskForm=[Net.Http.MultipartFormDataContent]::new()
    try {
        $taskFile=Get-Item -LiteralPath (Join-Path $taskRoot $taskSource.path)
        $taskContent=[Net.Http.StreamContent]::new($taskFile.OpenRead())
        $taskContent.Headers.ContentType=[Net.Http.Headers.MediaTypeHeaderValue]::new('application/pdf')
        $taskForm.Add($taskContent,'file',$taskFile.Name)
        $taskReply=$taskClient.PostAsync('http://127.0.0.1:28086/api/documents',$taskForm).GetAwaiter().GetResult()
        $taskReply.EnsureSuccessStatusCode() | Out-Null
        $taskUpload=$taskReply.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
    } finally {$taskForm.Dispose();$taskClient.Dispose()}
    Save-QualityReport "upload-$($taskSource.source_id).json" $taskUpload
    $taskDoc=[string]$taskUpload.id
    for($taskTry=0;$taskTry -lt 120;$taskTry++) {
        $taskProcessing=(Quality-Json "/api/documents/$taskDoc/processing").Value
        if($taskProcessing.status -in @('Ready','Partial','Failed','Cancelled')){break}
        Start-Sleep -Milliseconds 500
    }
    if($taskProcessing.status -notin @('Ready','Partial')){throw 'Quality fixture extraction failed.'}
    $taskReview=Quality-Json "/api/documents/$taskDoc/reviews" 'POST'
    foreach($taskField in @($taskReview.Value.fields | Where-Object {$null -ne $_.machine})) {
        $taskReview=Quality-Json "/api/documents/$taskDoc/reviews/$($taskReview.Value.session.id)/actions" 'POST' @{
            action='Confirmed';machineFieldValueId=$taskField.machine.id
        } $taskReview.ETag
    }
    $taskReview=Quality-Json "/api/documents/$taskDoc/reviews/$($taskReview.Value.session.id)/complete" 'POST' $null $taskReview.ETag
    if($taskReview.Value.session.status -ne 'Completed'){throw 'Quality fixture review incomplete.'}
    $taskIndex=(Quality-Json '/api/cited-indexes' 'POST' @{documentId=$taskDoc;reviewSessionId=$taskReview.Value.session.id}).Value
    if($taskIndex.status -ne 'Ready'){throw 'Quality fixture index not ready.'}
    $taskSourceMap[$taskSource.source_id]=[ordered]@{
        source_id=$taskSource.source_id;path=$taskSource.path;sha256=$taskSource.sha256;
        document_id=$taskDoc;review_session_id=[string]$taskReview.Value.session.id;index_id=[string]$taskIndex.id;
        extraction_status=$taskProcessing.status;field_count=@($taskReview.Value.fields).Count;chunk_count=$taskIndex.chunkCount
    }
    Save-QualityReport "source-$($taskSource.source_id).json" $taskSourceMap[$taskSource.source_id]
}
$taskCases=@()
foreach($taskQuestion in $taskQuestions) {
    $taskIndexIds=@($taskQuestion.scope.allowed_source_ids | ForEach-Object {$taskSourceMap[$_].index_id})
    $taskBody=@{question=$taskQuestion.question;requestKey="live-quality-v0.1.0-$($taskQuestion.question_id)";topK=$taskPlan.top_k;indexIds=$taskIndexIds}
    $taskResult=(Quality-Json '/api/cited-questions' 'POST' $taskBody).Value
    if($taskResult.answerGenerationEnabled -or $taskResult.stage -notin @('EvidenceReady','InsufficientEvidence') -or
       @($taskResult.retrieval.hits | Where-Object indexId -notin $taskIndexIds).Count -ne 0){throw 'Quality retrieval scope invariant failed.'}
    $taskCase=[ordered]@{case_id=$taskQuestion.question_id;category=$taskQuestion.category;question=$taskQuestion.question;
        expected=$taskQuestion.expected;scope=$taskQuestion.scope;request=$taskBody;result=$taskResult}
    Save-QualityReport "$($taskQuestion.question_id)-source.json" $taskCase
    $taskCases+= $taskCase
}
$taskManifest=[ordered]@{version=$taskPlan.version;synthetic=$true;claim_eligible=$false;prepared_at_utc=[DateTimeOffset]::UtcNow.ToString('o');
    plan_sha256=(Get-FileHash -LiteralPath $taskPlanPath).Hash.ToLowerInvariant();question_set_sha256=$taskPlan.question_set_sha256;
    paid_api_calls=0;sources=@($taskSources | ForEach-Object {$taskSourceMap[$_.source_id]});cases=$taskCases;
    limitations=@('Scripted review confirmations, not human field-accuracy review','Subset, not full 30-question benchmark','Direct prompt injection only; document-embedded attacks untested')}
Save-QualityReport 'prepared-cases.json' $taskManifest
$taskCases | ForEach-Object {[pscustomobject]@{case_id=$_.case_id;retrieval_id=$_.result.retrieval.id;stage=$_.result.stage;hits=@($_.result.retrieval.hits).Count}}
Get-FileHash -LiteralPath (Join-Path $taskOutput 'prepared-cases.json')
