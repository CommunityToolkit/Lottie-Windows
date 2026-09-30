// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <unknwn.h>

#include <cstddef>
#include <span>

#include <winrt/Windows.UI.Composition.h>

namespace CommunityToolkit::WinUI::Lottie
{
    // LoadComposition is the direct-link C++ API.
    // DLL callers use ILottieCompositionLoader from LottieRuntimeCom.h.
    // Builds the composition described by a serialized Lottie composition.
    //
    // The buffer is the output of LottieGen -Language flatbuffer, or equivalently of
    // CompositionSerializer. It is treated as untrusted: it is verified before it is
    // read, and every index in it is range checked, so a malformed buffer produces an
    // exception rather than undefined behaviour.
    //
    // The returned visual is the root of a tree that is already animated and ready to
    // be attached to a target with SetElementChildVisual or as a child of another
    // visual. The animations are bound to the "Progress" property of the property set
    // returned by ProgressPropertySet, so playback is driven by animating that single
    // property.
    //
    // Visual is sufficient for parenting the root and driving its progress.
    //
    // Throws winrt::hresult_error with E_INVALIDARG if the buffer is not a well formed
    // composition, and E_NOTIMPL if it requires a feature this build does not have.
    winrt::Windows::UI::Composition::Visual LoadComposition(
        winrt::Windows::UI::Composition::Compositor const& compositor,
        std::span<std::byte const> buffer);

    // The property set that drives the animation. Animating its "Progress" scalar from
    // 0 to 1 plays the animation once.
    //
    // Returns the root Visual's property set.
    winrt::Windows::UI::Composition::CompositionPropertySet ProgressPropertySet(
        winrt::Windows::UI::Composition::Visual const& root);
}
