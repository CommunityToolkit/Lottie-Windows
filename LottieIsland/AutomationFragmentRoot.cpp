// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#include "pch.h"
#include "AutomationFragmentRoot.h"

namespace AutomationHelpers
{

std::unique_ptr<AutomationCallbackRevoker> AutomationFragmentRoot::SetFragmentRootCallbackHandler(
    IAutomationFragmentRootCallbackHandler* const handler)
{
    AddHandler(AutomationCallbackHandlerType::FragmentRoot, handler);
    return AutomationCallbackRevoker::Create(GetWeak(), handler);
}

HRESULT __stdcall AutomationFragmentRoot::ElementProviderFromPoint(
    _In_ double x,
    _In_ double y,
    _COM_Outptr_opt_result_maybenull_ ::IRawElementProviderFragment** fragment)
{
    try
    {
        std::unique_lock lock{ m_mutex };
        if (nullptr != fragment)
        {
            *fragment = nullptr;
            if (auto handler = GetHandler<IAutomationFragmentRootCallbackHandler>(AutomationCallbackHandlerType::FragmentRoot))
            {
                handler->GetFragmentFromPointForAutomation(x, y, GetIUnknown<AutomationFragmentRoot>()).copy_to(fragment);
            }
        }
    }
    catch (...) { return UIA_E_ELEMENTNOTAVAILABLE; }
    return S_OK;
}

HRESULT __stdcall AutomationFragmentRoot::GetFocus(
    _COM_Outptr_opt_result_maybenull_ ::IRawElementProviderFragment** fragmentInFocus)
{
    try
    {
        std::unique_lock lock{ m_mutex };
        if (nullptr != fragmentInFocus)
        {
            *fragmentInFocus = nullptr;
            if (auto handler = GetHandler<IAutomationFragmentRootCallbackHandler>(AutomationCallbackHandlerType::FragmentRoot))
            {
                handler->GetFragmentInFocusForAutomation(GetIUnknown<AutomationFragmentRoot>()).copy_to(fragmentInFocus);
            }
        }
    }
    catch (...) { return UIA_E_ELEMENTNOTAVAILABLE; }
    return S_OK;
}

}