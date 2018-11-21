@pushd %~dp0
@PowerShell.exe -file "%~dp0build.ps1" %*
@popd
@PAUSE