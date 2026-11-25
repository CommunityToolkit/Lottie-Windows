// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Tests LottieVisualSource against serialized compositions passed on the command line.
// Builds with the runtime and a C++/WinRT projection of Microsoft.UI.Xaml 2.8, and hosts XAML so image assets
// load through LoadedImageSurface; see source\LottieRuntime\README.md.

#include <unknwn.h>
#include <Windows.h>
#include <DispatcherQueue.h>

// Include the implementation to read back each visual's theme properties.
#include "../../source/LottieRuntime/LottieVisualSource.cpp"

#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.h>
#include <winrt/Windows.UI.Xaml.Hosting.h>
#include <winrt/Windows.UI.Xaml.Controls.h>

#include <fstream>
#include <functional>
#include <iostream>
#include <stdexcept>
#include <string>

namespace
{
    namespace Runtime = CommunityToolkit::WinUI2::Lottie;
    namespace Fb = CommunityToolkit::WinUI::Lottie::CompDataFlatbuffer::Schema;
    namespace Mux = winrt::Microsoft::UI::Xaml::Controls;
    using namespace winrt::Windows::UI::Composition;

    void Require(bool condition, char const* message)
    {
        if (!condition)
        {
            throw std::runtime_error(message);
        }
    }

    std::vector<std::byte> Read(wchar_t const* path)
    {
        std::ifstream file(path, std::ios::binary | std::ios::ate);
        Require(file.good(), "Cannot open file.");
        std::vector<std::byte> bytes(static_cast<size_t>(file.tellg()));
        file.seekg(0);
        file.read(reinterpret_cast<char*>(bytes.data()), static_cast<std::streamsize>(bytes.size()));
        Require(file.good(), "Cannot read file.");
        return bytes;
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

    bool AnimationsRunning(Visual const& visual)
    {
        // Every serialized tree animates a property somewhere; find a keyframe controller.
        auto found = false;
        auto check = [&](CompositionObject const& object, wchar_t const* property)
        {
            found = found || object.TryGetAnimationController(property) != nullptr;
        };
        std::function<void(Visual const&)> walk = [&](Visual const& node)
        {
            for (auto property : { L"Opacity", L"IsVisible", L"Offset", L"Scale", L"RotationAngleInDegrees",
                L"TransformMatrix", L"Size" })
            {
                check(node, property);
            }
            if (auto container = node.try_as<ContainerVisual>())
            {
                for (auto child : container.Children())
                {
                    walk(child);
                }
            }
        };
        walk(visual);
        return found;
    }

    void TestMarkerPolicies()
    {
        flatbuffers::FlatBufferBuilder builder;
        auto names = builder.CreateVector(std::vector{
            builder.CreateString("Duplicate"),
            builder.CreateString("A\tB\x1F ~\x7F\xC2\x80\xC2\x9F\xC2\xA0"),
            builder.CreateString("Overlapping"),
        });
        auto markers = builder.CreateVector(std::vector{
            Fb::CreateMarker(builder, 0, 0.25f),
            Fb::CreateMarker(builder, 0, 0.5f),
            Fb::CreateMarker(builder, 1, 0.75f),
            Fb::CreateMarker(builder, 2, -0.25f, 0.5f),
        });
        auto metadata = Fb::CreateMetadata(builder, 0, 64, 32, 10000000, 60, 0, markers);
        auto visual = Fb::CreateVisual(builder);
        auto visuals = builder.CreateVector(std::vector{ visual });
        Fb::LottieCompositionBuilder composition(builder);
        composition.add_schema_version(1);
        composition.add_root_visual(0);
        composition.add_strings(names);
        composition.add_visuals(visuals);
        composition.add_metadata(metadata);
        Fb::FinishLottieCompositionBuffer(builder, composition.Finish());
        std::span<std::byte const> bytes{
            reinterpret_cast<std::byte const*>(builder.GetBufferPointer()), builder.GetSize() };

        Runtime::AnimatedVisualSourceOptions options;
        options.Markers = Runtime::MarkerPolicy::FirstWins;
        auto first = Runtime::CreateAnimatedVisualSource(bytes, options).Markers();
        Require(first.Size() == 2 && first.HasKey(L"Duplicate"), "Wrong first-wins markers.");
        Require(first.Lookup(L"Duplicate") < 0.3, "Duplicate replaced the first marker.");
        Require(!first.HasKey(L"Overlapping"), "First-wins retained an out-of-range marker.");

        options.Markers = Runtime::MarkerPolicy::Unique;
        auto unique = Runtime::CreateAnimatedVisualSource(bytes, options).Markers();
        Require(unique.Size() == 4 && unique.HasKey(L"Duplicate_0") && unique.HasKey(L"Duplicate_1"),
            "Duplicate markers were not renamed.");
        Require(unique.HasKey(L"A B ~\u00A0"), "Marker control-character filtering changed.");
        Require(unique.HasKey(L"Overlapping") && unique.Lookup(L"Overlapping") == 0,
            "Unique policy dropped an overlapping marker.");
    }

    void Test(Compositor const& compositor, wchar_t const* path)
    {
        auto bytes = Read(path);
        auto composition = Fb::GetLottieComposition(bytes.data());
        auto source = Runtime::CreateAnimatedVisualSource(bytes);
        Require(source.try_as<Mux::IAnimatedVisualSource>() != nullptr, "Not an IAnimatedVisualSource.");

        auto const images = HasImages(*composition);
        Require((source.try_as<Mux::IDynamicAnimatedVisualSource>() != nullptr) == images, "Wrong dynamic source.");

        winrt::Windows::Foundation::IInspectable diagnostics{ nullptr };
        auto visual = source.TryCreateAnimatedVisual(compositor, diagnostics);
        Require(diagnostics == nullptr, "Unexpected diagnostics.");
        Require(visual != nullptr, "No visual.");
        Require(visual.RootVisual() != nullptr, "No root.");
        Require(visual.Duration().count() == composition->metadata()->duration_ticks(), "Wrong duration.");

        auto visual2 = visual.try_as<Mux::IAnimatedVisual2>();
        Require(visual2 != nullptr, "Not an IAnimatedVisual2.");
        auto const animated = AnimationsRunning(visual.RootVisual());
        for (int cycle = 0; cycle != 2; ++cycle)
        {
            visual2.DestroyAnimations();
            visual2.DestroyAnimations();
            Require(!AnimationsRunning(visual.RootVisual()), "Animations not destroyed.");
            visual2.CreateAnimations();
            visual2.CreateAnimations();
            Require(AnimationsRunning(visual.RootVisual()) == animated, "Animations not recreated.");
        }

        auto markers = source.Markers();
        for (auto const& marker : markers)
        {
            Require(marker.Value() >= 0 && marker.Value() <= 1, "Marker out of range.");
        }

        // Bound colors and scalars reach live and later visuals; unbound names are ignored.
        auto theme = winrt::get_self<Runtime::AnimatedVisual>(visual)->ThemeProperties();
        source.SetColorProperty(L"NotABinding", winrt::Windows::UI::Color{ 255, 1, 2, 3 });
        Runtime::SetScalarProperty(source, L"NotABinding", 1);
        winrt::Windows::Foundation::Numerics::float4 color{};
        float scalar{};
        if (theme)
        {
            Require(theme.TryGetVector4(L"NotABinding", color) != CompositionGetValueStatus::Succeeded,
                "Unbound color applied.");
            Require(theme.TryGetScalar(L"NotABinding", scalar) != CompositionGetValueStatus::Succeeded,
                "Unbound scalar applied.");
        }
        std::vector<std::pair<winrt::hstring, Fb::PropertyValueType>> bound;
        if (auto bindings = composition->metadata()->property_bindings())
        {
            for (auto binding : *bindings)
            {
                auto name = winrt::to_hstring(composition->strings()->Get(binding->name())->str());
                if (binding->exposed_type() == Fb::PropertyValueType::Color)
                {
                    source.SetColorProperty(name, winrt::Windows::UI::Color{ 40, 10, 20, 30 });
                    bound.emplace_back(name, binding->exposed_type());
                }
                else if (binding->exposed_type() == Fb::PropertyValueType::Scalar)
                {
                    Runtime::SetScalarProperty(source, name, 0.25);
                    bound.emplace_back(name, binding->exposed_type());
                }
            }
        }
        Require(bound.empty() || theme, "Bindings without theme properties.");
        auto later = source.TryCreateAnimatedVisual(compositor, diagnostics);
        Require(later != nullptr, "No second visual.");
        for (auto const& set : { theme, winrt::get_self<Runtime::AnimatedVisual>(later)->ThemeProperties() })
        {
            for (auto const& [name, type] : bound)
            {
                if (type == Fb::PropertyValueType::Color)
                {
                    Require(set.TryGetVector4(name, color) == CompositionGetValueStatus::Succeeded &&
                        color == winrt::Windows::Foundation::Numerics::float4{ 10, 20, 30, 40 }, "Color not applied.");
                }
                else
                {
                    Require(set.TryGetScalar(name, scalar) == CompositionGetValueStatus::Succeeded && scalar == 0.25f,
                        "Scalar not applied.");
                }
            }
        }
        later.as<winrt::Windows::Foundation::IClosable>().Close();

        visual.as<winrt::Windows::Foundation::IClosable>().Close();
        Require(visual.RootVisual() == nullptr, "Root not released.");
        visual2.DestroyAnimations();
        visual2.CreateAnimations();

        // Image surfaces are tied to their compositor. Using another one throws by default
        // and becomes diagnostics on request.
        if (images)
        {
            Compositor other;
            try
            {
                source.TryCreateAnimatedVisual(other, diagnostics);
                throw std::runtime_error("Another compositor accepted.");
            }
            catch (winrt::hresult_invalid_argument const&)
            {
            }

            Runtime::AnimatedVisualSourceOptions options;
            options.ReportLoadErrors = true;
            source = Runtime::CreateAnimatedVisualSource(bytes, options);
            Require(source.TryCreateAnimatedVisual(compositor, diagnostics) != nullptr && !diagnostics,
                "No visual with diagnostics.");
            Require(source.TryCreateAnimatedVisual(other, diagnostics) == nullptr, "Error not reported.");
            Require(winrt::unbox_value_or<winrt::hstring>(diagnostics, L"") != L"", "No diagnostics.");
        }

        try
        {
            Runtime::SetScalarProperty(compositor.CreatePropertySet(), L"Name", 1);
            throw std::runtime_error("Foreign source accepted.");
        }
        catch (winrt::hresult_invalid_argument const&)
        {
        }
        std::wcout << path << L": markers=" << markers.Size()
            << L", animated=" << (animated ? L"yes" : L"no") << (images ? L", images" : L"") << L"\n";
    }
}

int wmain(int argc, wchar_t** argv)
{
    try
    {
        winrt::init_apartment(winrt::apartment_type::single_threaded);
        DispatcherQueueOptions const options{ sizeof(DispatcherQueueOptions), DQTYPE_THREAD_CURRENT, DQTAT_COM_NONE };
        winrt::Windows::System::DispatcherQueueController queue{ nullptr };
        winrt::check_hresult(CreateDispatcherQueueController(
            options, reinterpret_cast<PDISPATCHERQUEUECONTROLLER*>(winrt::put_abi(queue))));
        auto xaml = winrt::Windows::UI::Xaml::Hosting::WindowsXamlManager::InitializeForCurrentThread();
        {
            winrt::Windows::UI::Xaml::Controls::Grid host;
            auto compositor = winrt::Windows::UI::Xaml::Hosting::ElementCompositionPreview::GetElementVisual(host).Compositor();
            TestMarkerPolicies();

            try
            {
                Runtime::CreateAnimatedVisualSource({});
                throw std::runtime_error("Empty buffer accepted.");
            }
            catch (winrt::hresult_invalid_argument const&)
            {
            }

            for (int i = 1; i < argc; ++i)
            {
                Test(compositor, argv[i]);
            }
        }
        queue.ShutdownQueueAsync();
        std::cout << "Visual source tests passed.\n";
        return 0;
    }
    catch (winrt::hresult_error const& error)
    {
        std::wcerr << error.message().c_str() << L"\n";
    }
    catch (std::exception const& error)
    {
        std::cerr << error.what() << "\n";
    }
    return 1;
}
