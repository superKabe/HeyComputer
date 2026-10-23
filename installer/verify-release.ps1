[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [ValidateSet('win-x64', 'win-arm64')]
    [string] $Runtime = 'win-x64',

    [switch] $SkipPackage
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$solutionPath = Join-Path $projectRoot 'HeyComputer.sln'
if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
    throw 'HeyComputer.sln is missing.'
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

$listedOutput = & $dotnet sln $solutionPath list
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to enumerate solution projects.'
}
$listedProjects = @($listedOutput | Where-Object { $_ -match '\.csproj$' } | ForEach-Object {
    [System.IO.Path]::GetFullPath((Join-Path $projectRoot $_.Trim()))
})
$discoveredProjects = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src'), (Join-Path $projectRoot 'tests') -Filter '*.csproj' -File -Recurse)
foreach ($project in $discoveredProjects) {
    if ($listedProjects -notcontains $project.FullName) {
        throw "Project is not included in HeyComputer.sln: $($project.FullName)"
    }
}

$prohibitedModelFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'models') -File -Recurse | Where-Object {
    $_.Extension.ToLowerInvariant() -in @('.onnx', '.pt', '.pth', '.bin', '.model')
})
if ($prohibitedModelFiles.Count -gt 0) {
    throw "Model binaries must not be committed or packaged: $($prohibitedModelFiles.FullName -join ', ')"
}

& $dotnet restore $solutionPath
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
& $dotnet build $solutionPath --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }
& $dotnet test $solutionPath --configuration $Configuration --no-build
if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }

if (-not $SkipPackage) {
    $originalPath = $env:PATH
    try {
        $env:PATH = (Split-Path -Parent $dotnet) + [System.IO.Path]::PathSeparator + $originalPath
        & (Join-Path $PSScriptRoot 'build-portable.ps1') -Configuration $Configuration -Runtime $Runtime
    }
    finally {
        $env:PATH = $originalPath
    }
    if ($LASTEXITCODE -ne 0) { throw 'Portable package build failed.' }

    $archivePath = Join-Path $projectRoot "artifacts\HeyComputer-$Runtime-$Configuration-unsigned.zip"
    & (Join-Path $PSScriptRoot 'verify-portable.ps1') -ArchivePath $archivePath
}

Write-Host 'Credential-free release verification passed.'
