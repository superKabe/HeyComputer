[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [ValidateSet('win-x64', 'win-arm64')]
    [string] $Runtime = 'win-x64',

    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$desktopProjects = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\HeyComputer.Desktop') -Filter '*.csproj' -File -ErrorAction Stop)

if ($desktopProjects.Count -ne 1) {
    throw "Expected exactly one desktop project but found $($desktopProjects.Count)."
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot "artifacts\portable-$Runtime"
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$artifactsPrefix = $artifactsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if ($resolvedOutput.Equals($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutput.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must be inside the project artifacts directory.'
}

$candidate = $resolvedOutput
while ($true) {
    if (Test-Path -LiteralPath $candidate) {
        $item = Get-Item -LiteralPath $candidate -Force
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Packaging refuses to use a reparse-point directory: $candidate"
        }
    }

    if ($candidate.Equals($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        break
    }
    $parent = [System.IO.Directory]::GetParent($candidate)
    if ($null -eq $parent -or
        (-not $parent.FullName.Equals($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
         -not $parent.FullName.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase))) {
        throw 'Unable to prove that the output path remains inside the artifacts directory.'
    }
    $candidate = $parent.FullName
}

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnetCommand) {
    $workspaceSdk = Join-Path (Split-Path -Parent $projectRoot) '.tools\dotnet10\dotnet.exe'
    if (-not (Test-Path -LiteralPath $workspaceSdk -PathType Leaf)) {
        throw 'The .NET SDK was not found on PATH or in the workspace-local tool directory.'
    }
    $dotnet = $workspaceSdk
}
else {
    $dotnet = $dotnetCommand.Source
}

if (Test-Path -LiteralPath $resolvedOutput) {
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}
New-Item -ItemType Directory -Path $resolvedOutput | Out-Null

$publishArgs = @(
    'publish', $desktopProjects[0].FullName,
    '--configuration', $Configuration,
    '--runtime', $Runtime,
    '--self-contained', 'true',
    '--output', $resolvedOutput,
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)

& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$manifestPath = Join-Path $resolvedOutput 'SHA256SUMS.txt'
$outputPrefix = $resolvedOutput.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$hashLines = Get-ChildItem -LiteralPath $resolvedOutput -File -Recurse |
    Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
    Sort-Object FullName |
    ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        $relativeName = $_.FullName.Substring($outputPrefix.Length).Replace([System.IO.Path]::DirectorySeparatorChar, '/')
        "$($hash.Hash.ToLowerInvariant())  $relativeName"
    }
# Windows PowerShell 5.1 does not recognize the newer utf8NoBOM encoding name.
Set-Content -LiteralPath $manifestPath -Value $hashLines -Encoding UTF8

$archivePath = Join-Path $artifactsRoot "HeyComputer-$Runtime-$Configuration-unsigned.zip"
if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}
Compress-Archive -Path (Join-Path $resolvedOutput '*') -DestinationPath $archivePath -CompressionLevel Optimal

Write-Host "Unsigned development package created: $archivePath"
Write-Warning 'Do not use this unsigned archive as a public release.'
