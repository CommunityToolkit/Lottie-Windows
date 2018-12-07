# LottieGen command line code generator for Lottie-Windows

LottieGen is a tool for generating C#, C++, and other outputs from Lottie .json files.

# .NET Core global tool
LottieGen is built as a [.NET Core global tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools), which means it will run anywhere .NET Core is available, including Linux and Mac.

## Installing
*The following commands are examples only - adjust the paths and versions as necessary ...*

The latest release version can be installed from NuGet (https://www.nuget.org/packages/LottieGen):

    dotnet tool install -g LottieGen

Prerelease versions can be installed from NuGet:

    dotnet tool install -g LottieGen --version 5.0.0-prerelease01


CI builds can be installed from MyGet (https://dotnet.myget.org/feed/uwpcommunitytoolkit/package/nuget/LottieGen):

    dotnet tool install -g LottieGen --add-source https://dotnet.myget.org/F/uwpcommunitytoolkit/api/v3/index.json --version 5.0.0-build.11


Local builds can be installed from your bin\nupkg directory:

    dotnet tool install -g LottieGen --add-source f:\GitHub\Lottie-Windows\bin\nupkg --version 5.0.0-build.11.g31523b44e4

## Uninstalling
    dotnet tool uninstall -g LottieGen

# Help
All of the help for LottieGen is built into the tool.

    LottieGen -Help