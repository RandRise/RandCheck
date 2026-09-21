param([Parameter(Mandatory)][string]$Results)
$ErrorActionPreference = 'Stop'
$version='1.7.12'
if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') { throw 'Workflow lint bootstrap supports x64 only.' }
if ($IsWindows) {
    $file="actionlint_${version}_windows_amd64.zip"
    $expected='6e7241b51e6817ea6a047693d8e6fed13b31819c9a0dd6c5a726e1592d22f6e9'
    $executable='actionlint.exe'
} elseif ($IsLinux) {
    $file="actionlint_${version}_linux_amd64.tar.gz"
    $expected='8aca8db96f1b94770f1b0d72b6dddcb1ebb8123cb3712530b08cc387b349a3d8'
    $executable='actionlint'
} else { throw 'Unsupported workflow lint host.' }
$archive=Join-Path $Results $file
Invoke-WebRequest "https://github.com/rhysd/actionlint/releases/download/v$version/$file" -OutFile $archive
if ((Get-FileHash $archive).Hash -ine $expected) { throw 'Actionlint checksum mismatch.' }
if ($IsWindows) { Expand-Archive -LiteralPath $archive -DestinationPath $Results }
else { & tar -xzf $archive -C $Results; if ($LASTEXITCODE -ne 0) { throw 'Actionlint extraction failed.' } }
& (Join-Path $Results $executable) -shellcheck= -pyflakes= .github/workflows/ci.yml
if ($LASTEXITCODE -ne 0) { throw 'Workflow validation failed.' }
@{passed=$true;actionlint=$version;sha256=$expected} | ConvertTo-Json | Set-Content (Join-Path $Results 'workflow-checks.json')
