// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"
#include "MainPage.xaml.h"
#if __has_include("MainPage.g.cpp")
#include "MainPage.g.cpp"
#endif

#include <winrt/Microsoft.UI.Xaml.Hosting.h>
#include <winrt/LottieWinRT.h>

namespace winrt
{
    using namespace Microsoft::UI::Composition;
    using namespace Microsoft::UI::Xaml;
    using namespace Microsoft::UI::Xaml::Controls;
    using namespace Microsoft::UI::Xaml::Hosting;
    using namespace Microsoft::UI::Xaml::Media;
    using namespace Microsoft::UI::Xaml::Media::Animation;
    using namespace Microsoft::UI::Xaml::Navigation;
    using namespace LottieWinRT;
}

namespace winrt::CppApp::implementation
{
    CppApp::MainPage MainPage::current{ nullptr };

    MainPage::MainPage()
    {
        InitializeComponent();
        MainPage::current = *this;

        m_compositor = ElementCompositionPreview::GetElementVisual(MyGrid()).Compositor();
        m_rootVisual = m_compositor.CreateContainerVisual();
        ElementCompositionPreview::SetElementChildVisual(MyGrid(), m_rootVisual);

        winrt::LottieWinRT::LottieVisualSourceWinRT lottieAnimatedVisual;
        lottieAnimatedVisual.SetUpLottie(m_compositor, m_rootVisual, L"ms-appx:///LottieLogo1.json");
    }
}
