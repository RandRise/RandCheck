[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$validationRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$validationOutput = Join-Path $validationRoot 'outputs/scale-integration-20261023'
$settings = Get-Content (Join-Path $validationOutput 'runtime-settings.json') -Raw | ConvertFrom-Json
$seed = Get-Content (Join-Path $validationOutput 'seed.json') -Raw | ConvertFrom-Json
if ($seed.kind -ne 'synthetic-local-validation' -or $seed.database -ne 'uaedi_validation_20261023' -or
    $settings.connection -notmatch '^Host=127\.0\.0\.1;Port=55623;Database=uaedi_validation_20261023;') {
    throw 'Owned synthetic validation settings required.'
}
foreach ($port in @(8310, 3310)) {
    $probe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $port)
    try { $probe.Start() } finally { $probe.Stop() }
}
$env:DOTNET_ROOT = Join-Path $validationRoot '.dotnet'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:8310'
$env:Security__Mode = 'Development'
$env:DevelopmentData__Seed = 'false'
$env:ConnectionStrings__Database = $settings.connection
$env:ApiDatabase__Enabled = 'true'
$env:ApiDatabase__DatabaseRole = $settings.role
$env:Storage__UploadDirectory = Join-Path $validationOutput 'uploads'
$env:ExtractionWorker__Enabled = 'false'
$env:StorageMaintenance__Enabled = 'false'
$env:Cors__Origins__0 = 'http://127.0.0.1:3310'
$env:AiService__BaseUrl = 'http://127.0.0.1:8010'
$env:AiService__AdapterVersion = 'local-1.6.0'
$env:AiService__WorkerToken = 'synthetic_validation_worker_only'
$apiDirectory = Join-Path $validationRoot 'apps/backend/src/UaeDocumentIntelligence.Api'
$apiDll = Join-Path $apiDirectory 'bin/Debug/net8.0/UaeDocumentIntelligence.Api.dll'
$api = Start-Process (Join-Path $validationRoot '.dotnet/dotnet.exe') -ArgumentList ('"' + $apiDll + '"') -WorkingDirectory $apiDirectory -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $validationOutput 'api.log') -RedirectStandardError (Join-Path $validationOutput 'api.err.log')
$env:AUTH_MODE = 'Development'
$env:NEXT_PUBLIC_API_URL = 'http://127.0.0.1:8310'
$env:NEXT_DIST_DIR = '.next-validation-20261023'
$env:NEXT_TELEMETRY_DISABLED = '1'
try {
    $web = Start-Process (Join-Path $validationRoot '.node/node.exe') -ArgumentList 'node_modules/next/dist/bin/next','dev','--webpack','--hostname','127.0.0.1','--port','3310' -WorkingDirectory (Join-Path $validationRoot 'apps/web') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $validationOutput 'web.log') -RedirectStandardError (Join-Path $validationOutput 'web.err.log')
} catch { $api.Kill(); throw }
@{ apiPid = $api.Id; webPid = $web.Id; apiStartedUtc = $api.StartTime.ToUniversalTime().ToString('o'); webStartedUtc = $web.StartTime.ToUniversalTime().ToString('o'); root = $validationRoot; database = $seed.database } | ConvertTo-Json | Set-Content (Join-Path $validationOutput 'processes.json')
Write-Output 'Started owned validation processes: web 3310, API 8310. Development identity; workers disabled.'
