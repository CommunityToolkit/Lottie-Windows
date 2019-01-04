setlocal

:: Ensure there is no other LottieGen installed.
dotnet tool uninstall -g LottieGen

:: Find nupkg and parse the name
@for /f "tokens=2,3,4,5,6 delims=." %%A in ('dir /b %~dp0\..\bin\nupkg\LottieGen.*.nupkg') do @set PackageVersion=%%A.%%B.%%C.%%D.%%E

:: Install
+dotnet tool install LottieGen -g --version %PackageVersion% --add-source %~dp0\..\bin\nupkg