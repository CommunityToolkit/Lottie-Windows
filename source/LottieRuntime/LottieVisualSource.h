// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "LottieRuntime.h"

#undef GetCurrentTime
#include <winrt/Microsoft.UI.Xaml.Controls.h>

#if defined(LOTTIE_RUNTIME_WINUI3)
namespace CommunityToolkit::WinUI3::Lottie
#else
namespace CommunityToolkit::WinUI2::Lottie
#endif
{
    // One UAP-specific serialization of an animation, embedded as an RCDATA resource.
    struct EmbeddedCompositionVariant
    {
        uint16_t RequiredUapVersion = 0;
        wchar_t const* ResourceName = nullptr;
    };

    // Returns the bytes of the variant with the highest UniversalApiContract version
    // present, or of the lowest variant when none is present so that the source still
    // has metadata and TryCreateAnimatedVisual returns null. The resource is read from
    // the module containing moduleAddress and lives as long as that module.
    //
    // Throws winrt::hresult_error if there are no variants or the resource is missing or
    // empty, and winrt::hresult_invalid_argument if the buffer is not a verified
    // composition requiring exactly the variant's UAP version.
    std::span<std::byte const> LoadEmbeddedComposition(
        void const* moduleAddress,
        std::span<EmbeddedCompositionVariant const> variants);

    // How serialized markers become IAnimatedVisualSource2::Markers. Each policy matches a
    // LottieGen version's generated code. Frames are recovered from the marker progress
    // and the first nonzero frame is nudged by 0.05 frames, as LottieGen does.
    enum class MarkerPolicy : uint8_t
    {
        // A marker whose frame lies outside the animation is dropped; the first marker
        // of a name wins.
        FirstWins,

        // Names are sanitized as LottieGen does. A marker is dropped only when it lies
        // entirely outside the animation. Markers are ordered by frame and every
        // duplicate name gets a "_<n>" suffix.
        Unique,
    };

    struct AnimatedVisualSourceOptions
    {
        MarkerPolicy Markers = MarkerPolicy::FirstWins;

        // Whether a marker on the final frame is kept.
        bool IncludeFinalFrameMarkers = true;

        // When true, TryCreateAnimatedVisual returns null and reports the error message
        // through diagnostics if loading fails. When false, the error propagates.
        bool ReportLoadErrors = false;

        // For animations with images: return null from TryCreateAnimatedVisual until
        // every image has loaded, then raise AnimatedVisualInvalidated.
        bool AsynchronousImageLoading = false;
    };

    // Creates an IAnimatedVisualSource2 that loads the serialized composition with the
    // runtime. The buffer is not copied and must outlive the source; an embedded
    // resource does.
    //
    // Created visuals implement IAnimatedVisual2, so AnimatedVisualPlayer's
    // AnimationOptimization="Resources" stops their animations while idle.
    // SetColorProperty and SetScalarProperty (below) update the theme properties of every
    // live visual and of visuals created later; names that are not bound properties of that
    // type are ignored. Animations with images also implement
    // IDynamicAnimatedVisualSource and INotifyPropertyChanged, raising
    // "IsImageLoadingCompleted" once every image has loaded.
    //
    // Throws winrt::hresult_invalid_argument if the buffer is not a verified composition
    // with metadata, a positive duration and a positive frame rate.
    winrt::Microsoft::UI::Xaml::Controls::IAnimatedVisualSource2 CreateAnimatedVisualSource(
        std::span<std::byte const> buffer,
        AnimatedVisualSourceOptions const& options = {});

    // Sets a bound scalar theme property, as LottieGen's generated SetScalarProperty does.
    // IAnimatedVisualSource2 only declares SetColorProperty. Throws
    // winrt::hresult_invalid_argument if source was not created by CreateAnimatedVisualSource.
    void SetScalarProperty(
        winrt::Windows::Foundation::IInspectable const& source,
        winrt::hstring const& name,
        double value);
}
