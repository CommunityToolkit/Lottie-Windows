# Regenerates the FlatBuffers code for lottie_comp.fbs.
#
# The generated C# and C++ sources are checked in so that contributors do not
# need flatc installed in order to build. Run this script after any change to
# source/LottieFlatbuffer/lottie_comp.fbs, and check in the result.
#
[CmdletBinding()]
param(
    [string]$VcpkgRoot = $env:VCPKG_ROOT,

    [string]$InstalledDir = (Join-Path $PSScriptRoot "..\vcpkg_installed"),

    [string]$HostTriplet = $(if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64" -or $env:PROCESSOR_ARCHITEW6432 -eq "ARM64") { "arm64-windows" } else { "x64-windows" })
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$schema = Join-Path $repoRoot "source\LottieFlatbuffer\lottie_comp.fbs"
$csharpOut = Join-Path $repoRoot "source\CompDataFlatbuffer\Generated"
$cppOut = Join-Path $repoRoot "source\LottieRuntime\Generated"

if (!$VcpkgRoot) {
    $VcpkgRoot = Join-Path $repoRoot ".vcpkg"
}

& (Join-Path $PSScriptRoot "RestoreFlatBuffers.ps1") -VcpkgRoot $VcpkgRoot `
    -InstalledDir $InstalledDir -Triplet $HostTriplet -HostTriplet $HostTriplet
$flatc = Join-Path $InstalledDir "$HostTriplet\tools\flatbuffers\flatc.exe"

New-Item -ItemType Directory -Force -Path $csharpOut, $cppOut | Out-Null

# --gen-onefile keeps the checked in C# to a single file rather than one file
# per type spread over a namespace directory tree.
& $flatc --csharp --gen-onefile -o $csharpOut $schema
if ($LASTEXITCODE -ne 0) { throw "flatc failed to generate C#." }

& $flatc --cpp --scoped-enums -o $cppOut $schema
if ($LASTEXITCODE -ne 0) { throw "flatc failed to generate C++." }

foreach ($path in @(
    (Join-Path $csharpOut "lottie_comp_generated.cs"),
    (Join-Path $cppOut "lottie_comp_generated.h")
)) {
    $contents = [IO.File]::ReadAllText($path)
    [IO.File]::WriteAllText($path, ($contents -replace '\r?\n', "`r`n"), [Text.UTF8Encoding]::new($false))
}

Write-Host "Generated FlatBuffers code from $schema"
