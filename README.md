# Lottie-Windows

Lottie-Windows is a library for rendering [Adobe After Effects](https://www.adobe.com/products/aftereffects.html) animations natively in your application. This project adds Windows to the [Lottie](http://airbnb.io/lottie/) family of tools also targeting [Android](https://github.com/airbnb/lottie-android), [iOS](https://github.com/airbnb/lottie-ios), and [Web](https://github.com/airbnb/lottie-web).

Lottie simplifies the design-to-code workflow for bringing engaging, interactive vector animations to your Windows applications, with significant improvements in terms of performance, quality, and engineering efficiency over traditional approaches such as gifs, manually coded animations, etc. Lottie-Windows uses the [Windows.UI.Composition APIs](https://docs.microsoft.com/windows/uwp/composition/visual-layer) to provide smooth 60fps animations and resolution-independent vector graphics.

![Lottie-Windows Gif](/images/LottieWindows_Intro.gif)

Lottie-Windows consists of 3 related products:
* **[Lottie-Windows](/Lottie-Windows)** library for parsing and translating [Bodymovin](https://aescripts.com/bodymovin/) JSON files
* **[LottieGen](/LottieGen)** command-line tool for generating C# or C++ code to be used instead of JSON
* **[Lottie Viewer](/LottieViewer)** application for previewing JSON and also generating code 

This repo also contains source code for **[samples](/LottieViewer)**.

## <a name="quickstart"></a> Quick start

There are **two** options to integrate Lottie animations into your **WinUI 3** or **UWP** project.

**Option #1, using dynamic loader**
1. Install `CommunityToolkit.WinUI.Lottie` nuget package for WinUI project (or `CommunityToolkit.Uwp.Lottie` for UWP project).
2. If you are using C# you may also need to install `Microsoft.Graphics.Win2D` **(version 1.0.5 or below)** for WinUI project (or `Win2D.uwp` and `Microsoft.UI.Xaml` for UWP project).
3. In your `.xaml` markup file add:
    ```xml
        ...
        xmlns:lottie="using:CommunityToolkit.WinUI.Lottie"
        ...
        <AnimatedVisualPlayer>
            <lottie:LottieVisualSource UriSource="<asset path or web link to a json file>" />
        </AnimatedVisualPlayer>
    ```
    or for UWP project:
    ```xml
        ...
        xmlns:muxc="using:Microsoft.UI.Xaml.Controls"
        xmlns:lottie="using:CommunityToolkit.Uwp.Lottie"
        ...
        <muxc:AnimatedVisualPlayer>
            <lottie:LottieVisualSource UriSource="<asset path or web link to a json file>" />
        </muxc:AnimatedVisualPlayer>
    ```
**Option #2, using codegen (recommended)**
1. Install codegen tool using `dotnet tool install lottiegen` in powershell
2.  Run codegen tool `lottiegen -InputFile MyAnimation.json -Language cs -WinUIVersion 3`
    - For UWP projects use `-WinUIVersion 2.X` depending on the version of `Microsoft.UI.Xaml`
    - Other language options: `cppwinrt` and `cppcx`
3. Add generated source files to the project
4. Install packages from step 2 of Option #1 if needed.
5. In your `.xaml` markup file add:
    ```xml
        ...
        xmlns:animatedvisuals="using:AnimatedVisuals"
        ...
        <AnimatedVisualPlayer>
            <animatedvisuals:MyAnimation/>
        </AnimatedVisualPlayer>
    ```
    or for UWP project:
    ```xml
        ...
        xmlns:muxc="using:Microsoft.UI.Xaml.Controls"
        xmlns:animatedvisuals="using:AnimatedVisuals"
        ...
        <muxc:AnimatedVisualPlayer>
            <animatedvisuals:MyAnimation/>
        </muxc:AnimatedVisualPlayer>
    ```

## <a name="supported"></a> Supported SDKs
* May 2019 Update (18362) and later

## <a name="documentation"></a> Documentation
* [Documentation and Tutorials](https://aka.ms/lottiedocs)
* [Lottie Samples](https://aka.ms/lottiesamples)
* [Lottie Viewer](https://aka.ms/lottieviewer)

## Build Status
| Package | Branch | Status | Latest nuget version |
| ------ | ------ | ------ | ------ |
| CommunityToolkit.WinUI.Lottie | main | [![Build Status](https://dev.azure.com/dotnet/WindowsCommunityToolkit/_apis/build/status/Microsoft.Toolkit.Uwp.UI.Lottie?branchName=main)](https://dev.azure.com/dotnet/WindowsCommunityToolkit/_build/latest?definitionId=61&branchName=main) | [![NuGet](https://img.shields.io/nuget/v/CommunityToolkit.WinUI.Lottie.svg)](https://www.nuget.org/packages/CommunityToolkit.WinUI.Lottie/) |
| CommunityToolkit.Uwp.Lottie | main | [![Build Status](https://dev.azure.com/dotnet/WindowsCommunityToolkit/_apis/build/status/Microsoft.Toolkit.Uwp.UI.Lottie?branchName=main)](https://dev.azure.com/dotnet/WindowsCommunityToolkit/_build/latest?definitionId=61&branchName=main) | [![NuGet](https://img.shields.io/nuget/v/CommunityToolkit.Uwp.Lottie.svg)](https://www.nuget.org/packages/CommunityToolkit.Uwp.Lottie/) |
| LottieGen | main | [![Build Status](https://dev.azure.com/dotnet/WindowsCommunityToolkit/_apis/build/status/Microsoft.Toolkit.Uwp.UI.Lottie?branchName=main)](https://dev.azure.com/dotnet/WindowsCommunityToolkit/_build/latest?definitionId=61&branchName=main) | [![NuGet](https://img.shields.io/nuget/v/LottieGen.svg)](https://www.nuget.org/packages/LottieGen/) |



## Feedback and Requests
Please use [GitHub Issues](https://github.com/windows-toolkit/Lottie-Windows/issues) for bug reports and feature requests.

## Principles
This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/)
to clarify expected behavior in our community.
For more information see the [.NET Foundation Code of Conduct](http://dotnetfoundation.org/code-of-conduct).

## .NET Foundation
This project is supported by the [.NET Foundation](http://dotnetfoundation.org).
