# Building Lottie-Windows

## Prerequisites
* [.NET Core 2.2 SDK](https://dotnet.microsoft.com/download/dotnet-core/2.2) or later
* Windows SDK SV2 Update (22558) or later
* Visual Studio 2017 or later

## As Azure DevOps does it
Open a VS2017 Developer Command Prompt and run <code>build\build.bat</code>. This will build everything in RELEASE configuration, run some checks, and produce nuget packages. 

This is the slowest and most complete way to build. You should build this way to make sure your pull request can build on the official build system.

## From Visual Studio
Open the <code>Lottie-Windows.sln</code> in Visual Studio and build as you normally would. 

Use the <code>Debug</code> configuration to save a lot of time (the <code>Release</code> configuration of Lottie Viewer runs the .NET native compiler, which does a lot of slow optimization work).

## Just LottieGen
Turbocharge your workflow with the smaller <code>LottieGen\LottieGen.sln</code> in Visual Studio. This solution does not include the Lottie Viewer code or the DLLs, but it does include all the source needed to translating Lottie .json files to Windows.UI.Composition.

This solution was added as a useful subset for use on slow laptops. It can even be built with <code>Dotnet Build</code> with no Visual Studio installed.
