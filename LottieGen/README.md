# LottieGen command-line code generator for Lottie-Windows

LottieGen is a tool for generating C#, C++, and other outputs from Lottie / Bodymovin JSON files. LottieGen is built as a [.NET Core global tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools), which means it will run anywhere .NET Core is available, including Linux and Mac.

LottieGen requires [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.1) or later.

# Run without installation

Local builds can be run directly. Just install [.NET Core 3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1), build the LottieGen.sln solution and run the output.

    f:\GitHub\Lottie-Windows\LottieGen\bin\AnyCpu\Debug\netcoreapp3.1\lottiegen.exe

And of course you can copy LottieGen to a directory and run directly without installing it, for example:

    copy f:\Github\Lottie-Windows\LottieGen\bin\AnyCpu\Debug\netcoreapp3.1\* d:\mybuildtools


# Install as a .NET Core global tool
*The following commands are examples only; adjust the paths and versions as necessary.*

LottieGen is built as a [.NET Core global tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools).

### Installing

The latest release version can be [installed from NuGet](https://www.nuget.org/packages/LottieGen):

    dotnet tool install -g LottieGen

A specific release version can be installed from NuGet:

    dotnet tool install -g LottieGen --version 6.1.0

CI builds can be installed from Azure DevOps:

    dotnet tool install -g LottieGen --add-source https://pkgs.dev.azure.com/dotnet/WindowsCommunityToolkit/_packaging/WindowsCommunityToolkit-MainLatest/nuget/v3/index.json --version 7.0.0-build.2

Note that these builds may not be as stable as the official release builds.

Local builds can be installed from your bin\nupkg directory:

    dotnet tool install -g LottieGen --add-source f:\GitHub\Lottie-Windows\bin\nupkg --version 7.0.0-build.18.g31523b44e4

Local builds can also be run directly without installing, see below.

### Updating
    dotnet tool update -g LottieGen

### Uninstalling
    dotnet tool uninstall -g LottieGen

# Help
All of the help for LottieGen is built into the tool.

    LottieGen -Help
