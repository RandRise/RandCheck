[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('Upload','Review','Restart','Empty')][string]$Stage)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
# Validate ownership/configuration before touching the fixed demo API.
& "$PSScriptRoot/local-demo.ps1" status
if ($LASTEXITCODE -ne 0) { throw 'Demo status failed.' }
$owner = Get-Content (Join-Path $root '.demo-owner.json') -Raw | ConvertFrom-Json
$backend = (& docker inspect "$($owner.project)-backend-1" | ConvertFrom-Json)[0]
if ($LASTEXITCODE -ne 0 -or !$backend.State.Running -or
    $backend.Config.Labels.'com.docker.compose.project' -ne $owner.project -or
    $backend.NetworkSettings.Ports.'8080/tcp'[0].HostIp -ne '127.0.0.1' -or
    $backend.NetworkSettings.Ports.'8080/tcp'[0].HostPort -ne '8308') { throw 'This copy does not own the running loopback demo API.' }
$api = 'http://127.0.0.1:8308'
$receiptPath = Join-Path $root 'journey.json'
function Assert-That($Condition, [string]$Message) { if (!$Condition) { throw $Message } }
function Read-Workspace($Id) { Invoke-RestMethod "$api/api/documents/$Id/review" }
switch ($Stage) {
    Empty {
        $documents = Invoke-RestMethod "$api/api/documents"
        Assert-That (@($documents).Count -eq 0) 'Reset/initial seed must contain no documents.'
        Write-Output 'PASS: empty disposable document store.'
    }
    Upload {
        if (Test-Path -LiteralPath $receiptPath) { throw 'Journey already exists; prepare another copy for another verification.' }
        $sample = Join-Path $root 'samples/phase-2/generated/native-two-page-invoice.pdf'
        $client = [Net.Http.HttpClient]::new()
        $multipart = [Net.Http.MultipartFormDataContent]::new()
        $file = [Net.Http.ByteArrayContent]::new([IO.File]::ReadAllBytes($sample))
        $file.Headers.ContentType = [Net.Http.Headers.MediaTypeHeaderValue]::new('application/pdf')
        $multipart.Add($file, 'file', 'native-two-page-invoice.pdf')
        try {
            $response = $client.PostAsync("$api/api/documents", $multipart).GetAwaiter().GetResult()
            Assert-That ([int]$response.StatusCode -eq 202) "Upload must be queued (202); received $([int]$response.StatusCode)."
            $document = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        } finally { $multipart.Dispose(); $client.Dispose() }
        for ($i = 0; $i -lt 90; $i++) {
            $document = Invoke-RestMethod "$api/api/documents/$($document.id)"
            if ($document.status -in @('Ready','Failed','NeedsReview')) { break }
            Start-Sleep -Seconds 1
        }
        $workspace = Read-Workspace $document.id
        $total = $workspace.fields | Where-Object fieldKey -eq 'total'
        Assert-That ($total.machine.value -eq '1,050.00') 'Expected native extraction total 1,050.00.'
        @{documentId=$document.id; extractionRunId=$workspace.extractionRunId;
          sampleSha256=(Get-FileHash $sample).Hash; uploadStatus=202; machineTotal=$total.machine.value;
          uploadedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content $receiptPath
        Write-Output "PASS: upload and local extraction, document $($document.id)."
    }
    Review {
        $receipt = Get-Content $receiptPath -Raw | ConvertFrom-Json -AsHashtable
        $id = $receipt.documentId
        $workspace = Invoke-RestMethod "$api/api/documents/$id/reviews" -Method Post
        foreach ($field in @($workspace.fields | Where-Object { $null -ne $_.machine })) {
            $action = @{action='Confirmed'; machineFieldValueId=$field.machine.id}
            if ($field.fieldKey -eq 'total') {
                $action = @{action='Corrected';machineFieldValueId=$field.machine.id;valueType='Decimal';
                    value='1155.00';reason='Synthetic walkthrough correction; not an accuracy label.'}
            }
            $workspace = Invoke-RestMethod "$api/api/documents/$id/reviews/$($workspace.session.id)/actions" -Method Post -ContentType 'application/json' -Headers @{'If-Match'='"'+$workspace.session.concurrencyToken+'"'} -Body ($action | ConvertTo-Json)
        }
        $workspace = Invoke-RestMethod "$api/api/documents/$id/reviews/$($workspace.session.id)/complete" -Method Post -Headers @{'If-Match'='"'+$workspace.session.concurrencyToken+'"'}
        $total = $workspace.fields | Where-Object fieldKey -eq 'total'
        Assert-That ($workspace.session.status -eq 'Completed' -and $total.machine.value -eq '1,050.00' -and $total.effective.value -eq '1155.00') 'Review completion/immutable machine value mismatch.'
        $receipt.reviewId = $workspace.session.id
        $receipt.reviewCompleted = $true
        $receipt.effectiveTotal = $total.effective.value
        $receipt.beforeRestart = $workspace
        $receipt.beforeRestartJson = (Invoke-WebRequest "$api/api/documents/$id/review").Content
        $receipt | ConvertTo-Json -Depth 30 | Set-Content $receiptPath
        Write-Output 'PASS: completed append-only review; machine 1,050.00, effective 1155.00.'
    }
    Restart {
        $receipt = Get-Content $receiptPath -Raw | ConvertFrom-Json -AsHashtable
        Assert-That $receipt.reviewCompleted 'Complete the review before restart.'
        & "$PSScriptRoot/local-demo.ps1" down
        & "$PSScriptRoot/local-demo.ps1" up
        $expected = $receipt.beforeRestartJson
        $actual = (Invoke-WebRequest "$api/api/documents/$($receipt.documentId)/review").Content
        Assert-That ($actual -ceq $expected) 'Full review workspace changed after container recreation.'
        $sourceHashes = @(& docker exec "$($owner.project)-backend-1" find /data/uploads -type f -name '*.pdf' -exec sha256sum '{}' ';')
        Assert-That ($LASTEXITCODE -eq 0 -and @($sourceHashes | Where-Object { $_.StartsWith($receipt.sampleSha256.ToLowerInvariant()) }).Count -eq 1) 'Original uploaded bytes were not preserved.'
        $receipt.restartVerified = $true
        $receipt.sourceBytesPreserved = $true
        $receipt.restartUtc = [DateTime]::UtcNow.ToString('o')
        $receipt | ConvertTo-Json -Depth 30 | Set-Content $receiptPath
        Write-Output 'PASS: complete workspace identical after down/up container recreation.'
    }
}

