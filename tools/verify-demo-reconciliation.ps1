# Exercise existing Phase 4 behavior only inside an owned disposable local demo.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Prepare','Verify')][string]$Stage,
    [Guid]$CaseId = [Guid]::Empty
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& "$PSScriptRoot/local-demo.ps1" status
if ($LASTEXITCODE -ne 0) { throw 'Demo status failed.' }
$owner = Get-Content (Join-Path $root '.demo-owner.json') -Raw | ConvertFrom-Json
$backend = (& docker inspect "$($owner.project)-backend-1" | ConvertFrom-Json)[0]
$ports = @($backend.NetworkSettings.Ports.'8080/tcp')
if ($LASTEXITCODE -ne 0 -or !$backend.State.Running -or
    $backend.Config.Labels.'com.docker.compose.project' -ne $owner.project -or
    $ports.Count -ne 1 -or $ports[0].HostIp -ne '127.0.0.1' -or $ports[0].HostPort -ne '8308') {
    throw 'This copy does not own the running loopback demo API.'
}
$api = 'http://127.0.0.1:8308'
$receiptPath = Join-Path $root 'reconciliation-journey.json'
$pack = Join-Path $root 'samples/phase-4/procurement-v0.1.0'
$truth = Get-Content (Join-Path $pack 'truth-set.json') -Raw | ConvertFrom-Json
function Assert-That($Condition, [string]$Message) { if (!$Condition) { throw $Message } }
function Write-Receipt($Receipt) { $Receipt | ConvertTo-Json -Depth 40 | Set-Content $receiptPath }
function Invoke-Action([string]$Path, $Body, [string]$Token) {
    Invoke-RestMethod "$api$Path" -Method Post -TimeoutSec 30 -ContentType 'application/json' `
        -Headers @{'If-Match'='"'+$Token+'"'} -Body ($Body | ConvertTo-Json)
}
if ($Stage -eq 'Prepare') {
    Assert-That (!(Test-Path $receiptPath)) 'Journey exists; use a fresh copy rather than overwriting evidence.'
    $documents = Invoke-RestMethod "$api/api/documents" -TimeoutSec 10
    Assert-That (@($documents).Count -eq 0) 'Prepare requires an empty disposable document store.'
    # Check all sample bytes before uploading anything.
    foreach ($sample in $truth.documents) {
        Assert-That ($sample.filename -match '^synthetic-[a-zA-Z0-9-]+\.pdf$') 'Unexpected fixture filename.'
        Assert-That ((Get-FileHash (Join-Path $pack $sample.filename)).Hash -eq $sample.sha256) 'Synthetic sample hash mismatch.'
    }
    $receipt = @{schema=1; synthetic=$true; project=$owner.project; prepared=$false; verified=$false; documents=@()}
    Write-Receipt $receipt
    foreach ($sample in $truth.documents) {
        $file = Get-Item (Join-Path $pack $sample.filename)
        $client = [Net.Http.HttpClient]::new()
        $client.Timeout = [TimeSpan]::FromSeconds(30)
        $multipart = [Net.Http.MultipartFormDataContent]::new()
        $content = [Net.Http.ByteArrayContent]::new([IO.File]::ReadAllBytes($file.FullName))
        $content.Headers.ContentType = [Net.Http.Headers.MediaTypeHeaderValue]::new('application/pdf')
        $multipart.Add($content, 'file', $file.Name)
        try {
            $response = $client.PostAsync("$api/api/documents", $multipart).GetAwaiter().GetResult()
            Assert-That ([int]$response.StatusCode -eq 202) 'Expected queued upload (202).'
            $document = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        } finally { $multipart.Dispose(); $client.Dispose() }
        # Retain partial progress if a later extraction/review fails.
        $entry = @{role=$sample.role; filename=$sample.filename; sha256=$sample.sha256; documentId=$document.id}
        $receipt.documents += $entry
        Write-Receipt $receipt
        for ($attempt=0; $attempt -lt 90; $attempt++) {
            $processing = Invoke-RestMethod "$api/api/documents/$($document.id)/processing" -TimeoutSec 10
            if ($processing.status -in @('Ready','Partial','Failed','Cancelled')) { break }
            Start-Sleep -Seconds 1
        }
        Assert-That ($processing.status -in @('Ready','Partial')) 'Synthetic extraction did not complete.'
        $stored = Invoke-RestMethod "$api/api/documents/$($document.id)" -TimeoutSec 10
        $expectedType = @{Quotation='quotation'; PurchaseOrder='purchase_order'; Invoice='invoice'}[$sample.role]
        Assert-That ($stored.documentType -eq $expectedType) 'Unexpected document classification.'
        $review = Invoke-RestMethod "$api/api/documents/$($document.id)/reviews" -Method Post -TimeoutSec 30
        foreach ($field in @($review.fields | Where-Object {$null -ne $_.machine})) {
            $review = Invoke-Action "/api/documents/$($document.id)/reviews/$($review.session.id)/actions" `
                @{action='Confirmed'; machineFieldValueId=$field.machine.id} $review.session.concurrencyToken
        }
        $review = Invoke-RestMethod "$api/api/documents/$($document.id)/reviews/$($review.session.id)/complete" `
            -Method Post -TimeoutSec 30 -Headers @{'If-Match'='"'+$review.session.concurrencyToken+'"'}
        Assert-That ($review.session.status -eq 'Completed') 'Review did not complete.'
        $entry.reviewSessionId = $review.session.id
        $entry.fieldCount = @($review.fields).Count
        Write-Receipt $receipt
    }
    $receipt.prepared = $true
    Write-Receipt $receipt
    Write-Output 'PASS: three existing synthetic PDFs uploaded, extracted and reviewed. Create the case in /reconciliation.'
} else {
    Assert-That ($CaseId -ne [Guid]::Empty) 'Verify requires the case ID shown in the browser.'
    $receipt = Get-Content $receiptPath -Raw | ConvertFrom-Json -AsHashtable
    Assert-That ($receipt.prepared -and !$receipt.verified) 'Prepare first; completed verification receipts are not overwritten.'
    $caseResponse = Invoke-WebRequest "$api/api/reconciliation-cases/$CaseId" -TimeoutSec 10
    $case = $caseResponse.Content | ConvertFrom-Json
    Assert-That (@($case.documents).Count -eq 3) 'Expected three pinned review revisions.'
    foreach ($document in $receipt.documents) {
        $link = @($case.documents | Where-Object role -eq $document.role)
        Assert-That ($link.Count -eq 1 -and $link[0].documentId -eq $document.documentId -and
            $link[0].reviewSessionId -eq $document.reviewSessionId) 'Case must pin this journey''s exact completed revisions.'
    }
    Assert-That (@($case.runs).Count -eq 1) 'Expected one focused comparison run.'
    $run = $case.runs[0]
    $expected = @($truth.expected_findings | ForEach-Object {"$($_.rule_code)|$($_.left_role)|$($_.right_role)|$($_.field_key)|$($_.occurrence)"} | Sort-Object)
    $actual = @($run.findings | ForEach-Object {"$($_.ruleCode)|$($_.leftRole)|$($_.rightRole)|$($_.fieldKey)|$($_.occurrence)"} | Sort-Object)
    Assert-That ($actual.Count -eq 9 -and @(Compare-Object $expected $actual).Count -eq 0) 'Findings differ from the unchanged Phase 4 truth set.'
    foreach ($finding in $run.findings) {
        $left = @($run.fields | Where-Object id -eq $finding.leftFieldSnapshotId)
        $right = @($run.fields | Where-Object id -eq $finding.rightFieldSnapshotId)
        Assert-That ($left.Count -eq 1 -and $right.Count -eq 1 -and $left[0].page -ge 1 -and
            $right[0].page -ge 1 -and $left[0].quote -and $right[0].quote) 'Finding lacks source-page evidence.'
    }
    $total = $run.findings | Where-Object ruleCode -eq 'AMT-002'
    Assert-That ([decimal]$total.leftValue -eq 8925 -and [decimal]$total.rightValue -eq 8820 -and
        $total.difference -eq 105 -and $total.tolerance -eq 0.01) 'PO/invoice total comparison differs from the fixture.'
    Assert-That (@($total.resolutions).Count -eq 1 -and $total.resolutions[0].action -eq 'Accepted' -and
        $total.resolutions[0].reason -eq 'Synthetic demo: reviewed the AED 105 difference; no payment approval.') 'Expected one reasoned browser decision on AMT-002.'
    foreach ($key in @('supplier_trn','currency','line_total')) {
        $values = @($run.fields | Where-Object {$_.key -eq $key -and $_.occurrence -eq $(if($key -eq 'line_total'){1}else{0})})
        Assert-That ($values.Count -eq 3 -and @($values.value | Sort-Object -Unique).Count -eq 1) 'Expected matching supplier/currency/first-line values.'
    }
    # A fresh API read must retain the frozen findings and append-only decision.
    $reread = (Invoke-WebRequest "$api/api/reconciliation-cases/$CaseId" -TimeoutSec 10).Content
    Assert-That ($reread -ceq $caseResponse.Content) 'Stored case changed between reads.'
    $receipt.caseId = $CaseId.ToString()
    $receipt.runId = $run.id
    $receipt.expectedFindingCount = 9
    $receipt.actualFindingCount = $actual.Count
    $receipt.findingIdentities = $actual
    $receipt.matchingFieldsVerified = @('supplier_trn','currency','line_total occurrence 1')
    $receipt.pageEvidenceVerified = $true
    $receipt.resolutionRetained = $true
    $receipt.freshReadIdentical = $true
    $receipt.case = $case
    $receipt.verified = $true
    $receipt.verifiedUtc = [DateTime]::UtcNow.ToString('o')
    Write-Receipt $receipt
    Write-Output 'PASS: nine exact findings, matching values, source pages and one retained browser decision.'
}
