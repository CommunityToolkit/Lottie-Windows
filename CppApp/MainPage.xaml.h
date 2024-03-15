// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include "MainPage.g.h"

namespace winrt::CppApp::implementation
{
    struct MainPage : MainPageT<MainPage>
    {
        MainPage();
        static CppApp::MainPage Current() { return current; }
        static Windows::Foundation::Collections::IVector<CppApp::Scenario> Scenarios() { return scenariosInner; }

    private:
        static Windows::Foundation::Collections::IVector<Scenario> scenariosInner;
        static CppApp::MainPage current;
        winrt::Microsoft::UI::Composition::Compositor m_compositor{ nullptr };
        winrt::Microsoft::UI::Composition::ContainerVisual m_rootVisual{ nullptr };
        winrt::Microsoft::UI::Xaml::Controls::IAnimatedVisualSource m_animatedVisualSource{ nullptr };
    };
}

namespace winrt::CppApp::factory_implementation
{
    struct MainPage : MainPageT<MainPage, implementation::MainPage>
    {
    };
}
