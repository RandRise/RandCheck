# Copies current working source, including uncommitted files; never exports plain HEAD.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$id = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$destination = Join-Path $root "outputs/local-demo/$id"
if (Test-Path -LiteralPath $destination) { throw 'Snapshot destination already exists.' }
# Exact runtime-source allowlist, independent of Git's tracking status.
$files = @('global.json', 'pyproject.toml', '.dockerignore',
    'apps/api/Dockerfile', 'apps/backend/Directory.Build.props',
    'tools/local-demo.ps1', 'tools/verify-local-demo.ps1',
    'tools/verify-demo-reconciliation.ps1',
    'tools/test-local-demo-guards.ps1',
    'tools/use-project-runtimes.ps1', 'tools/install-project-runtimes.ps1', 'tools/runtime-baseline.json',
    'apps/web/package.json', 'apps/web/package-lock.json', 'apps/web/next.config.mjs',
    'apps/web/next-env.d.ts', 'apps/web/tsconfig.json', 'apps/web/AGENTS.md',
    'samples/phase-2/generated/native-two-page-invoice.pdf',
    'samples/phase-2/source-license-register.json', 'samples/phase-2/README.md',
    'samples/phase-2/licenses/bitstream-vera-license.txt')
$files += @('README.md', 'truth-set.json', 'generation-report.json',
    'synthetic-quotation-Q-260801.pdf', 'synthetic-purchase-order-PO-260801.pdf',
    'synthetic-tax-invoice-SYN-STD-260801.pdf') | ForEach-Object {
    "samples/phase-4/procurement-v0.1.0/$_"
}
foreach ($dir in @('apps/api/app', 'apps/backend/src', 'apps/web/app', 'apps/web/public', 'tools/demo')) {
    $path = Join-Path $root $dir
    if (!(Test-Path -LiteralPath $path)) { continue }
    $files += Get-ChildItem -LiteralPath $path -Recurse -File | ForEach-Object {
        $_.FullName.Substring($root.Length + 1).Replace('\', '/')
    } | Where-Object {
        $_ -notmatch '/(bin|obj|__pycache__|node_modules|\.next)/' -and
        $_ -notmatch '/\.env' -and $_ -notmatch '\.(pfx|p12|key|log|pyc|tsbuildinfo)$'
    }
}
$manifest = foreach ($relative in ($files | Sort-Object -Unique)) {
    $source = Join-Path $root $relative
    if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing source: $relative" }
    $cursor = Get-Item -LiteralPath $source
    while ($cursor.FullName -ne $root) {
        if ($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse source refused: $relative" }
        $cursor = $cursor.Parent ?? $cursor.Directory
    }
    $target = Join-Path $destination $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
    @{ path = $relative; sha256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $destination 'source-inventory.json')
@{ schema = 1; project = "uaedi-demo-$id"; root = $destination;
   composeSha256 = (Get-FileHash (Join-Path $destination 'tools/demo/compose.yml')).Hash;
   inventorySha256 = (Get-FileHash (Join-Path $destination 'source-inventory.json')).Hash;
   createdUtc = [DateTime]::UtcNow.ToString('o') } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination '.demo-owner.json')
New-Item -ItemType File -Path (Join-Path $destination 'tools/demo/empty.env') | Out-Null
Write-Output $destination
