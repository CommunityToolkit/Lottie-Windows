<#
.SYNOPSIS
Runs LottieGen over a corpus of Lottie files.

.DESCRIPTION
Runs the most recently built LottieGen tool over a corpus of Lottie
files, saving the result in a directory named according to the version
of LottieGen. The generated files can be used for comparing with a
baseline.

.PARAMETER CorpusDirectory
Path to a directory under which the Lottie files are stored.

.EXAMPLE
LottieGenCorpus D:\Lottie\Corpus\TestInputs
#>

[CmdletBinding()]
Param(
    [parameter(Mandatory=$true)]
    [string]
    [ValidateScript({test-path $_ -PathType Container})]
    $CorpusDirectory
)

# Find the most recently built LottieGen.dll.
$lottieGenDll = 
    Get-ChildItem LottieGen.dll -r -path "$PSScriptRoot\..\LottieGen\bin\" | 
    Sort-Object -Property 'CreationTime' -Desc | 
    Select-Object -first 1 |
    ForEach-Object 'FullName'


if (!$lottieGenDll)
{
    throw 'Could not find LottieGen.dll'
}

Write-Host -ForegroundColor Blue -NoNewline 'Using LottieGen binary: '
Write-Host -ForegroundColor Green $lottieGenDll

# Run LottieGen once to get its version number
$lottieGenVersion =
    &dotnet $lottieGenDll -help | 
        select-string '^Lottie for Windows Code Generator version ' | 
        ForEach-Object {$_  -replace '^Lottie for Windows Code Generator version (\d+\.\d+\.\d+\S*).*$','$1' }

# Create a directory to output.
$outputPath = join-path $PSScriptRoot "LottieGenOutput-$lottieGenVersion"

if (Test-Path $outputPath)
{
    Write-Host -ForegroundColor Red -NoNewline 'Output directory exists. Delete '
    Write-Host -ForegroundColor Yellow -NoNewline $outputPath
    Write-Host -ForegroundColor Red -NoNewline ' ? [y/N] '
    $userResponse = Read-Host
    if ($userResponse -match '^\s*y(es|\s*)$')
    {
        Write-Host -ForegroundColor Yellow "Deleting $outputPath"
        remove-item -path $outputPath -Recurse
    }

    if (Test-Path $outputPath)
    {
        throw "Output path already exists: $outputPath"
    }
}

New-Item $outputPath -ItemType Directory >$null

Write-Host -ForegroundColor Blue -NoNewline 'Output will be written to: '
Write-Host -ForegroundColor Green $outputPath

# Run LottieGen over everything in the corpus.
Write-Host -ForegroundColor Blue -NoNewline 'Executing: '
Write-Host -ForegroundColor Green "dotnet $lottieGenDll -i $CorpusDirectory\**json -o $outputPath -l cs -l cppcx -l lottiexml -l lottieyaml -l wincompxml -l dgml -l stats"
Write-Host -ForegroundColor Blue '...'

$errorLog = "$outputPath\Errors_and_Warnings.log"

$lottieGenExitCode = 99
$lottieGenTime = Measure-Command {
    &dotnet $lottieGenDll -i "$CorpusDirectory\**json" -o $outputPath -l cs -l cppcx -l lottiexml -l wincompxml -l dgml -l stats 2>&1 |
        select-string '(\:( warning )|( error ) L)|(Error: )' | Sort-Object -CaseSensitive | Out-File $errorLog -Width 240
    $lottieGenExitCode = $LASTEXITCODE
}

$minutes = [Math]::Floor($lottieGenTime.TotalMinutes)
$seconds = $lottieGenTime.TotalSeconds - ($minutes * 60)
$executionTime = "$seconds seconds"

if ($minutes -gt 0)
{
    if ($minutes -eq 1)
    {
        $executionTime = "1 minute $executionTime"
    }
    else 
    {
        $executionTime = "$minutes minutes $executionTime"
    }
}

write-host "Execution time: $executionTime"
write-host 'Errors and warnings written to:'
write-host $errorLog

if ($lottieGenExitCode -ne 0)
{
    write-host -ForegroundColor Red "Execution failed. See $errorLog. Error code: $lottieGenExitCode"
}
else 
{
    write-host 'Execution succeeded'
}