// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// The classic COM entry point declared in LottieRuntimeCom.h. This is the only
// translation unit that needs to know both LoadComposition and classic COM, so it
// stays out of LottieRuntime.cpp.

#include <unknwn.h>

#include "LottieRuntimeCom.h"
#include "LottieRuntime.h"

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.Composition.h>

using namespace winrt::Windows::UI::Composition;


namespace
{
    // The caller owns the thread's DispatcherQueue. It must outlive every Visual
    // returned by this loader.
    struct LottieCompositionLoader : winrt::implements<LottieCompositionLoader, ILottieCompositionLoader>
    {
        STDMETHODIMP LoadComposition(UINT32 length, BYTE const* buffer, REFIID riid, void** result) noexcept override
        {
            if (result == nullptr)
            {
                return E_POINTER;
            }

            *result = nullptr;

            if (buffer == nullptr && length != 0)
            {
                return E_INVALIDARG;
            }

            try
            {
                if (!m_compositor)
                {
                    if (!winrt::Windows::System::DispatcherQueue::GetForCurrentThread())
                    {
                        return E_ILLEGAL_METHOD_CALL;
                    }

                    m_compositor = Compositor();
                }

                auto const span = std::span(reinterpret_cast<std::byte const*>(buffer), length);
                auto const visual = CommunityToolkit::WinUI::Lottie::LoadComposition(m_compositor, span);
                return visual.as<::IUnknown>()->QueryInterface(riid, result);
            }
            catch (...)
            {
                return winrt::to_hresult();
            }
        }

    private:
        Compositor m_compositor{ nullptr };
    };

    // A minimal class factory: this DLL only ever creates LottieCompositionLoader,
    // so there is no per-CLSID dispatch to do.
    struct ClassFactory : winrt::implements<ClassFactory, IClassFactory>
    {
        STDMETHODIMP CreateInstance(::IUnknown* outer, REFIID riid, void** result) noexcept override
        {
            if (result == nullptr)
            {
                return E_POINTER;
            }

            *result = nullptr;

            if (outer != nullptr)
            {
                return CLASS_E_NOAGGREGATION;
            }

            try
            {
                return winrt::make<LottieCompositionLoader>()->QueryInterface(riid, result);
            }
            catch (...)
            {
                return winrt::to_hresult();
            }
        }

        STDMETHODIMP LockServer(BOOL lock) noexcept override
        {
            if (lock)
            {
                ++winrt::get_module_lock();
            }
            else
            {
                --winrt::get_module_lock();
            }

            return S_OK;
        }
    };
}

extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID rclsid, REFIID riid, void** result)
{
    if (result == nullptr)
    {
        return E_POINTER;
    }

    *result = nullptr;

    if (rclsid != CLSID_LottieCompositionLoader)
    {
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    try
    {
        return winrt::make<ClassFactory>()->QueryInterface(riid, result);
    }
    catch (...)
    {
        return winrt::to_hresult();
    }
}

extern "C" HRESULT __stdcall DllCanUnloadNow()
{
    return winrt::get_module_lock() == 0 ? S_OK : S_FALSE;
}
