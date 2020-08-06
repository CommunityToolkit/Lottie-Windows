# LottieGen command-line code generator for Lottie-Windows

LottieGen is a tool for generating C#, C++, and other outputs from Lottie / Bodymovin JSON files. LottieGen is built as a [.NET Core global tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools), which means it will run anywhere .NET Core is available, including Linux and Mac.

LottieGen requires [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.1) or later.

# Installing
*The following commands are examples only; adjust the paths and versions as necessary.*

## Install as a .NET Core global tool

The latest release version can be [installed from NuGet](https://www.nuget.org/packages/LottieGen):

    dotnet tool install -g LottieGen

A specific release version can be installed from NuGet:

    dotnet tool install -g LottieGen --version 6.1.0

CI builds can be [installed from MyGet](https://dotnet.myget.org/feed/uwpcommunitytoolkit/package/nuget/LottieGen):

    dotnet tool install -g LottieGen --add-source https://dotnet.myget.org/F/uwpcommunitytoolkit/api/v3/index.json --version 7.0.0-build.2

See the list of CI builds available on the [LottieGen MyGet page](https://dotnet.myget.org/feed/uwpcommunitytoolkit/package/nuget/LottieGen). Note that these builds may not be as stable as the official release builds.

Local builds can be installed from your bin\nupkg directory:

    dotnet tool install -g LottieGen --add-source f:\GitHub\Lottie-Windows\bin\nupkg --version 7.0.0-build.18.g31523b44e4

Local builds can also be run directly without installing, see below.

#### Updating
    dotnet tool update -g LottieGen

#### Uninstalling
    dotnet tool uninstall -g LottieGen

# Run without installation

Local builds can be run directly without installing as a .NET core global tool. Just install [.NET Core 3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1), build the LottieGen.sln solution and run the output.

    f:\GitHub\Lottie-Windows\LottieGen\bin\AnyCpu\Debug\netcoreapp3.1\lottiegen.exe

And of course you can copy LottieGen to a directory and run directly without installing it, for example:

    copy f:\Github\Lottie-Windows\LottieGen\bin\AnyCpu\Debug\netcoreapp3.1\* d:\mybuildtools


# Help
All of the help for LottieGen is built into the tool.

    LottieGen -Help
