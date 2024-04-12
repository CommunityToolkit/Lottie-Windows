#pragma once

#include <chrono>

#include <unknwn.h>

// Xaml has a GetCurrentTime, and somewhere in the windows sdk there's a macro for it.
// These conflict and cause issues.
#undef GetCurrentTime

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.UI.h>
#include <winrt/Microsoft.UI.Content.h>
#include <winrt/Microsoft.UI.Composition.h>
#include <winrt/Microsoft.UI.Xaml.Controls.h>
#include <winrt/LottieWinRT.h>
//#include <winrt/CommunityToolkit.WinAppSDK.Frameworkless.Lottie.h>

namespace winrt
{
    using namespace ::winrt::Microsoft::UI::Composition;
    using namespace ::winrt::Microsoft::UI::Content;
    using namespace ::winrt::LottieWinRT;
    /*using IAnimatedVisual = ::winrt::CommunityToolkit::WinAppSDK::Frameworkless::Lottie::IAnimatedVisual;
    using IAnimatedVisualSource = ::winrt::CommunityToolkit::WinAppSDK::Frameworkless::Lottie::IAnimatedVisualSource;*/
}

// Opt into time literals (i.e. 200ms, 1min, 15s)
using namespace std::chrono_literals;

using float2 = winrt::Windows::Foundation::Numerics::float2;
