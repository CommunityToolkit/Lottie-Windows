// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// An IAnimatedVisualSource2 over the runtime, replacing the classes LottieGen generates
// for each animation.

#include <unknwn.h>
#include <windows.h>
#include <hstring.h>

#if defined(LOTTIE_RUNTIME_WINUI2) == defined(LOTTIE_RUNTIME_WINUI3)
#error Define exactly one of LOTTIE_RUNTIME_WINUI2 or LOTTIE_RUNTIME_WINUI3.
#endif

#include "LottieVisualSource.h"

#include <algorithm>
#include <cmath>
#include <map>
#include <optional>
#include <string>
#include <utility>
#include <vector>

#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Foundation.Metadata.h>
#include <winrt/Windows.UI.h>

#undef GetCurrentTime
#if defined(LOTTIE_RUNTIME_WINUI3)
#include <winrt/Microsoft.UI.Xaml.Data.h>
#include <winrt/Microsoft.UI.Xaml.Media.h>
#else
#include <winrt/Windows.UI.Xaml.Data.h>
#include <winrt/Windows.UI.Xaml.Media.h>
#endif

#include "Generated/lottie_comp_generated.h"

#if defined(LOTTIE_RUNTIME_WINUI3)
namespace CommunityToolkit::WinUI3::Lottie
#else
namespace CommunityToolkit::WinUI2::Lottie
#endif
{
    namespace
    {
        namespace Fb = CommunityToolkit::WinUI::Lottie::CompDataFlatbuffer::Schema;
        using namespace winrt::Microsoft::UI::Xaml::Controls;
        using namespace winrt::Windows::Foundation;
        using namespace winrt::Windows::Foundation::Collections;
        using namespace winrt::Windows::Foundation::Numerics;
        using winrt::hstring;
        using winrt::Windows::Foundation::IInspectable;
#if defined(LOTTIE_RUNTIME_WINUI3)
        namespace XamlData = winrt::Microsoft::UI::Xaml::Data;
        namespace XamlMedia = winrt::Microsoft::UI::Xaml::Media;
#else
        namespace XamlData = winrt::Windows::UI::Xaml::Data;
        namespace XamlMedia = winrt::Windows::UI::Xaml::Media;
#endif

        // IAnimatedVisualSource2 has no scalar setter, so sources expose it privately.
#if defined(LOTTIE_RUNTIME_WINUI3)
        struct __declspec(uuid("ae67b9f9-0afe-467c-9d06-f6a2dbaa46da")) __declspec(novtable) IScalarProperties : ::IUnknown
#else
        struct __declspec(uuid("c36aecea-4fd6-42ae-ac25-7cb9c564476e")) __declspec(novtable) IScalarProperties : ::IUnknown
#endif
        {
            virtual HRESULT __stdcall SetScalar(HSTRING name, double value) noexcept = 0;
        };

        bool IsUapPresent(uint16_t version)
        {
            return winrt::Windows::Foundation::Metadata::ApiInformation::IsApiContractPresent(
                L"Windows.Foundation.UniversalApiContract", version);
        }

        Fb::LottieComposition const* Verify(std::span<std::byte const> buffer)
        {
            flatbuffers::Verifier verifier(reinterpret_cast<uint8_t const*>(buffer.data()), buffer.size());
            if (buffer.empty() || !Fb::VerifyLottieCompositionBuffer(verifier))
            {
                throw winrt::hresult_invalid_argument(L"Invalid serialized Lottie composition.");
            }
            return Fb::GetLottieComposition(buffer.data());
        }

        bool HasImages(Fb::LottieComposition const& composition)
        {
            if (auto surfaces = composition.surfaces())
            {
                for (auto surface : *surfaces)
                {
                    if (surface->kind() == Fb::SurfaceKind::LoadedImageFromStream ||
                        surface->kind() == Fb::SurfaceKind::LoadedImageFromUri)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        struct AnimatedVisual : winrt::implements<AnimatedVisual, IAnimatedVisual2, IAnimatedVisual, IClosable>
        {
            explicit AnimatedVisual(LoadedComposition composition) : m_composition(std::move(composition))
            {
            }

            Visual RootVisual() const { return m_composition.Root; }
            TimeSpan Duration() const { return m_composition.Duration; }
            float2 Size() const { return m_composition.Size; }
            CompositionPropertySet ThemeProperties() const { return m_composition.ThemeProperties; }

            void CreateAnimations() { StartAnimations(m_composition); }
            void DestroyAnimations() { StopAnimations(m_composition); }

            void Close()
            {
                if (m_composition.Root)
                {
                    m_composition.Root.Close();
                }
                m_composition = {};
            }

        private:
            LoadedComposition m_composition;
        };

        template<typename TSource, bool TImages>
        struct SourceInterfaces : winrt::implements<TSource, IAnimatedVisualSource, IAnimatedVisualSource2,
            IScalarProperties>
        {
        };

        template<typename TSource>
        struct SourceInterfaces<TSource, true> : winrt::implements<TSource,
            IDynamicAnimatedVisualSource, IAnimatedVisualSource, IAnimatedVisualSource2,
            XamlData::INotifyPropertyChanged, IScalarProperties>
        {
        };

        template<bool TImages>
        struct AnimatedVisualSource : SourceInterfaces<AnimatedVisualSource<TImages>, TImages>
        {
            AnimatedVisualSource(
                std::span<std::byte const> buffer,
                Fb::LottieComposition const* composition,
                AnimatedVisualSourceOptions const& options) :
                m_buffer(buffer), m_composition(composition), m_options(options)
            {
                auto metadata = m_composition->metadata();
                if (!metadata)
                {
                    throw winrt::hresult_invalid_argument(L"Lottie composition has no metadata.");
                }
                auto duration = metadata->duration_ticks();
                auto framerate = metadata->frames_per_second();
                if (duration <= 0 || !std::isfinite(framerate) || framerate <= 0)
                {
                    throw winrt::hresult_invalid_argument(L"Lottie composition has no duration.");
                }
                m_frameCount = std::round(duration * static_cast<double>(framerate) / 10000000.0);
                if (m_frameCount <= 0)
                {
                    throw winrt::hresult_invalid_argument(L"Lottie composition has no frames.");
                }
                if (auto bindings = metadata->property_bindings())
                {
                    for (auto binding : *bindings)
                    {
                        (void)StringAt(binding->name());
                    }
                }
                m_markers = m_options.Markers == MarkerPolicy::Unique ? UniqueMarkers() : FirstWinsMarkers();
            }

            ~AnimatedVisualSource()
            {
                // The handlers hold weak references, so a failed unregistration (for example
                // a release off the UI thread) is harmless and must not escape the destructor.
                for (auto const& [surface, token] : m_imageSubscriptions)
                {
                    try
                    {
                        surface.LoadCompleted(token);
                    }
                    catch (...)
                    {
                    }
                }
            }

            IAnimatedVisual TryCreateAnimatedVisual(Compositor const& compositor, IInspectable& diagnostics)
            {
                diagnostics = nullptr;
                if (!IsUapPresent(m_composition->required_uap_version()))
                {
                    return nullptr;
                }
                if (!m_options.ReportLoadErrors)
                {
                    return CreateAnimatedVisual(compositor);
                }
                try
                {
                    return CreateAnimatedVisual(compositor);
                }
                catch (winrt::hresult_error const& error)
                {
                    diagnostics = winrt::box_value(error.message());
                    return nullptr;
                }
            }

            IMapView<hstring, double> Markers() const { return m_markers; }

            void SetColorProperty(hstring const& name, winrt::Windows::UI::Color value)
            {
                if (HasBinding(name, Fb::PropertyValueType::Color))
                {
                    float4 color{ static_cast<float>(value.R), static_cast<float>(value.G),
                        static_cast<float>(value.B), static_cast<float>(value.A) };
                    m_colors.insert_or_assign(name, color);
                    UpdateThemes([&](auto const& theme) { theme.InsertVector4(name, color); });
                }
            }

            void SetScalarProperty(hstring const& name, double value)
            {
                if (HasBinding(name, Fb::PropertyValueType::Scalar))
                {
                    auto scalar = static_cast<float>(value);
                    m_scalars.insert_or_assign(name, scalar);
                    UpdateThemes([&](auto const& theme) { theme.InsertScalar(name, scalar); });
                }
            }

            HRESULT __stdcall SetScalar(HSTRING name, double value) noexcept override
            {
                try
                {
                    SetScalarProperty(reinterpret_cast<hstring const&>(name), value);
                    return S_OK;
                }
                catch (...)
                {
                    return winrt::to_hresult();
                }
            }

            winrt::event_token AnimatedVisualInvalidated(
                TypedEventHandler<IDynamicAnimatedVisualSource, IInspectable> const& handler)
            {
                return m_invalidated.add(handler);
            }

            void AnimatedVisualInvalidated(winrt::event_token const& token) noexcept
            {
                m_invalidated.remove(token);
            }

            winrt::event_token PropertyChanged(XamlData::PropertyChangedEventHandler const& handler)
            {
                return m_propertyChanged.add(handler);
            }

            void PropertyChanged(winrt::event_token const& token) noexcept
            {
                m_propertyChanged.remove(token);
            }

        private:
            IAnimatedVisual CreateAnimatedVisual(Compositor const& compositor)
            {
                LoadedComposition composition;
                if (m_pendingComposition)
                {
                    if (m_imageCompositor != compositor)
                    {
                        throw winrt::hresult_invalid_argument(L"Images were loaded with another compositor.");
                    }
                    if (!m_imageLoadingCompleted)
                    {
                        return nullptr;
                    }
                    composition = std::move(*m_pendingComposition);
                    m_pendingComposition.reset();
                }
                else
                {
                    if (!m_imageSurfaces.empty() && m_imageCompositor != compositor)
                    {
                        throw winrt::hresult_invalid_argument(L"Images were loaded with another compositor.");
                    }
                    composition = LoadCompositionWithMetadata(compositor, m_buffer, m_imageSurfaces);
                    if constexpr (TImages)
                    {
                        if (!m_imageLoadingStarted)
                        {
                            ObserveImages(compositor, composition.ImageSurfaces);
                        }
                        if (m_options.AsynchronousImageLoading && !m_imageLoadingCompleted)
                        {
                            m_pendingComposition.emplace(std::move(composition));
                            return nullptr;
                        }
                    }
                }

                auto theme = composition.ThemeProperties;
                auto visual = winrt::make_self<AnimatedVisual>(std::move(composition));
                if (theme)
                {
                    for (auto const& [name, color] : m_colors)
                    {
                        theme.InsertVector4(name, color);
                    }
                    for (auto const& [name, scalar] : m_scalars)
                    {
                        theme.InsertScalar(name, scalar);
                    }
                    std::erase_if(m_visuals, [](auto const& weak) { return !weak.get(); });
                    m_visuals.emplace_back(visual->get_weak());
                }
                return visual.as<IAnimatedVisual>();
            }

            void ObserveImages(Compositor const& compositor, std::vector<CompositionImageSurface> const& images)
            {
                m_imageLoadingStarted = true;
                m_imageCompositor = compositor;
                m_imageSurfaces = images;
                m_imagesRemaining = m_imageSurfaces.size();
                m_imageLoadingCompleted = m_imagesRemaining == 0;
                auto weak = this->get_weak();
                for (auto const& image : m_imageSurfaces)
                {
                    auto surface = image.Surface.as<XamlMedia::LoadedImageSurface>();
                    auto token = surface.LoadCompleted([weak](auto const&, auto const&)
                    {
                        if (auto source = weak.get())
                        {
                            source->ImageLoadCompleted();
                        }
                    });
                    m_imageSubscriptions.emplace_back(surface, token);
                }
            }

            void ImageLoadCompleted()
            {
                if (m_imagesRemaining && --m_imagesRemaining == 0)
                {
                    m_imageLoadingCompleted = true;
                    m_propertyChanged(*this, XamlData::PropertyChangedEventArgs(L"IsImageLoadingCompleted"));
                    if (m_options.AsynchronousImageLoading)
                    {
                        m_invalidated(*this, nullptr);
                    }
                }
            }

            hstring StringAt(uint32_t index) const
            {
                auto strings = m_composition->strings();
                if (!strings || index >= strings->size())
                {
                    throw winrt::hresult_invalid_argument(L"Invalid Lottie composition string index.");
                }
                return winrt::to_hstring(strings->Get(index)->string_view());
            }

            bool HasBinding(hstring const& name, Fb::PropertyValueType type) const
            {
                if (auto bindings = m_composition->metadata()->property_bindings())
                {
                    for (auto binding : *bindings)
                    {
                        if (binding->exposed_type() == type && StringAt(binding->name()) == name)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            template<typename TUpdate>
            void UpdateThemes(TUpdate const& update)
            {
                std::erase_if(m_visuals, [&](auto const& weak)
                {
                    if (auto visual = weak.get())
                    {
                        if (auto theme = visual->ThemeProperties())
                        {
                            update(theme);
                            return false;
                        }
                    }
                    return true;
                });
            }

            double MarkerProgress(double frame) const
            {
                return frame == 0 ? 0 : std::clamp((frame + 0.05) / m_frameCount, 0.0, 1.0);
            }

            IMapView<hstring, double> FirstWinsMarkers() const
            {
                auto markers = winrt::single_threaded_map<hstring, double>();
                if (auto entries = m_composition->metadata()->markers())
                {
                    for (auto marker : *entries)
                    {
                        if (!std::isfinite(marker->progress()) || !std::isfinite(marker->duration_progress()))
                        {
                            throw winrt::hresult_invalid_argument(L"Invalid Lottie composition marker.");
                        }
                        auto name = StringAt(marker->name());
                        auto frame = std::round(marker->progress() * m_frameCount);
                        auto last = m_options.IncludeFinalFrameMarkers ? m_frameCount : m_frameCount - 1;
                        if (frame >= 0 && frame <= last && !markers.HasKey(name))
                        {
                            markers.Insert(name, MarkerProgress(frame));
                        }
                    }
                }
                return markers.GetView();
            }

            IMapView<hstring, double> UniqueMarkers() const
            {
                struct Marker
                {
                    std::wstring Name;
                    double Frame;
                };
                std::vector<Marker> markers;
                if (auto entries = m_composition->metadata()->markers())
                {
                    for (auto entry : *entries)
                    {
                        if (!std::isfinite(entry->progress()) || !std::isfinite(entry->duration_progress()))
                        {
                            throw winrt::hresult_invalid_argument(L"Invalid Lottie composition marker.");
                        }
                        auto frame = std::round(entry->progress() * m_frameCount);
                        auto end = frame + std::round(entry->duration_progress() * m_frameCount);
                        auto last = m_options.IncludeFinalFrameMarkers ? m_frameCount : m_frameCount - 1;
                        if ((frame < 0 && end < 0) || frame > last)
                        {
                            continue;
                        }
                        std::wstring name;
                        for (auto ch : StringAt(entry->name()))
                        {
                            if (ch == L'\t')
                            {
                                name += L' ';
                            }
                            // Match LottieGen: discard C0 (U+0000-001F), DEL and C1
                            // (U+007F-009F) controls, with tabs converted to spaces above.
                            else if (ch >= 0x20 && (ch < 0x7F || ch > 0x9F))
                            {
                                name += ch;
                            }
                        }
                        if (name.find_first_not_of(L" \t\r\n") == std::wstring::npos)
                        {
                            name = L"anonymous";
                        }
                        markers.push_back({ std::move(name), std::max(0.0, frame) });
                    }
                }
                std::stable_sort(markers.begin(), markers.end(),
                    [](auto const& a, auto const& b) { return a.Frame < b.Frame; });

                // Suffix every duplicate, repeating if a suffixed name collides with an authored one.
                for (bool duplicate = true; duplicate;)
                {
                    duplicate = false;
                    std::map<std::wstring, std::vector<size_t>> groups;
                    for (size_t i = 0; i != markers.size(); ++i)
                    {
                        groups[markers[i].Name].push_back(i);
                    }
                    for (auto const& [name, indices] : groups)
                    {
                        if (indices.size() > 1)
                        {
                            duplicate = true;
                            for (size_t i = 0; i != indices.size(); ++i)
                            {
                                markers[indices[i]].Name = name + L"_" + std::to_wstring(i);
                            }
                        }
                    }
                }

                auto result = winrt::single_threaded_map<hstring, double>();
                for (auto const& marker : markers)
                {
                    result.Insert(hstring(marker.Name), MarkerProgress(marker.Frame));
                }
                return result.GetView();
            }

            std::span<std::byte const> m_buffer;
            Fb::LottieComposition const* m_composition{};
            AnimatedVisualSourceOptions m_options;
            double m_frameCount{};
            IMapView<hstring, double> m_markers{ nullptr };
            std::map<hstring, float4> m_colors;
            std::map<hstring, float> m_scalars;
            // Composition objects do not expose weak references; their owning visuals do.
            std::vector<winrt::weak_ref<AnimatedVisual>> m_visuals;

            bool m_imageLoadingStarted{};
            bool m_imageLoadingCompleted{ !TImages };
            size_t m_imagesRemaining{};
            Compositor m_imageCompositor{ nullptr };
            std::optional<LoadedComposition> m_pendingComposition;
            std::vector<CompositionImageSurface> m_imageSurfaces;
            std::vector<std::pair<XamlMedia::LoadedImageSurface, winrt::event_token>> m_imageSubscriptions;
            winrt::event<TypedEventHandler<IDynamicAnimatedVisualSource, IInspectable>> m_invalidated;
            winrt::event<XamlData::PropertyChangedEventHandler> m_propertyChanged;
        };
    }

    std::span<std::byte const> LoadEmbeddedComposition(
        void const* moduleAddress,
        std::span<EmbeddedCompositionVariant const> variants)
    {
        EmbeddedCompositionVariant const* selected = nullptr;
        EmbeddedCompositionVariant const* lowest = nullptr;
        for (auto const& variant : variants)
        {
            if (!lowest || variant.RequiredUapVersion < lowest->RequiredUapVersion)
            {
                lowest = &variant;
            }
            if ((!selected || selected->RequiredUapVersion < variant.RequiredUapVersion) &&
                IsUapPresent(variant.RequiredUapVersion))
            {
                selected = &variant;
            }
        }
        if (!selected)
        {
            selected = lowest;
        }
        if (!selected || !selected->ResourceName)
        {
            throw winrt::hresult_invalid_argument(L"No embedded Lottie composition variant.");
        }

        HMODULE module{};
        winrt::check_bool(GetModuleHandleExW(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            static_cast<LPCWSTR>(moduleAddress), &module));
        auto resource = FindResourceW(module, selected->ResourceName, MAKEINTRESOURCEW(10) /* RT_RCDATA */);
        if (!resource)
        {
            throw winrt::hresult_error(HRESULT_FROM_WIN32(ERROR_RESOURCE_NAME_NOT_FOUND),
                L"Missing embedded Lottie composition.");
        }
        auto size = SizeofResource(module, resource);
        auto loaded = LoadResource(module, resource);
        auto data = loaded ? LockResource(loaded) : nullptr;
        if (!data || !size)
        {
            throw winrt::hresult_error(HRESULT_FROM_WIN32(ERROR_RESOURCE_DATA_NOT_FOUND),
                L"Empty embedded Lottie composition.");
        }
        std::span<std::byte const> buffer{ static_cast<std::byte const*>(data), size };
        if (Verify(buffer)->required_uap_version() != selected->RequiredUapVersion)
        {
            throw winrt::hresult_invalid_argument(L"Embedded Lottie composition has the wrong UAP version.");
        }
        return buffer;
    }

    IAnimatedVisualSource2 CreateAnimatedVisualSource(
        std::span<std::byte const> buffer,
        AnimatedVisualSourceOptions const& options)
    {
        auto composition = Verify(buffer);
        if (HasImages(*composition))
        {
            return winrt::make<AnimatedVisualSource<true>>(buffer, composition, options).as<IAnimatedVisualSource2>();
        }
        return winrt::make<AnimatedVisualSource<false>>(buffer, composition, options).as<IAnimatedVisualSource2>();
    }

    void SetScalarProperty(
        winrt::Windows::Foundation::IInspectable const& source,
        winrt::hstring const& name,
        double value)
    {
        auto properties = source.try_as<IScalarProperties>();
        if (!properties)
        {
            throw winrt::hresult_invalid_argument(L"Source was not created by CreateAnimatedVisualSource.");
        }
        winrt::check_hresult(properties->SetScalar(static_cast<HSTRING>(winrt::get_abi(name)), value));
    }
}
