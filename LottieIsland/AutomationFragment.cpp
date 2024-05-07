// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#include "pch.h"
#include "AutomationFragment.h"
#include <wil/resource.h>

using unique_safearray = wil::unique_any<SAFEARRAY*, decltype(&::SafeArrayDestroy), ::SafeArrayDestroy>;

namespace AutomationHelpers
{

std::unique_ptr<AutomationCallbackRevoker> AutomationFragment::SetFragmentCallbackHandler(
        IAutomationFragmentCallbackHandler* const handler)
{
    AddHandler(AutomationCallbackHandlerType::Fragment, handler);
    return AutomationCallbackRevoker::Create(GetWeak(), handler);
}

void AutomationFragment::AddChildToEnd(
    winrt::com_ptr<AutomationFragment> const& child)
{
    std::unique_lock lock{ m_mutex };

    if (nullptr == child)
    {
        // Nothing to do.
        return;
    }

    // The child should not already have a parent.
    winrt::check_bool(nullptr == child->Parent());

    // Set us up as the parent for the new child.
    child->Parent(GetWeak());

    // Set up the sibling relationships.
    if (!m_children.empty())
    {
        auto& previousSiblingForNewChild = m_children.back();
        previousSiblingForNewChild->NextSibling(child);
        child->PreviousSibling(previousSiblingForNewChild);
    }

    // Finally add the child.
    m_children.push_back(child);

    // Raise the UIA structure changed event.
    winrt::check_hresult(::UiaRaiseStructureChangedEvent(
        GetStrong<AutomationFragment>().as<IRawElementProviderSimple>().get(),
        StructureChangeType_ChildAdded,
        child->RuntimeId(), child->RuntimeIdSize()));
}

void AutomationFragment::RemoveChild(
    winrt::com_ptr<AutomationFragment> const& child)
{
    std::unique_lock lock{ m_mutex };

    if (nullptr == child)
    {
        // Nothing to do.
        return;
    }

    auto iterator = std::find_if(
        m_children.begin(), m_children.end(), [&child](auto const& childEntry)
        {
            // See if we find a matching child entry in our children.
            return (childEntry.as<::IUnknown>().get() == child.as<::IUnknown>().get());
        });

    // We cannot remove a child that isn't ours.
    winrt::check_bool(m_children.end() != iterator);

    // Remove us from the parent relationship with the child.
    child->Parent(nullptr);

    // Reset the sibling relationships.
    auto previousSibling = child->PreviousSibling();
    auto nextSibling = child->NextSibling();
    if (nullptr != previousSibling)
    {
        previousSibling->NextSibling(nextSibling);
    }
    if (nullptr != nextSibling)
    {
        nextSibling->PreviousSibling(previousSibling);
    }
    child->PreviousSibling(nullptr);
    child->NextSibling(nullptr);

    // Finally, remove the child.
    m_children.erase(iterator);

    // Raise the UIA structure changed event.
    winrt::check_hresult(::UiaRaiseStructureChangedEvent(
        GetStrong<AutomationFragment>().as<IRawElementProviderSimple>().get(),
        StructureChangeType_ChildRemoved,
        child->RuntimeId(), child->RuntimeIdSize()));
}

void AutomationFragment::RemoveAllChildren()
{
    std::unique_lock lock{ m_mutex };

    for (auto& child : m_children)
    {
        // Disconnect the relationships from all our children.
        child->Parent(nullptr);
        child->PreviousSibling(nullptr);
        child->NextSibling(nullptr);
    }

    // Remove all the children.
    m_children.clear();

    // Raise the UIA structure changed event.
    winrt::check_hresult(::UiaRaiseStructureChangedEvent(
        GetStrong<AutomationFragment>().as<IRawElementProviderSimple>().get(),
        StructureChangeType_ChildrenBulkRemoved,
        nullptr, 0));
}

HRESULT __stdcall AutomationFragment::Navigate(
    _In_ NavigateDirection direction,
    _COM_Outptr_opt_result_maybenull_ IRawElementProviderFragment** fragment)
{
    try
    {
        std::unique_lock lock{ m_mutex };
        if (nullptr != fragment)
        {
            *fragment = nullptr;
            switch (direction)
            {
                case NavigateDirection_Parent:
                {
                    if (auto strongParent = LockWeak<AutomationFragment>(m_parent))
                    {
                        strongParent.as<IRawElementProviderFragment>().copy_to(fragment);
                    }
                    break;
                }
                case NavigateDirection_NextSibling:
                {
                    if (auto strongSibling = LockWeak<AutomationFragment>(m_nextSibling))
                    {
                        strongSibling.as<IRawElementProviderFragment>().copy_to(fragment);
                    }
                    break;
                }
                case NavigateDirection_PreviousSibling:
                {
                    if (auto strongSibling = LockWeak<AutomationFragment>(m_previousSibling))
                    {
                        strongSibling.as<IRawElementProviderFragment>().copy_to(fragment);
                    }
                    break;
                }
                case NavigateDirection_FirstChild:
                {
                    if (!m_children.empty())
                    {
                        m_children.front().as<IRawElementProviderFragment>().copy_to(fragment);
                    }
                    break;
                }
                case NavigateDirection_LastChild:
                {
                    if (!m_children.empty())
                    {
                        m_children.back().as<IRawElementProviderFragment>().copy_to(fragment);
                    }
                    break;
                }
            }
        }
    }
    catch (...) { return UIA_E_ELEMENTNOTAVAILABLE; }
    return S_OK;
}

HRESULT __stdcall AutomationFragment::GetRuntimeId(
    _Outptr_opt_result_maybenull_ SAFEARRAY** runtimeId)
{
    try
    {
        std::unique_lock lock{ m_mutex };
        if (nullptr != runtimeId)
        {
            *runtimeId = nullptr;

            unsigned long arraySizeAsUnsignedLong = static_cast<unsigned long>(m_runtimeId.size());

            unique_safearray runtimeIdArray{ ::SafeArrayCreateVector(VT_I4, 0, arraySizeAsUnsignedLong) };
            SAFEARRAY* rawPointerToSafeArray = runtimeIdArray.get();
            winrt::check_pointer(rawPointerToSafeArray);

            for (long i = 0; i < static_cast<long>(arraySizeAsUnsignedLong); ++i)
            {
                winrt::check_hresult(::SafeArrayPutElement(rawPointerToSafeArray, &i, &(m_runtimeId[i])));
            }

            *runtimeId = runtimeIdArray.release();
        }
    }
    catch (...) { return UIA_E_ELEMENTNOTAVAILABLE; }
    return S_OK;
}

HRESULT __stdcall AutomationFragment::get_BoundingRectangle(
    _Out_ UiaRect* boundingRectangle)
{
    try
    {
        std::unique_lock lock{ m_mutex };
        if (nullptr != boundingRectangle)
        {
            *boundingRectangle = { 0, 0, 0, 0 };
            if (auto handler = GetHandler<IAutomationFragmentCallbackHandler>(AutomationCallbackHandlerType::Fragment))
            {
                auto screenRectangle =
                    handler->GetBoundingRectangleInScreenSpaceForAutomation(GetIUnknown<AutomationFragment>());

                boundingRectangle->left = screenRectangle.X;
                boundingRectangle->top = screenRectangle.Y;
                boundingRectangle->width = screenRectangle.Width;
                boundingRectangle->height = screenRectangle.Height;
            }
        }
    }
    catch (...) { return UIA_E_ELEMENTNOTAVAILABLE; }
    return S_OK;
}

HRESULT __stdcall AutomationFragment::GetEmbeddedFragmentRoots(
    _Outptr_opt_result_maybenull_ SAFEARRAY** embeddedFragmentRoots)
{
    try
    {
        std::unique_lock lock{ m_mutex };
        if (nullptr != embeddedFragmentRoots)
        {
            *embeddedFragmentRoots = nullptr;

            if (!m_embeddedFragments.empty())
            {
                unsigned long vectorSizeAsUnsignedLong = static_cast<unsigned long>(m_embeddedFragments.size());

                unique_safearray embeddedFragmentRootsArray{ ::SafeArrayCreateVector(VT_UNKNOWN, 0, vectorSizeAsUnsignedLong) };
                SAFEARRAY* rawPointerToSafeArray = embeddedFragmentRootsArray.get();
                winrt::check_pointer(rawPointerToSafeArray);

                for (long i = 0; i < static_cast<long>(vectorSizeAsUnsignedLong); ++i)
                {
                    winrt::check_hresult(::SafeArrayPutElement(rawPointerToSafeArray, &i, m_embeddedFragments.at(i).as<::IUnknown>().get()));
                }

                *embeddedFragmentRoots = embeddedFragmentRootsArray.release();
            }
        }
    }
    catch (...) { return UIA_E_ELEMENTNOTAVAILABLE; }
    return S_OK;
}

HRESULT __stdcall AutomationFragment::SetFocus()
{
    try
    {
        std::unique_lock lock{ m_mutex };
        if (auto handler = GetHandler<IAutomationFragmentCallbackHandler>(AutomationCallbackHandlerType::Fragment))
        {
            handler->HandleSetFocusForAutomation(GetIUnknown<AutomationFragment>());
        }
    }
    catch (...) { return UIA_E_ELEMENTNOTAVAILABLE; }
    return S_OK;
}

HRESULT __stdcall AutomationFragment::get_FragmentRoot(
    _COM_Outptr_opt_result_maybenull_ IRawElementProviderFragmentRoot** fragmentRoot)
{
    try
    {
        std::unique_lock lock{ m_mutex };
        if (nullptr != fragmentRoot)
        {
            *fragmentRoot = nullptr;

            // Walk up our fragment tree until we find our fragment root.
            auto fragmentRootCandidate = GetStrong<AutomationFragment>();
            bool currentCandidateIsThisObject = true;
            while (nullptr != fragmentRootCandidate && nullptr == fragmentRootCandidate.try_as<IRawElementProviderFragmentRoot>())
            {
                // Haven't found the fragment root yet, keep walking up our tree.
                fragmentRootCandidate = currentCandidateIsThisObject ? LockWeak<AutomationFragment>(m_parent) : fragmentRootCandidate->Parent();
                currentCandidateIsThisObject = false;
            }

            if (nullptr != fragmentRootCandidate)
            {
                // Found the fragment root, return it.
                fragmentRootCandidate.as<IRawElementProviderFragmentRoot>().copy_to(fragmentRoot);
            }
        }
    }
    catch (...) { return UIA_E_ELEMENTNOTAVAILABLE; }
    return S_OK;
}

}