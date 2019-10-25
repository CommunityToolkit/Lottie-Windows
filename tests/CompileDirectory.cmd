@echo off
setlocal
echo Compiling all .cs files under %1
set cscPath="%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe"
for /r "%1" %%F in (*.cs) do @%cscPath% /noconfig @CompileDirectory.rsp "%%F" /out:"%%~dpnF.dll" & del "%%~dpnF.dll"

