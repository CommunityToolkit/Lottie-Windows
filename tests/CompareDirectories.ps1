<#
.SYNOPSIS
Compares the contents of 2 directory trees.

.DESCRIPTION
Recursively compares all of the files and directories at 2 given paths
and reports on any differences. This is useful for determining whether
the output of a test run has regressed from a baseline.

.PARAMETER Directory1
Path to the first directory.

.PARAMETER Directory2
Path to the second directory.

.EXAMPLE
CompareDirectories Baseline TestOutput
#>

[CmdletBinding()]
Param(
    [parameter(Mandatory=$true)]
    [ValidateScript({test-path $_ -PathType Container})]
    $Directory1,
    [parameter(Mandatory=$true)]
    [ValidateScript({test-path $_ -PathType Container})]
    $Directory2
)

$dir1Color = 'Green'
$dir2Color = 'Magenta'

write-host -NoNewLine 'Comparing '
write-host -NoNewLine -ForegroundColor $dir1Color $Directory1
write-host -NoNewLine ' with '
write-host -ForegroundColor $dir2Color $Directory2

function IndexDirectoryTree
{
    Param([IO.DirectoryInfo]$dir)

    $directoryPrefixLength = $dir.FullName.Length + 1

    $result = @{}
    $children = Get-ChildItem -r $dir

    $itemCount = 0
    foreach($item in $children)
    {
        $relativePath = $item.FullName.Substring($directoryPrefixLength)
        $hash = Get-FileHash $item.FullName
        if ($hash)
        {
            $result.Add($relativePath, $hash.Hash)
        }

        if ($itemCount % 100 -eq 0)
        {
            $percentComplete = 100 * $itemCount / $children.Count
            Write-Progress -Activity "Examining files in $dir" -PercentComplete $percentComplete -Status "$itemCount / $($children.Count)"
        }

        $itemCount++
    }

    Write-Progress -Activity "Examining files in $dir" -Completed
    $result
}

$dir1Contents = IndexDirectoryTree $Directory1
$dir2Contents = IndexDirectoryTree $Directory2

if ($dir1Contents.Count -ne $dir2Contents.Count)
{
    $hasErrors = $true
    write-host -ForegroundColor Red "Directories have different numbers of items ($($dir1Contents.Count) vs $($dir2Contents.Count))"
}


foreach($item in $dir1Contents.GetEnumerator() | sort-object 'Key')
{
    $key = $item.Key

    if (!$dir2Contents.ContainsKey($key))
    {
        write-host -ForegroundColor $dir1Color "Only in $($Directory1): $key"
        $hasErrors = $true
    }
    elseif ($item.Value -ne $dir2Contents[$key])
    {
        write-host -NoNewLine -ForegroundColor DarkYellow 'Different: '
        write-host -NoNewLine -ForegroundColor $dir1Color (Join-Path $Directory1 $key)
        write-host -NoNewLine ' '
        write-host -ForegroundColor $dir2Color (Join-Path $Directory2 $key)

        $hasErrors = $true
    }
}

foreach($item in $dir2Contents.GetEnumerator() | sort-object 'Key')
{
    $key = $item.Key

    if (!$dir1Contents.ContainsKey($key))
    {
        write-host -ForegroundColor $dir2Color "Only in $($Directory2): $key"
        $hasErrors = $true
    }
}

if ($hasErrors)
{
    write-host -ForegroundColor Red 'Directories are not equal'
}
else 
{
    write-host 'Directories are equal'
}
