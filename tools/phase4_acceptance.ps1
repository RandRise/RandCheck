param(
    [string]$BaseUri = "http://localhost:8080",
    [string]$PackPath = "output/pdf/uae-procurement-reconciliation-v0.1.0",
    [string]$ReportPath = "outputs/phase-4/reconciliation-v0.1.0/acceptance-report.json"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$pack = [System.IO.Path]::GetFullPath((Join-Path $root $PackPath))
$reportFile = [System.IO.Path]::GetFullPath((Join-Path $root $ReportPath))
$source = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "phase4_procurement_source_v0.1.0.json") | ConvertFrom-Json

function Read-JsonResponse([Microsoft.PowerShell.Commands.BasicHtmlWebResponseObject]$Response) {
    [pscustomobject]@{
        Value = $Response.Content | ConvertFrom-Json
        ETag = [string]$Response.Headers.ETag
    }
}

function Invoke-Json([string]$Uri, [string]$Method, $Body, [string]$ETag = "") {
    $headers = @{}
    if ($ETag) { $headers["If-Match"] = $ETag }
    $parameters = @{ Uri = $Uri; Method = $Method; Headers = $headers; TimeoutSec = 30 }
    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 8 -Compress
    }
    Read-JsonResponse (Invoke-WebRequest @parameters)
}

function Send-Pdf([string]$Uri, [System.IO.FileInfo]$File) {
    $client = [System.Net.Http.HttpClient]::new()
    $multipart = [System.Net.Http.MultipartFormDataContent]::new()
    $stream = $null
    try {
        $stream = $File.OpenRead()
        $fileContent = [System.Net.Http.StreamContent]::new($stream)
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new("application/pdf")
        $multipart.Add($fileContent, "file", $File.Name)
        $response = $client.PostAsync($Uri, $multipart).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            throw "Upload failed ($([int]$response.StatusCode)): $body"
        }
        [pscustomobject]@{
            Value = $body | ConvertFrom-Json
            ETag = if ($response.Headers.ETag) { [string]$response.Headers.ETag } else { "" }
        }
    } finally {
        $multipart.Dispose()
        $client.Dispose()
        if ($stream) { $stream.Dispose() }
    }
}

for ($attempt = 0; $attempt -lt 30; $attempt++) {
    try {
        $health = Invoke-RestMethod -Uri "$BaseUri/health" -TimeoutSec 3
        if ($health.status -eq "ok") { break }
    } catch {
        if ($attempt -eq 29) { throw }
        Start-Sleep -Seconds 1
    }
}

$reviewed = @()
foreach ($document in $source.documents) {
    $file = Get-Item -LiteralPath (Join-Path $pack $document.filename)
    $upload = Send-Pdf "$BaseUri/api/documents" $file
    $documentId = [string]$upload.Value.id
    $processing = $null
    for ($attempt = 0; $attempt -lt 90; $attempt++) {
        Start-Sleep -Milliseconds 500
        $processing = Invoke-RestMethod -Uri "$BaseUri/api/documents/$documentId/processing" -TimeoutSec 10
        if ($processing.status -in @("Ready", "Partial", "Failed", "Cancelled")) { break }
    }
    if ($processing.status -notin @("Ready", "Partial")) {
        throw "$($document.role) extraction did not complete: $($processing.status) $($processing.errorCode)"
    }
    $stored = Invoke-RestMethod -Uri "$BaseUri/api/documents/$documentId" -TimeoutSec 10
    $expectedType = @{ Quotation = "quotation"; PurchaseOrder = "purchase_order"; Invoice = "invoice" }[$document.role]
    if ($stored.documentType -ne $expectedType) {
        throw "$($document.role) classified as '$($stored.documentType)', expected '$expectedType'"
    }

    $review = Read-JsonResponse (Invoke-WebRequest -Uri "$BaseUri/api/documents/$documentId/reviews" -Method Post -TimeoutSec 30)
    foreach ($field in @($review.Value.fields | Where-Object { $null -ne $_.machine })) {
        $review = Invoke-Json "$BaseUri/api/documents/$documentId/reviews/$($review.Value.session.id)/actions" "Post" @{
            action = "Confirmed"
            machineFieldValueId = $field.machine.id
        } $review.ETag
    }
    $review = Invoke-Json "$BaseUri/api/documents/$documentId/reviews/$($review.Value.session.id)/complete" "Post" $null $review.ETag
    if ($review.Value.session.status -ne "Completed") {
        throw "$($document.role) review did not complete"
    }
    $reviewed += [pscustomobject]@{
        role = $document.role
        document_id = $documentId
        document_type = $stored.documentType
        field_count = @($review.Value.fields).Count
        review_session_id = [string]$review.Value.session.id
    }
}

