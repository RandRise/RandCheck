param([ValidateSet('All', 'DotNet', 'Node')][string]$Runtime = 'All')
$ErrorActionPreference = 'Stop'
if (-not [Environment]::Is64BitOperatingSystem -or [Environment]::OSVersion.Platform -ne 'Win32NT') {
    throw 'This bootstrap supports Windows x64. Use the pinned container/runtime versions on other platforms.'
}
$projectRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$baseline = Get-Content (Join-Path $PSScriptRoot 'runtime-baseline.json') -Raw | ConvertFrom-Json
function WorkspacePath([string]$relative) {
    $path = [IO.Path]::GetFullPath((Join-Path $projectRoot $relative))
    if (-not $path.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Runtime path escapes the workspace.'
    }
    return $path
}
function DownloadVerified([string]$url, [string]$file, [string]$algorithm, [string]$expected) {
    if (-not (Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file -Algorithm $algorithm).Hash -ine $expected) {
        Invoke-WebRequest -Uri $url -OutFile $file
    }
    if ((Get-FileHash -LiteralPath $file -Algorithm $algorithm).Hash -ine $expected) { throw 'Runtime checksum mismatch.' }
}
$downloads = WorkspacePath 'tmp/runtime-downloads'
New-Item -ItemType Directory -Force -Path $downloads | Out-Null
if ($Runtime -in @('All', 'DotNet')) {
    $target = WorkspacePath '.dotnet'
    if (Test-Path -LiteralPath $target) {
        if (-not (Test-Path "$target/sdk/$($baseline.DotNet.Sdk)") -or
            -not (Test-Path "$target/shared/Microsoft.AspNetCore.App/$($baseline.DotNet.Runtime)")) {
            throw 'Existing .dotnet directory does not match the baseline; it was not overwritten.'
        }
    } else {
        $zip = Join-Path $downloads "dotnet-$($baseline.DotNet.Sdk).zip"
        DownloadVerified $baseline.DotNet.Url $zip 'SHA512' $baseline.DotNet.Sha512
        Expand-Archive -LiteralPath $zip -DestinationPath $target
    }
}
if ($Runtime -in @('All', 'Node')) {
    $target = WorkspacePath '.node'
    if (Test-Path -LiteralPath $target) {
        if (-not (Test-Path "$target/node.exe") -or (& "$target/node.exe" --version) -ne "v$($baseline.Node.Version)") {
            throw 'Existing .node directory does not match the baseline; it was not overwritten.'
        }
    } else {
        $zip = Join-Path $downloads "node-$($baseline.Node.Version).zip"
        DownloadVerified $baseline.Node.Url $zip 'SHA256' $baseline.Node.Sha256
        $staging = WorkspacePath ('tmp/runtime-downloads/node-' + [Guid]::NewGuid().ToString('N'))
        Expand-Archive -LiteralPath $zip -DestinationPath $staging
        $source = WorkspacePath ([IO.Path]::GetRelativePath($projectRoot, (Join-Path $staging "node-v$($baseline.Node.Version)-win-x64")))
        Move-Item -LiteralPath $source -Destination $target
    }
}
Write-Output 'Project runtime files are ready. Dot-source tools/use-project-runtimes.ps1 in the shell used for builds and tests.'
