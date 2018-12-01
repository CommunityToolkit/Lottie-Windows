# LottieGen command line code generator for Lottie-Windows

LottieGen is a tool for generating C#, C++, and other outputs from Lottie .json files.

# .NET Core global tool
LottieGen is built as a [.NET Core global tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools), which means it will run anywhere .NET Core is available, including Linux and Mac.

## Installing
*The following commands are examples only - adjust the paths and versions as necessary ...*

The latest release version can be installed from NuGet:

    dotnet tool install lottiegen -g

Prerelease versions can be installed from NuGet:

    dotnet tool install lottiegen -g --version 1.0.0-prerelease01


CI builds can be installed from MyGet:

    dotnet tool install LottieGen -g --version 1.0.0-build.1.g26d7c6442f --add-source https://dotnet.myget.org/F/uwpcommunitytoolkit/api/v3/index.json 


Local builds can be installed from your bin\nupkg directory:

    dotnet tool install LottieGen -g --version 1.0.0-build.14.g1d4752119a --add-source f:\GitHub\Lottie-Windows\bin\nupkg

## Uninstalling
    dotnet tool uninstall -g LottieGen

# Help
All of the help for LottieGen is built into the tool.
    LottieGen -Help