$case = Invoke-Json "$BaseUri/api/reconciliation-cases" "Post" @{ name = "Phase 4 synthetic acceptance" }
foreach ($document in $reviewed) {
    $case = Invoke-Json "$BaseUri/api/reconciliation-cases/$($case.Value.id)/documents/$($document.role)" "Put" @{
        documentId = $document.document_id
        reviewSessionId = $document.review_session_id
    } $case.ETag
}
$case = Invoke-Json "$BaseUri/api/reconciliation-cases/$($case.Value.id)/evaluations" "Post" @{
    quantityTolerance = 0
    unitPriceTolerance = 0.01
    taxTolerance = 0.01
    lineTotalTolerance = 0.01
    documentTotalTolerance = 0.01
} $case.ETag

$run = $case.Value.runs[0]
$expected = @($source.expected_findings | ForEach-Object {
    "$($_.rule_code)|$($_.left_role)|$($_.right_role)|$($_.field_key)|$($_.occurrence)"
} | Sort-Object)
$actual = @($run.findings | ForEach-Object {
    "$($_.ruleCode)|$($_.leftRole)|$($_.rightRole)|$($_.fieldKey)|$($_.occurrence)"
} | Sort-Object)
$difference = @(Compare-Object -ReferenceObject $expected -DifferenceObject $actual)
if ($difference.Count -ne 0 -or $actual.Count -ne 9) {
    throw "Finding identities differ from the v0.1.0 truth set: $($difference | Out-String)"
}
$perRule = @($run.findings | Group-Object ruleCode | Sort-Object Name | ForEach-Object {
    $ruleCode = $_.Name
    [ordered]@{
        rule_code = $ruleCode
        expected = @($source.expected_findings | Where-Object rule_code -eq $ruleCode).Count
        actual = $_.Count
    }
})
foreach ($finding in $run.findings) {
    if (-not $finding.leftFieldSnapshotId -or -not $finding.rightFieldSnapshotId) {
        throw "$($finding.ruleCode) is missing a compared field snapshot"
    }
}

$firstFinding = $run.findings[0]
$case = Invoke-Json "$BaseUri/api/reconciliation-cases/$($case.Value.id)/runs/$($run.id)/findings/$($firstFinding.id)/resolutions" "Post" @{
    action = "Accepted"
    reason = "Synthetic Phase 4 acceptance decision"
} $case.ETag
$resolved = $case.Value.runs[0].findings | Where-Object id -eq $firstFinding.id
if ($resolved.resolutions.Count -ne 1 -or $resolved.resolutions[0].reason -ne "Synthetic Phase 4 acceptance decision") {
    throw "The append-only finding resolution was not preserved"
}

$report = [ordered]@{
    report_schema_version = "1.0"
    dataset_id = $source.dataset_id
    dataset_version = $source.dataset_version
    rule_set_version = [string]$run.ruleSetVersion
    synthetic = $true
    claim_eligible = $false
    executed_at_utc = [DateTimeOffset]::UtcNow.ToString("O")
    case_id = [string]$case.Value.id
    documents = $reviewed
    expected_finding_count = $expected.Count
    actual_finding_count = $actual.Count
    metrics = [ordered]@{
        true_positives = 9
        false_positives = 0
        false_negatives = 0
        precision = 1.0
        recall = 1.0
        per_rule = $perRule
    }
    finding_identities = $actual
    compared_field_snapshots_present = $true
    append_only_resolution_verified = $true
    passed = $true
}
$directory = Split-Path -Parent $reportFile
[System.IO.Directory]::CreateDirectory($directory) | Out-Null
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportFile -Encoding utf8NoBOM
Write-Host "Phase 4 acceptance passed: 3 reviewed documents, 9 exact findings, 1 retained resolution."
Write-Host $reportFile
