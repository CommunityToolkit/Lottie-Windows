# LottieGen command-line code generator for Lottie-Windows

LottieGen is a tool for generating C#, C++, and other outputs from Lottie / Bodymovin JSON files. LottieGen is built as a [.NET global tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools), which means it will run anywhere .NET Core is available, including Linux and Mac.

LottieGen requires [.NET Core 7.0 SDK](https://dotnet.microsoft.com/download/dotnet-core/7.0) or later.

# Run without installation

Local builds can be run directly. Just install [.NET Core 7.0](https://dotnet.microsoft.com/download/dotnet-core/7.0), build the LottieGen.sln solution and run the output.

    f:\GitHub\Lottie-Windows\LottieGen\DotnetTool\bin\AnyCpu\Debug\net7.0\lottiegen.exe

And of course you can copy LottieGen to a directory and run directly without installing it, for example:

    copy f:\GitHub\Lottie-Windows\LottieGen\DotnetTool\bin\AnyCpu\Debug\net7.0\* d:\mybuildtools


# Install as a .NET Core global tool
*The following commands are examples only; adjust the paths and versions as necessary.*

LottieGen is built as a [.NET global tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools).

### Installing

The latest release version can be [installed from NuGet](https://www.nuget.org/packages/LottieGen):

    dotnet tool install -g LottieGen

A specific release version can be installed from NuGet:

    dotnet tool install -g LottieGen --version 7.0.0

CI builds can be installed from Azure DevOps. From the [main branch](https://dev.azure.com/dotnet/WindowsCommunityToolkit/_packaging?_a=package&feed=WindowsCommunityToolkit-MainLatest&package=LottieGen&protocolType=NuGet):

    dotnet tool install -g LottieGen --add-source https://pkgs.dev.azure.com/dotnet/WindowsCommunityToolkit/_packaging/WindowsCommunityToolkit-MainLatest/nuget/v3/index.json --version 7.0.0-build.2

From [PRs](https://dev.azure.com/dotnet/WindowsCommunityToolkit/_packaging?_a=package&feed=WindowsCommunityToolkit-PullRequests&protocolType=NuGet&package=CommunityToolkit.WinUI.LottieGen):

    dotnet tool install -g LottieGen --add-source https://pkgs.dev.azure.com/dotnet/WindowsCommunityToolkit/_packaging/WindowsCommunityToolkit-PullRequests/nuget/v3/index.json --version 7.0.0-build.2

Note that these builds may not be as stable as the official release builds.

Local builds can be installed from your bin\nupkg directory:

    dotnet tool install -g LottieGen --add-source f:\GitHub\Lottie-Windows\bin\nupkg

Local builds can also be run directly without installing, see above.

### Updating
    dotnet tool update -g LottieGen

### Uninstalling
    dotnet tool uninstall -g LottieGen

# Help
All of the help for LottieGen is built into the tool.

    LottieGen -Help
