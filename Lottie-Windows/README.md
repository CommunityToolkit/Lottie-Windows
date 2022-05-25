# Lottie-Windows
Lottie-Windows provides the [`LottieVisualSource`](https://docs.microsoft.com/dotnet/api/CommunityToolkit.WinUI.lottie.lottievisualsource) which is consumed by the [`Microsoft.UI.Xaml.Controls.AnimatedVisualPlayer`](https://docs.microsoft.com/uwp/api/microsoft.ui.xaml.controls.animatedvisualplayer) to render Lottie JSON files.

The Lottie-Windows project generates a NuGet package for use by Windows apps.

## Package locations
* The [latest release and pre-release versions are on NuGet](https://www.nuget.org/packages/CommunityToolkit.WinUI.Lottie).
* The NuGets for the latest CI builds are published to Azure DevOps. Links here for the [main branch](https://dev.azure.com/dotnet/WindowsCommunityToolkit/_packaging?_a=package&feed=WindowsCommunityToolkit-MainLatest&protocolType=NuGet&package=CommunityToolkit.WinUI.Lottie) and for [PRs](https://dev.azure.com/dotnet/WindowsCommunityToolkit/_packaging?_a=package&feed=WindowsCommunityToolkit-PullRequests&protocolType=NuGet&package=CommunityToolkit.WinUI.Lottie).
See the [Windows Community Toolkit wiki](https://github.com/windows-toolkit/WindowsCommunityToolkit/wiki/Preview-Packages) for details.
* The latest local build is output to the bin\nupkg directory in your repo directory.

## Usage

To get started using the Lottie-Windows library in your XAML, follow [this tutorial](https://docs.microsoft.com/windows/communitytoolkit/animations/lottie-scenarios/getting_started_json).
