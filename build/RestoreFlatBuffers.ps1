[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$VcpkgRoot,

    [Parameter(Mandatory)]
    [string]$InstalledDir,

    [Parameter(Mandatory)]
    [string]$Triplet,

    [string]$HostTriplet = "x64-windows"
)

$ErrorActionPreference = "Stop"
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$VcpkgRoot = [IO.Path]::GetFullPath($VcpkgRoot)
$InstalledDir = [IO.Path]::GetFullPath($InstalledDir)
$localRoot = Join-Path $repoRoot ".vcpkg"
$vcpkg = Join-Path $VcpkgRoot "vcpkg.exe"
$baseline = (Get-Content (Join-Path $repoRoot "vcpkg.json") -Raw | ConvertFrom-Json).'builtin-baseline'

function Invoke-Checked([string]$Program, [string[]]$Arguments) {
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "'$Program $($Arguments -join ' ')' failed with exit code $LASTEXITCODE."
    }
}

if (!(Test-Path -LiteralPath $vcpkg)) {
    if ($VcpkgRoot -ne $localRoot) {
        throw "vcpkg.exe was not found at '$VcpkgRoot'. Bootstrap that installation or omit FlatBuffersVcpkgRoot to use automatic discovery."
    }

    if (!(Test-Path -LiteralPath $VcpkgRoot)) {
        Invoke-Checked "git" @("init", $VcpkgRoot)
        Invoke-Checked "git" @("-C", $VcpkgRoot, "remote", "add", "origin", "https://github.com/microsoft/vcpkg.git")
    }

    $remote = & git -C $VcpkgRoot remote get-url origin
    if ($LASTEXITCODE -ne 0 -or $remote -ne "https://github.com/microsoft/vcpkg.git") {
        throw "'$VcpkgRoot' must be a checkout of https://github.com/microsoft/vcpkg.git."
    }

    Invoke-Checked "git" @("-C", $VcpkgRoot, "fetch", "origin", $baseline)
    Invoke-Checked "git" @("-C", $VcpkgRoot, "checkout", "--detach", $baseline)
    Invoke-Checked (Join-Path $VcpkgRoot "bootstrap-vcpkg.bat") @("-disableMetrics")
}

if ($VcpkgRoot -eq $localRoot) {
    $shallow = & git -C $VcpkgRoot rev-parse --is-shallow-repository
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect the repo-local vcpkg checkout at '$VcpkgRoot'."
    }
    if ($shallow -eq "true") {
        Invoke-Checked "git" @("-C", $VcpkgRoot, "fetch", "--unshallow", "origin", $baseline)
    }
}

Invoke-Checked $vcpkg @(
    "install",
    "--triplet=$Triplet",
    "--host-triplet=$HostTriplet",
    "--x-wait-for-lock",
    "--x-manifest-root=$repoRoot",
    "--x-install-root=$InstalledDir"
)
