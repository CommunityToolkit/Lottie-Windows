// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <unknwn.h>

#include <cstddef>
#include <cstdint>
#include <memory>
#include <span>
#include <vector>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Numerics.h>

#if defined(LOTTIE_RUNTIME_WINUI2) == defined(LOTTIE_RUNTIME_WINUI3)
#error Define exactly one of LOTTIE_RUNTIME_WINUI2 or LOTTIE_RUNTIME_WINUI3.
#endif

#if defined(LOTTIE_RUNTIME_WINUI3)
#include <winrt/Microsoft.UI.Composition.h>
namespace CommunityToolkit::WinUI3::Lottie
{
    using namespace winrt::Microsoft::UI::Composition;
#else
#include <winrt/Windows.UI.Composition.h>
namespace CommunityToolkit::WinUI2::Lottie
{
    using namespace winrt::Windows::UI::Composition;
#endif

    // Define LOTTIE_RUNTIME_WINUI2 for Windows.UI.Composition or
    // LOTTIE_RUNTIME_WINUI3 for Microsoft.UI.Composition.
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
    Visual LoadComposition(
        Compositor const& compositor,
        std::span<std::byte const> buffer);

    struct CompositionMarker
    {
        winrt::hstring Name;
        float Progress = 0;
        float DurationProgress = 0;
    };

    struct CompositionImageSurface
    {
        uint32_t Index = 0;
        ICompositionSurface Surface{ nullptr };
    };

    // The keyframe animations a loaded tree started, recorded so that a host can stop
    // and restart them. Shared by copies of the LoadedComposition that owns it.
    class CompositionAnimations;

    struct LoadedComposition
    {
        Visual Root{ nullptr };
        winrt::Windows::Foundation::TimeSpan Duration{};
        winrt::Windows::Foundation::Numerics::float2 Size{};
        float FramesPerSecond = 0;
        std::vector<CompositionMarker> Markers;
        CompositionPropertySet ThemeProperties{ nullptr };
        std::vector<CompositionImageSurface> ImageSurfaces;
        std::shared_ptr<CompositionAnimations> Animations;
    };

    // Loads the same animated tree as LoadComposition, together with its serialized
    // metadata. Duration uses 100ns ticks. Marker progress and duration are fractions
    // of the animation duration; order and duplicate names are preserved. Markers
    // outside the animation's playback range are not clamped or discarded.
    //
    // ThemeProperties is the actual standalone property set referenced by the
    // loaded graph's expressions, or nullptr when no theme set is declared.
    // Inserting values into it updates the existing graph, not a separate copy.
    //
    // ImageSurfaces contains the actual realized LoadedImageSurface objects and
    // their serialized surface indices, in index order. Visual surfaces and
    // unrealized images are excluded. A host can observe image completion and
    // reuse these objects when loading another tree from the same buffer.
    //
    // Requires a metadata table. Missing metadata, invalid numeric metadata, or
    // invalid metadata indices throw winrt::hresult_invalid_argument. The returned
    // metadata owns its values and does not require the buffer to remain alive.
    LoadedComposition LoadCompositionWithMetadata(
        Compositor const& compositor,
        std::span<std::byte const> buffer);

    // Reuses images from a previous load of the same buffer on the same XAML host
    // compositor. Null surfaces, duplicate/out-of-range indices, nonimage indices,
    // and objects that are not this projection's LoadedImageSurface are rejected.
    // Other images are loaded normally. Completion events and playback policy
    // remain the host's responsibility; this overload does not wait for images.
    LoadedComposition LoadCompositionWithMetadata(
        Compositor const& compositor,
        std::span<std::byte const> buffer,
        std::span<CompositionImageSurface const> imageSurfaces);

    // The property set that drives the animation. Animating its "Progress" scalar from
    // 0 to 1 plays the animation once.
    //
    // Returns the root Visual's property set.
    CompositionPropertySet ProgressPropertySet(Visual const& root);

    // Stop and restart support for IAnimatedVisual2::DestroyAnimations and
    // CreateAnimations, matching LottieGen's generated C++ and C# sources.
    //
    // StopAnimations stops the keyframe animations the load started, leaving every
    // animated property at its current value and keeping expression bindings (Progress,
    // theme and other bindings) in place. StartAnimations starts them again with their
    // original controllers, so the tree once more follows its Progress property. A loaded
    // composition starts with its animations running. Both calls are idempotent.
    //
    // CanRestartAnimations returns false when the tree cannot be restarted faithfully:
    // another object references an implicit animation controller, an implicit controller
    // owns a property set, two animations share one implicit controller, or an implicit
    // controller's own animation has a controller. For such trees, and for a default
    // LoadedComposition, StopAnimations and StartAnimations do nothing.
    bool CanRestartAnimations(LoadedComposition const& composition) noexcept;
    void StopAnimations(LoadedComposition const& composition);
    void StartAnimations(LoadedComposition const& composition);
}
