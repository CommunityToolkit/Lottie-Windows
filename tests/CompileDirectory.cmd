@echo off
setlocal
if '%1' EQU '' (
   echo No directory provided.
   goto :eof
)

echo Compiling all .cs files under %1

:: The C# compiler on your machine.
set cscPath="%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe"

:: WinUI local copy. Needed because we can't mock IDynamicAnimatedVisualSource in C# due to the use of WinRT events.
set winuiWinmdPath="%homedrive%%homepath%\.nuget\packages\microsoft.ui.xaml\2.2.190917002\lib\uap10.0\Microsoft.UI.Xaml.winmd"

for /r "%1" %%F in (*.cs) do @%cscPath% /noconfig @CompileDirectory.rsp /reference:%winuiWinmdPath% "%%F" /out:"%%~dpnF.dll" & del "%%~dpnF.dll"

