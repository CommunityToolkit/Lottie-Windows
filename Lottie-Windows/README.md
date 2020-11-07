# Lottie-Windows
Lottie-Windows provides the [`LottieVisualSource`](https://docs.microsoft.com/dotnet/api/microsoft.toolkit.uwp.ui.lottie.lottievisualsource) which is consumed by the [`Microsoft.UI.Xaml.Controls.AnimatedVisualPlayer`](https://docs.microsoft.com/uwp/api/microsoft.ui.xaml.controls.animatedvisualplayer) to render Lottie JSON files.

The Lottie-Windows project generates a NuGet package for use by Windows apps.

## Package locations
* The [latest release and pre-release versions are on NuGet](https://www.nuget.org/packages/Microsoft.Toolkit.Uwp.UI.Lottie).
* The NuGets for the latest CI builds are published to Azure DevOps. Links here for the [main branch](https://pkgs.dev.azure.com/dotnet/WindowsCommunityToolkit/_packaging/WindowsCommunityToolkit-MainLatest/nuget/v3/index.json) and for [PRs](https://pkgs.dev.azure.com/dotnet/WindowsCommunityToolkit/_packaging/WindowsCommunityToolkit-PullRequests/nuget/v3/index.json). See the [Windows Community Toolkit wiki](https://github.com/windows-toolkit/WindowsCommunityToolkit/wiki/Preview-Packages) for details.
* The latest local build is output to the bin\nupkg directory in your repo directory.

## Usage

To get started using the Lottie-Windows library in your XAML, follow [this tutorial](https://docs.microsoft.com/windows/communitytoolkit/animations/lottie-scenarios/getting_started_json).
