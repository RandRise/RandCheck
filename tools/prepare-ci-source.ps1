# Snapshot current source, including uncommitted implementation, without retained data.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$destination = Join-Path $root ('outputs/ci-source/' + [Guid]::NewGuid().ToString('N').Substring(0,12))
$candidates = @(& git -C $root ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate current source.' }
$files = $candidates | Sort-Object -Unique | Where-Object {
    ($_ -match '^(apps/|tools/|docs/|samples/|\.github/)' -or $_ -in @('global.json','pyproject.toml','.gitattributes','.gitignore','.dockerignore','.nvmrc','README.md')) -and
    $_ -notmatch '/(bin|obj|node_modules|__pycache__|\.next[^/]*|\.venv|\.vite|dist|cache)/' -and
    $_ -notmatch '(^|/)\.env' -and $_ -notmatch '\.(db|dump|pfx|p12|pem|key|log|trx|pyc|tsbuildinfo)$'
}
$inventory = foreach ($relative in $files) {
    $source = Join-Path $root $relative
    if (!(Test-Path -LiteralPath $source -PathType Leaf)) { continue }
    $cursor = Get-Item -LiteralPath $source
    while ($cursor.FullName -ne $root) {
        if ($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse source refused: $relative" }
        $cursor = $cursor.Parent ?? $cursor.Directory
    }
    $target = Join-Path $destination $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
    @{path=$relative;sha256=(Get-FileHash -LiteralPath $target).Hash.ToLowerInvariant()}
}
$inventory | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $destination 'source-inventory.json')
@{schema=1;root=$destination;inventorySha256=(Get-FileHash (Join-Path $destination 'source-inventory.json')).Hash} |
    ConvertTo-Json | Set-Content (Join-Path $destination '.ci-owner.json')
Write-Output $destination
