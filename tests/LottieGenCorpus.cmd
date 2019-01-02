@pushd %~dp0
@PowerShell.exe -file "%~dpn0.ps1" %*
@popd
@PAUSE