# LottieRuntime tool

`LottieRuntime.exe` loads FlatBuffer compositions through the registration-free
`ILottieCompositionLoader` COM interface.

## Build

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\Tools\Launch-VsDevShell.ps1" -SkipAutomaticLocation
msbuild .\dlls\LottieRuntimeTool\LottieRuntime.vcxproj /p:Platform=x64 /p:Configuration=Release
```

The executable is written to:

```text
dlls\LottieRuntimeTool\x64\Release\LottieRuntime.exe
```

## Convert Lottie JSON

```powershell
$output = Join-Path $env:TEMP "lottie-runtime"
New-Item -ItemType Directory -Force $output | Out-Null

dotnet run --project .\LottieGen\DotnetTool\LottieGen.csproj -- `
    -InputFile .\LottieSamples\Assets\LottieLogo1.json `
    -Language flatbuffer `
    -OutputFolder $output
```

The command writes `$output\LottieLogo1.lcomp`.

## Load, inspect, or display

Validate the composition:

```powershell
.\dlls\LottieRuntimeTool\x64\Release\LottieRuntime.exe `
    "$output\LottieLogo1.lcomp"
```

Print the interpreted Composition object hierarchy:

```powershell
.\dlls\LottieRuntimeTool\x64\Release\LottieRuntime.exe `
    "$output\LottieLogo1.lcomp" --dump
```

Display and animate the interpreted visual:

```powershell
.\dlls\LottieRuntimeTool\x64\Release\LottieRuntime.exe `
    "$output\LottieLogo1.lcomp" --show
```
