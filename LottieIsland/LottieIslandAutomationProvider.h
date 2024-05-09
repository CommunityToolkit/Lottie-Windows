// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#pragma once
#include "AutomationFragmentRoot.h"

namespace LottieIslandInternal
{

struct LottieIslandAutomationProvider : winrt::implements<LottieIslandAutomationProvider, AutomationHelpers::AutomationFragmentRoot, ::IInvokeProvider>
{
    // Automation callback handler.
    [[nodiscard]] std::unique_ptr<AutomationHelpers::AutomationCallbackRevoker> SetInvokeCallbackHandler(
        AutomationHelpers::IAutomationInvokeCallbackHandler* const handler);

    // IInvokeProvider implementation.
    HRESULT __stdcall Invoke() final override;
};

}