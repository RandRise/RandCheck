param(
    [string]$BaseUri = "http://127.0.0.1:8086",
    [string]$PrivateBaseUri = "http://127.0.0.1:8006",
    [string]$ReportPath = "outputs/phase-5/security-20260908/full-stack-api.json"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$uri = [Uri]$BaseUri
if (-not $uri.IsLoopback -or $uri.Scheme -ne 'http' -or $uri.AbsolutePath -ne '/') {
    throw 'This synthetic acceptance script only targets a loopback HTTP API.'
}
$BaseUri = $BaseUri.TrimEnd('/')
$privateUri = [Uri]$PrivateBaseUri
if (-not $privateUri.IsLoopback -or $privateUri.Scheme -ne 'http' -or $privateUri.AbsolutePath -ne '/') {
    throw 'The private extraction service must also be a loopback HTTP service.'
}
$report = [IO.Path]::GetFullPath((Join-Path $root $ReportPath))
$outputRoot = [IO.Path]::GetFullPath((Join-Path $root 'outputs')) + [IO.Path]::DirectorySeparatorChar
if (-not $report.StartsWith($outputRoot, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $report)) {
    throw 'Use a new report path inside the repository outputs directory; existing reports are preserved.'
}
$file = Get-Item -LiteralPath (Join-Path $root 'output/pdf/uae-invoice-development-v0.2.0/fta-standard-services-native.pdf')
$checks = [Collections.Generic.List[string]]::new()
function Assert-Check([bool]$condition, [string]$name) {
    if (-not $condition) { throw "Acceptance failed: $name" }
    $checks.Add($name)
}
function Json-Request([string]$path, [string]$method = 'GET', $body = $null, [string]$etag = '') {
    $parameters = @{ Uri = "$BaseUri$path"; Method = $method; TimeoutSec = 60 }
    if ($etag) { $parameters.Headers = @{ 'If-Match' = $etag } }
    if ($null -ne $body) { $parameters.ContentType = 'application/json'; $parameters.Body = $body | ConvertTo-Json -Depth 8 -Compress }
    $response = Invoke-WebRequest @parameters
    if ($path.StartsWith('/api/cited-')) { Assert-Check ([string]$response.Headers['Cache-Control'] -eq 'no-store') "no-store $method $path" }
    [pscustomobject]@{ Value = $response.Content | ConvertFrom-Json; ETag = [string]$response.Headers.ETag }
}

# This uploads an existing synthetic PDF to the real API and durable worker.
# Fail before creating a document when either service has not started.
Invoke-RestMethod -Uri "$BaseUri/health" -TimeoutSec 5 | Out-Null
Invoke-RestMethod -Uri "$($PrivateBaseUri.TrimEnd('/'))/health" -TimeoutSec 5 | Out-Null
$client = [Net.Http.HttpClient]::new()
$multipart = [Net.Http.MultipartFormDataContent]::new()
try {
    $content = [Net.Http.StreamContent]::new($file.OpenRead())
    $content.Headers.ContentType = [Net.Http.Headers.MediaTypeHeaderValue]::new('application/pdf')
    $multipart.Add($content, 'file', $file.Name)
    $response = $client.PostAsync("$BaseUri/api/documents", $multipart).GetAwaiter().GetResult()
    $response.EnsureSuccessStatusCode() | Out-Null
    $upload = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
} finally {
    $multipart.Dispose()
    $client.Dispose()
}
$documentId = [string]$upload.id
$processing = $null
for ($attempt = 0; $attempt -lt 120; $attempt++) {
    $processing = Invoke-RestMethod -Uri "$BaseUri/api/documents/$documentId/processing" -TimeoutSec 10
    if ($processing.status -in @('Ready','Partial','Failed','Cancelled')) { break }
    Start-Sleep -Milliseconds 500
}
Assert-Check ($processing.status -in @('Ready','Partial')) "real extraction completed (status=$($processing.status), code=$($processing.errorCode))"
$review = Json-Request "/api/documents/$documentId/reviews" 'POST'
$machineCount = @($review.Value.fields | Where-Object { $null -ne $_.machine }).Count
Assert-Check ($machineCount -gt 0) 'nonempty extracted fields'
foreach ($field in @($review.Value.fields | Where-Object { $null -ne $_.machine })) {
    $review = Json-Request "/api/documents/$documentId/reviews/$($review.Value.session.id)/actions" 'POST' @{
        action='Confirmed'; machineFieldValueId=$field.machine.id
    } $review.ETag
}
$review = Json-Request "/api/documents/$documentId/reviews/$($review.Value.session.id)/complete" 'POST' $null $review.ETag
Assert-Check ($review.Value.session.status -eq 'Completed') 'completed reviewed revision'
$reviewId = [string]$review.Value.session.id
$sources = (Json-Request '/api/cited-sources').Value
Assert-Check (@($sources.sources | Where-Object reviewSessionId -eq $reviewId).Count -eq 1) 'completed review in catalog'
$index = (Json-Request '/api/cited-indexes' 'POST' @{documentId=$documentId;reviewSessionId=$reviewId}).Value
Assert-Check ($index.status -eq 'Ready') 'private Python index ready'
$body = @{question='What amount is due on the invoice?';requestKey=[guid]::NewGuid().ToString();indexIds=@($index.id);topK=5}
$answer = (Json-Request '/api/cited-questions' 'POST' $body).Value
Assert-Check ($answer.stage -eq 'EvidenceReady' -and -not $answer.answerGenerationEnabled -and $null -eq $answer.answer) 'evidence only, no generated answer'
Assert-Check (@($answer.retrieval.hits | Where-Object documentId -ne $documentId).Count -eq 0) 'only selected document retrieved'
$pageHit = @($answer.retrieval.hits | Where-Object kind -eq 'PageText')[0]
Assert-Check ($null -ne $pageHit -and @($pageHit.spans).Count -gt 0) 'page hit with stored span'
$span = $pageHit.spans[0]
$evidence = (Json-Request "/api/cited-questions/$($answer.retrieval.id)/hits/$($pageHit.hitId)/evidence?spanId=$($span.id)").Value
Assert-Check ($evidence.reviewSessionId -eq $reviewId -and $evidence.page.quote -ceq $span.quote) 'authorized exact pinned quote'
Assert-Check ($evidence.page.quote.Contains('8,820.00')) 'expected synthetic amount in page evidence'
$replay = (Json-Request '/api/cited-questions' 'POST' $body).Value
Assert-Check ($replay.retrieval.replayed -and $replay.retrieval.id -eq $answer.retrieval.id) 'idempotent POST replay'
$saved = (Json-Request "/api/cited-questions/$($answer.retrieval.id)").Value
Assert-Check ($saved.question -eq $body.question -and $saved.retrieval.replayed) 'read-only saved result'
$refusal = (Json-Request '/api/cited-questions' 'POST' @{question='zzxunmatchedtoken';requestKey=[guid]::NewGuid().ToString();indexIds=@($index.id);topK=5}).Value
Assert-Check ($refusal.stage -eq 'InsufficientEvidence' -and $refusal.answer.reasonCode -eq 'no_relevant_evidence') 'real no-hit refusal'

$result = [ordered]@{
    schema_version='1.0'; recorded_at_utc=[DateTimeOffset]::UtcNow.ToString('o'); mode='real-local-stack-synthetic-input';
    api_url=$BaseUri; input_file=$file.Name; input_sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant();
    document_id=$documentId; review_session_id=$reviewId; index_id=[string]$index.id;
    retrieval_id=[string]$answer.retrieval.id; refusal_id=[string]$refusal.retrieval.id;
    page_hit_id=[string]$pageHit.hitId; span_id=[string]$span.id; extraction_status=$processing.status;
    machine_field_count=$machineCount; hit_count=@($answer.retrieval.hits).Count;
    lexical_policy=$answer.retrieval.policyVersion; api_checks_passed=$checks.ToArray();
    paid_api_calls=0; browser_verified=$false;
    limitations=@('One synthetic native PDF, not an accuracy benchmark','Browser verification is recorded separately','Confirmations are scripted fixture setup, not human field-accuracy review','No generated answer or production authentication')
}
New-Item -ItemType Directory -Path (Split-Path -Parent $report) -Force | Out-Null
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $report -Encoding utf8
$result | ConvertTo-Json -Depth 8
