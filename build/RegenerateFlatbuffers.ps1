# Regenerates the FlatBuffers code for lottie_comp.fbs.
#
# The generated C# and C++ sources are checked in so that contributors do not
# need flatc installed in order to build. Run this script after any change to
# source/LottieFlatbuffer/lottie_comp.fbs, and check in the result.
#
# C# output must match the Google.FlatBuffers package available through the
# configured Microsoft feed. C++ output must match external/flatbuffers.

[CmdletBinding()]
param(
    # C# code must match the Google.FlatBuffers package version.
    [string]$CSharpFlatc = "flatc",

    # C++ code must match the headers in external/flatbuffers.
    [Parameter(Mandatory)]
    [string]$CppFlatc
)

$ErrorActionPreference = "Stop"

$expectedCSharpVersion = "25.2.10"
$expectedCppVersion = "25.12.19"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$schema = Join-Path $repoRoot "source/LottieFlatbuffer/lottie_comp.fbs"
$csharpOut = Join-Path $repoRoot "source/CompDataFlatbuffer/Generated"
$cppOut = Join-Path $repoRoot "dlls/LottieRuntime/Generated"

$actualCSharpVersion = (& $CSharpFlatc --version) -replace '^flatc version ', ''
if ($actualCSharpVersion.Trim() -ne $expectedCSharpVersion) {
    throw "C# flatc version $expectedCSharpVersion is required, but '$CSharpFlatc' is version $($actualCSharpVersion.Trim())."
}

$actualCppVersion = (& $CppFlatc --version) -replace '^flatc version ', ''
if ($actualCppVersion.Trim() -ne $expectedCppVersion) {
    throw "C++ flatc version $expectedCppVersion is required, but '$CppFlatc' is version $($actualCppVersion.Trim())."
}

New-Item -ItemType Directory -Force -Path $csharpOut, $cppOut | Out-Null

# --gen-onefile keeps the checked in C# to a single file rather than one file
# per type spread over a namespace directory tree.
& $CSharpFlatc --csharp --gen-onefile -o $csharpOut $schema
if ($LASTEXITCODE -ne 0) { throw "flatc failed to generate C#." }

& $CppFlatc --cpp --scoped-enums -o $cppOut $schema
if ($LASTEXITCODE -ne 0) { throw "flatc failed to generate C++." }

Write-Host "Generated FlatBuffers code from $schema"
