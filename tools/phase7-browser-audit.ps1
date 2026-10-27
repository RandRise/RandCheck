param([Parameter(Mandatory)][guid]$PriceResultId, [Parameter(Mandatory)][guid]$PaymentResultId)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$seed = Get-Content (Join-Path $root 'outputs/phase-7/browser-seed-final.json') -Raw | ConvertFrom-Json
$api = 'http://127.0.0.1:5327'
function Require($condition, $message) { if (-not $condition) { throw $message } }
$historyResponse = Invoke-WebRequest "$api/api/documents/$($seed.documentId)/review-history"
$history = $historyResponse.Content | ConvertFrom-Json
Require ($history.sessions.Count -eq 1 -and $history.sessions[0].status -eq 'Completed') 'Review is not completed'
$prices = @($history.sessions[0].actions | Where-Object fieldKey -eq 'line_unit_price' | Sort-Object occurrenceIndex)
Require ($prices.Count -eq 2) 'Expected two reviewer prices'
Require ($prices[0].occurrenceIndex -eq 1 -and $prices[1].occurrenceIndex -eq 2) 'Incorrect row positions'
foreach ($price in $prices) {
    Require ($price.action -eq 'Added' -and $price.valueType -eq 'Decimal' -and $price.value -eq '400.00') 'Price type or value mismatch'
}
$correction = @($history.sessions[0].actions | Where-Object { $_.fieldKey -eq 'payment_terms' -and $_.action -eq 'Corrected' })
Require ($correction.Count -eq 1 -and $correction[0].value -eq 'خلال 30 يوماً') 'Missing Arabic correction'
$priceAnswer = Invoke-RestMethod "$api/api/cited-questions/$PriceResultId"
$paymentAnswer = Invoke-RestMethod "$api/api/cited-questions/$PaymentResultId"
foreach ($answer in @($priceAnswer, $paymentAnswer)) {
    Require ($answer.reviewedAnswer.status -eq 'Answered' -and -not $answer.answerGenerationEnabled) 'Reviewed answer unavailable'
    Require (@($answer.retrieval.hits | Where-Object documentId -ne $seed.documentId).Count -eq 0) 'Answer uses another document'
}
Require ($priceAnswer.reviewedAnswer.claims[0].text -eq 'Invoice line unit price: 400.00 AED.') 'Price answer mismatch'
$priceHit = @($priceAnswer.retrieval.hits | Where-Object content -eq 'line_unit_price[1]: 400.00')
Require ($priceHit.Count -eq 1 -and $priceHit[0].spans.Count -eq 0) 'Reviewer price acquired fabricated page evidence'
$priceEvidence = Invoke-RestMethod "$api/api/cited-questions/$PriceResultId/hits/$($priceHit[0].hitId)/evidence"
Require ($priceEvidence.review.action -eq 'Added' -and $null -eq $priceEvidence.page) 'Price provenance mismatch'
Require ($priceEvidence.review.reason -eq 'سعر الوحدة من الصفحة 1 — AED 400.00') 'Price reason changed'
Require ($paymentAnswer.reviewedAnswer.claims[0].text -eq 'Invoice payment terms: خلال 30 يوماً.') 'Arabic reviewed answer mismatch'
$paymentHit = @($paymentAnswer.retrieval.hits | Where-Object content -eq 'payment_terms[0]: خلال 30 يوماً')
Require ($paymentHit.Count -eq 1 -and $paymentHit[0].spans.Count -eq 1) 'Payment citation missing'
$span = $paymentHit[0].spans[0]
$paymentEvidence = Invoke-RestMethod "$api/api/cited-questions/$PaymentResultId/hits/$($paymentHit[0].hitId)/evidence?spanId=$($span.id)"
$source = Get-Content (Join-Path $root 'samples/phase-7/pdf-v0.1.0/mixed-native-two-page-extraction.json') -Raw | ConvertFrom-Json
$original = @($source.pages | Where-Object page_number -eq 2)[0].canonical_text
$page = $paymentEvidence.page
Require ($page.pageNumber -eq 2 -and $page.quote -eq 'خلال 30 يوما') 'Original OCR quote changed'
Require (($page.before + $page.quote + $page.after) -ceq $original) 'Canonical source changed'
$raw = [Text.Encoding]::UTF8.GetBytes($original)
Require ([Text.Encoding]::UTF8.GetString($raw, $page.byteStart, $page.byteEnd - $page.byteStart) -ceq $page.quote) 'UTF-8 evidence offsets changed'
$report = [ordered]@{
    scenario='Real browser + API + disposable PostgreSQL; frozen native extraction; no upload/worker replay'
    documentId=$seed.documentId; reviewId=$seed.reviewId
    priceResultId=$PriceResultId; paymentResultId=$PaymentResultId
    historyHttpStatus=$historyResponse.StatusCode; reviewStatus=$history.sessions[0].status
    prices=@($prices | Select-Object fieldKey,occurrenceIndex,action,valueType,value,reason)
    correctedPayment=$correction[0].value; priceAnswer=$priceAnswer.reviewedAnswer.claims[0].text
    pricePageQuotes=$priceHit[0].spans.Count; priceCitationAction=$priceEvidence.review.action
    paymentAnswer=$paymentAnswer.reviewedAnswer.claims[0].text
    originalPaymentQuote=$page.quote; originalPaymentPage=$page.pageNumber; exactUtf8Evidence=$true
    paidAnswerGenerationEnabled=$false
}
$output = Join-Path $root 'docs/evidence/phase-7/P7-browser-saved-workflow-20260922.json'
if (Test-Path -LiteralPath $output) { throw 'Refusing to overwrite an audit receipt' }
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $output -Encoding utf8
$historyResponse.Content | Set-Content (Join-Path $root 'outputs/phase-7/browser-history-final.json') -Encoding utf8
Write-Output "Saved workflow and UTF-8 citation checks passed: $output"
