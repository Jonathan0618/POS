[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$FullSolution
)

$ErrorActionPreference = 'Stop'

$msbuild = Get-Command msbuild -ErrorAction SilentlyContinue
if (-not $msbuild) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere)) {
        throw 'MSBuild was not found. Install Visual Studio Build Tools for .NET Framework 4.8.'
    }

    $msbuildPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
        Select-Object -First 1
    if (-not $msbuildPath) {
        throw 'MSBuild was not found by Visual Studio Installer.'
    }
} else {
    $msbuildPath = $msbuild.Source
}

$root = Split-Path -Parent $PSScriptRoot
$target = if ($FullSolution) {
    Join-Path $root 'POS.sln'
} else {
    Join-Path $root 'POS.Tests\POS.Tests.csproj'
}

& $msbuildPath $target /t:Build /p:Configuration=$Configuration /m /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with exit code $LASTEXITCODE."
}

$testExecutable = Join-Path $root "POS.Tests\bin\$Configuration\POS.Tests.exe"
if (-not (Test-Path -LiteralPath $testExecutable)) {
    throw "Test executable was not produced at '$testExecutable'."
}

& $testExecutable
if ($LASTEXITCODE -ne 0) {
    throw "Tests failed with exit code $LASTEXITCODE."
}
