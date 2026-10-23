[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ArchivePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$resolvedArchive = [System.IO.Path]::GetFullPath($ArchivePath)
if (-not (Test-Path -LiteralPath $resolvedArchive -PathType Leaf)) {
    throw "Portable archive does not exist: $resolvedArchive"
}
if ([System.IO.Path]::GetExtension($resolvedArchive) -ne '.zip') {
    throw 'Portable archive must be a ZIP file.'
}

$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$verificationRoot = Join-Path $temporaryRoot ('heycomputer-package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $verificationRoot | Out-Null

try {
    Expand-Archive -LiteralPath $resolvedArchive -DestinationPath $verificationRoot

    $executable = Join-Path $verificationRoot 'HeyComputer.exe'
    $manifestPath = Join-Path $verificationRoot 'SHA256SUMS.txt'
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw 'Portable archive does not contain HeyComputer.exe.'
    }
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw 'Portable archive does not contain SHA256SUMS.txt.'
    }

    $blockedExtensions = @('.pfx', '.snk', '.key', '.pem', '.onnx', '.pt', '.pth', '.bin', '.model')
    $blockedNames = @('.env', 'secrets.json', 'credentials.json')
    foreach ($file in Get-ChildItem -LiteralPath $verificationRoot -File -Recurse) {
        if ($blockedExtensions -contains $file.Extension.ToLowerInvariant() -or
            $blockedNames -contains $file.Name.ToLowerInvariant()) {
            throw "Portable archive contains prohibited release material: $($file.Name)"
        }
    }

    $packageRoot = $verificationRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $verified = 0
    foreach ($line in Get-Content -LiteralPath $manifestPath) {
        if ($line -notmatch '^([0-9a-fA-F]{64})  (.+)$') {
            throw "Malformed checksum line: $line"
        }

        $expected = $Matches[1]
        $relativeName = $Matches[2]
        $target = [System.IO.Path]::GetFullPath((Join-Path $verificationRoot $relativeName))
        if (-not $target.StartsWith($packageRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Checksum path escapes the package: $relativeName"
        }
        if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
            throw "Checksum target is missing: $relativeName"
        }

        $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
        if (-not $actual.Equals($expected, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Checksum mismatch: $relativeName"
        }
        $verified++
    }

    if ($verified -eq 0) {
        throw 'Checksum manifest did not contain any files.'
    }

    Write-Host "Portable archive verified: $verified files; no bundled models, keys, or credential files."
}
finally {
    $resolvedVerification = [System.IO.Path]::GetFullPath($verificationRoot)
    $safePrefix = $temporaryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if ($resolvedVerification.StartsWith($safePrefix, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedVerification).StartsWith('heycomputer-package-', [System.StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $resolvedVerification -Recurse -Force -ErrorAction SilentlyContinue
    }
}
