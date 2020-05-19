# Lottie-Windows

Lottie-Windows is a library for rendering [Adobe After Effects](https://www.adobe.com/products/aftereffects.html) animations natively in your application. This project adds Windows to the [Lottie](http://airbnb.io/lottie/) family of tools also targeting [Android](https://github.com/airbnb/lottie-android), [iOS](https://github.com/airbnb/lottie-ios), and [Web](https://github.com/airbnb/lottie-web).

Lottie simplifies the design-to-code workflow for bringing engaging, interactive vector animations to your Windows applications, with significant improvements in terms of performance, quality, and engineering efficiency over traditional approaches such as gifs, manually coded animations, etc. Lottie-Windows uses the [Windows.UI.Composition APIs](https://docs.microsoft.com/windows/uwp/composition/visual-layer) to provide smooth 60fps animations and resolution-independent vector graphics.

![Lottie-Windows Gif](/images/LottieWindows_Intro.gif)

Lottie-Windows consists of 3 related products:
* **[Lottie-Windows](/Lottie-Windows)** library for parsing and translating [Bodymovin](https://aescripts.com/bodymovin/) JSON files
* **[LottieGen](/LottieGen)** command-line tool for generating C# or C++ code to be used instead of JSON
* **[Lottie Viewer](/LottieViewer)** application for previewing JSON and also generating code 

This repo also contains source code for **[samples](/LottieViewer)**.

## <a name="supported"></a> Supported SDKs
* May 2019 Update (18362) and later

## <a name="documentation"></a> Getting Started
* [Documentation and Tutorials](https://aka.ms/lottiedocs)
* [Lottie Samples](https://aka.ms/lottiesamples)
* [Lottie Viewer](https://aka.ms/lottieviewer)

## Build Status
| Target | Branch | Status | Recommended NuGet package |
| ------ | ------ | ------ | ------ |
| 6.1.0 release | master | [![Build Status](https://dev.azure.com/dotnet/WindowsCommunityToolkit/_apis/build/status/Microsoft.Toolkit.Uwp.UI.Lottie?branchName=master)](https://dev.azure.com/dotnet/WindowsCommunityToolkit/_build/latest?definitionId=61&branchName=master) | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Toolkit.Uwp.UI.Lottie.svg)](https://www.nuget.org/packages/Microsoft.Toolkit.Uwp.UI.Lottie/) |

## Feedback and Requests
Please use [GitHub Issues](https://github.com/windows-toolkit/Lottie-Windows/issues) for bug reports and feature requests.

## Principles
This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/)
to clarify expected behavior in our community.
For more information see the [.NET Foundation Code of Conduct](http://dotnetfoundation.org/code-of-conduct).

## .NET Foundation
This project is supported by the [.NET Foundation](http://dotnetfoundation.org).
