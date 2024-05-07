// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#include "pch.h"
#include "LottieIslandAutomationProvider.h"

namespace LottieIslandInternal
{


std::unique_ptr<AutomationHelpers::AutomationCallbackRevoker> LottieIslandAutomationProvider::SetInvokeCallbackHandler(
    AutomationHelpers::IAutomationInvokeCallbackHandler* const handler)
{
    AddHandler(AutomationHelpers::AutomationCallbackHandlerType::Invoke, handler);
    return AutomationHelpers::AutomationCallbackRevoker::Create(GetWeak(), handler);
}


HRESULT __stdcall LottieIslandAutomationProvider::Invoke()
{
    try
    {
        std::unique_lock lock{ m_mutex };
        if (auto handler = GetHandler<AutomationHelpers::IAutomationInvokeCallbackHandler>(
            AutomationHelpers::AutomationCallbackHandlerType::Invoke))
        {
            handler->HandleInvokeForAutomation(GetIUnknown<LottieIslandAutomationProvider>());
        }
    }
    catch (...) { return UIA_E_ELEMENTNOTAVAILABLE; }
    return S_OK;
}

}