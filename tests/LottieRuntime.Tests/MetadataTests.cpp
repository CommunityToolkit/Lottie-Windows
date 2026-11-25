// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <unknwn.h>
#include <Windows.h>
#include <DispatcherQueue.h>

// Include the implementation to test realization-cache identity as well as the API.
#include "../../source/LottieRuntime/LottieRuntime.cpp"

#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.Xaml.Hosting.h>
#include <winrt/Windows.UI.Xaml.Controls.h>

#include <fstream>
#include <iostream>
#include <limits>
#include <stdexcept>

namespace
{
    void Require(bool condition)
    {
        if (!condition)
        {
            throw std::runtime_error("Metadata assertion failed.");
        }
    }

    struct Options
    {
        bool Metadata = true;
        bool Markers = true;
        bool ReferenceTheme = true;
        bool ReferenceOwnerProperties = false;
        bool Image = false;
        bool VisualSurface = false;
        uint32_t Theme = 0;
        uint32_t ThemeOwner = NullIndex;
        uint32_t Name = 0;
        uint32_t MarkerName = 1;
        uint32_t BindingName = 2;
        uint32_t BindingDisplayName = 2;
        Fb::PropertyValueType BindingType = Fb::PropertyValueType::Scalar;
        float Width = 64;
        float Height = 32;
        int64_t Ticks = 12345678;
        float Fps = 60;
        float Progress = 0.25f;
        float DurationProgress = 0.5f;
        float DefaultScalar = 0.75f;
        uint16_t SchemaVersion = 1;
    };

    std::vector<std::byte> Buffer(Options const& options = {})
    {
        flatbuffers::FlatBufferBuilder builder;
        std::vector<flatbuffers::Offset<flatbuffers::String>> strings;
        for (auto text : { "Animation", "Marker", "Opacity", "Theme", "Theme.Opacity", "Progress", "Theme.Progress" })
        {
            strings.push_back(builder.CreateString(text));
        }
        auto const stringsVector = builder.CreateVector(strings);

        uint8_t const png[] = {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x04, 0x00, 0x00, 0x00, 0xB5, 0x1C, 0x0C,
            0x02, 0x00, 0x00, 0x00, 0x0B, 0x49, 0x44, 0x41,
            0x54, 0x78, 0xDA, 0x63, 0xFC, 0xFF, 0x1F, 0x00,
            0x03, 0x03, 0x02, 0x00, 0xEF, 0x9A, 0x2F, 0x5B,
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44,
            0xAE, 0x42, 0x60, 0x82,
        };
        auto const pngBytes = builder.CreateVector(png, std::size(png));
        auto const image = Fb::CreateSurface(
            builder, 0, Fb::SurfaceKind::LoadedImageFromStream, NullIndex, nullptr, nullptr, NullIndex, pngBytes);
        auto const visualSurface = Fb::CreateSurface(
            builder, 0, Fb::SurfaceKind::VisualSurface, options.VisualSurface ? 1u : NullIndex);
        auto const surfaces = builder.CreateVector(std::vector{ visualSurface, image, image });
        Fb::BrushBuilder brushBuilder(builder);
        brushBuilder.add_kind(Fb::BrushKind::Surface);
        brushBuilder.add_surface(options.VisualSurface ? 0u : 1u);
        auto const brush = brushBuilder.Finish();
        auto const brushes = builder.CreateVector(std::vector{ brush });

        auto const value = Fb::CreatePropertyValue(builder, 2, Fb::PropertyValueType::Scalar, 0.75f);
        auto const themeValues = builder.CreateVector(std::vector{ value });
        auto const theme = Fb::CreatePropertySet(builder, 0, options.ThemeOwner, themeValues);
        auto const progressValue = Fb::CreatePropertyValue(builder, 5, Fb::PropertyValueType::Scalar, 0);
        auto const progressValues = builder.CreateVector(std::vector{ progressValue });
        auto const progress = Fb::CreatePropertySet(builder, 0, 0, progressValues);
        auto const sets = builder.CreateVector(std::vector{ theme, progress });

        auto const reference = Fb::CreateReferenceParameter(
            builder, 3, (static_cast<uint32_t>(Fb::ObjectCategory::PropertySet) << CategoryShift) |
                (options.ReferenceOwnerProperties ? 1u : 0u));
        auto const references = builder.CreateVector(std::vector{ reference });
        auto const animation = Fb::CreateAnimation(
            builder, 0, Fb::AnimationKind::Expression, NullIndex, references,
            options.ReferenceOwnerProperties ? 6 : 4);
        auto const animations = builder.CreateVector(std::vector{ animation });
        auto const animator = Fb::CreateAnimator(builder, 2, 0);
        auto const animators = builder.CreateVector(std::vector{ animator });
        auto const childBase = Fb::CreateCompObj(builder, NullIndex, animators);
        Fb::VisualBuilder childBuilder(builder);
        childBuilder.add_base(childBase);
        if (options.VisualSurface)
        {
            childBuilder.add_kind(Fb::VisualKind::Sprite);
            childBuilder.add_brush(0);
        }
        auto const child = childBuilder.Finish();
        auto const children = builder.CreateVector(std::vector<uint32_t>{ 1 });
        auto const visualBase = Fb::CreateCompObj(
            builder, NullIndex, options.ReferenceTheme && !options.ReferenceOwnerProperties ? animators : 0, 1);
        Fb::VisualBuilder visualBuilder(builder);
        visualBuilder.add_base(visualBase);
        if (options.Image || options.VisualSurface)
        {
            visualBuilder.add_kind(Fb::VisualKind::Sprite);
            visualBuilder.add_brush(0);
        }
        if (options.ReferenceOwnerProperties)
        {
            visualBuilder.add_children(children);
        }
        auto const visual = visualBuilder.Finish();
        auto const visuals = builder.CreateVector(std::vector{ visual, child });

        auto const marker = Fb::CreateMarker(
            builder, options.MarkerName, options.Progress, options.DurationProgress);
        auto const markers = builder.CreateVector(std::vector{ marker, marker });
        auto const binding = Fb::CreatePropertyBinding(
            builder, options.BindingName, options.BindingDisplayName,
            options.BindingType, options.BindingType, options.DefaultScalar);
        auto const bindings = builder.CreateVector(std::vector{ binding });
        auto const metadata = Fb::CreateMetadata(
            builder, options.Name, options.Width, options.Height, options.Ticks,
            options.Fps, bindings, options.Markers ? markers : 0, options.Theme);

        Fb::LottieCompositionBuilder root(builder);
        root.add_schema_version(options.SchemaVersion);
        root.add_root_visual(0);
        root.add_strings(stringsVector);
        root.add_visuals(visuals);
        root.add_property_sets(sets);
        root.add_animations(animations);
        root.add_surfaces(surfaces);
        root.add_brushes(brushes);
        if (options.Metadata)
        {
            root.add_metadata(metadata);
        }
        Fb::FinishLottieCompositionBuffer(builder, root.Finish());
        auto const* first = reinterpret_cast<std::byte const*>(builder.GetBufferPointer());
        return { first, first + builder.GetSize() };
    }

    void Invalid(
        Compositor const& compositor,
        std::vector<std::byte> const& bytes,
        std::span<Runtime::CompositionImageSurface const> images = {})
    {
        try
        {
            Runtime::LoadCompositionWithMetadata(compositor, bytes, images);
        }
        catch (winrt::hresult_invalid_argument const&)
        {
            return;
        }
        throw std::runtime_error("Malformed input was not rejected with E_INVALIDARG.");
    }

    // Every recorded animation is a keyframe animation, which always has a controller
    // while it runs and none once it is stopped.
    void RequireAnimationsRunning(Runtime::LoadedComposition const& loaded, bool running)
    {
        for (auto const& binding : loaded.Animations->Bindings)
        {
            Require(binding.Animation.try_as<ExpressionAnimation>() == nullptr);
            Require((binding.Target.TryGetAnimationController(binding.Property) != nullptr) == running);
        }
    }

    // Returns the number of recorded animations.
    size_t TestRestart(Runtime::LoadedComposition const& loaded)
    {
        Require(Runtime::CanRestartAnimations(loaded));
        RequireAnimationsRunning(loaded, true);
        for (int cycle = 0; cycle != 2; ++cycle)
        {
            Runtime::StopAnimations(loaded);
            Runtime::StopAnimations(loaded);
            RequireAnimationsRunning(loaded, false);
            Runtime::StartAnimations(loaded);
            Runtime::StartAnimations(loaded);
            RequireAnimationsRunning(loaded, true);
        }

        // Restarting must not disturb the expression that drives playback.
        Runtime::ProgressPropertySet(loaded.Root).InsertScalar(L"Progress", 0.5f);
        return loaded.Animations->Bindings.size();
    }

    void Test(Compositor const& compositor)
    {
        auto bytes = Buffer();
        auto loaded = Runtime::LoadCompositionWithMetadata(compositor, bytes);
        Require(loaded.Root != nullptr);
        Require(loaded.ImageSurfaces.empty());
        Require(loaded.Duration.count() == 12345678);
        Require(loaded.Size.x == 64 && loaded.Size.y == 32);
        Require(loaded.FramesPerSecond == 60);
        Require(loaded.Markers.size() == 2);
        Require(loaded.Markers[0].Name == L"Marker");
        Require(loaded.Markers[0].Progress == 0.25f);
        Require(loaded.Markers[0].DurationProgress == 0.5f);
        Require(loaded.Markers[1].Name == loaded.Markers[0].Name);
        Require(loaded.ThemeProperties != nullptr);
        Require(loaded.ThemeProperties != Runtime::ProgressPropertySet(loaded.Root));
        float value = -1;
        Require(loaded.ThemeProperties.TryGetScalar(L"Opacity", value) == CompositionGetValueStatus::Succeeded);
        Require(value == 0.75f);
        Require(Runtime::ProgressPropertySet(loaded.Root).TryGetScalar(L"Progress", value) ==
            CompositionGetValueStatus::Succeeded && value == 0);

        // Expression bindings are not stopped, so this tree records no animations.
        Require(TestRestart(loaded) == 0);
        Runtime::LoadedComposition empty;
        Require(!Runtime::CanRestartAnimations(empty));
        Runtime::StopAnimations(empty);
        Runtime::StartAnimations(empty);

        // The expression realizes the theme while loading the root. Metadata must
        // return that same cached object, including changes made after realization.
        Interpreter interpreter(compositor, VerifyComposition(bytes));
        auto first = interpreter.RunWithMetadata();
        first.ThemeProperties.InsertScalar(L"Opacity", 0.125f);
        auto second = interpreter.RunWithMetadata();
        Require(first.Root == second.Root && first.ThemeProperties == second.ThemeProperties);
        Require(second.ThemeProperties.TryGetScalar(L"Opacity", value) ==
            CompositionGetValueStatus::Succeeded && value == 0.125f);

        bytes.clear();
        Require(loaded.Markers[0].Name == L"Marker" && loaded.Duration.count() == 12345678);
        loaded.ThemeProperties.InsertScalar(L"Opacity", 0.25f);

        Options options;
        options.VisualSurface = true;
        auto surfaceComposition = Runtime::LoadCompositionWithMetadata(compositor, Buffer(options));
        auto surfaceBrush = surfaceComposition.Root.as<SpriteVisual>().Brush().as<CompositionSurfaceBrush>();
        auto sourceVisual = surfaceBrush.Surface().as<CompositionVisualSurface>().SourceVisual().as<SpriteVisual>();
        Require(sourceVisual.Brush() == surfaceBrush);

        options = {};
        options.Image = true;
        bytes = Buffer(options);
        loaded = Runtime::LoadCompositionWithMetadata(compositor, bytes);
        Require(loaded.ImageSurfaces.size() == 1 && loaded.ImageSurfaces[0].Index == 1);
        auto const image = loaded.ImageSurfaces[0].Surface;
        Require(image.try_as<XamlMedia::LoadedImageSurface>() != nullptr);
        Require(loaded.Root.as<SpriteVisual>().Brush().as<CompositionSurfaceBrush>().Surface() == image);
        auto reused = Runtime::LoadCompositionWithMetadata(compositor, bytes, loaded.ImageSurfaces);
        Require(reused.Root != loaded.Root && reused.ImageSurfaces.size() == 1);
        Require(reused.ImageSurfaces[0].Index == 1 && reused.ImageSurfaces[0].Surface == image);
        Require(reused.Root.as<SpriteVisual>().Brush().as<CompositionSurfaceBrush>().Surface() == image);
        Interpreter imageInterpreter(compositor, VerifyComposition(bytes), loaded.ImageSurfaces);
        auto cachedImages = imageInterpreter.RunWithMetadata();
        Require(cachedImages.ImageSurfaces[0].Surface == image);
        Require(imageInterpreter.RunWithMetadata().ImageSurfaces[0].Surface == image);

        auto images = loaded.ImageSurfaces;
        images.push_back(images[0]);
        Invalid(compositor, bytes, images);
        for (auto index : { 0u, 99u, NullIndex })
        {
            images = loaded.ImageSurfaces;
            images[0].Index = index;
            Invalid(compositor, bytes, images);
        }
        images = loaded.ImageSurfaces;
        images[0].Surface = nullptr;
        Invalid(compositor, bytes, images);
        images[0].Surface = compositor.CreateVisualSurface();
        Invalid(compositor, bytes, images);

        options = {};
        options.Theme = NullIndex;
        options.Markers = false;
        loaded = Runtime::LoadCompositionWithMetadata(compositor, Buffer(options));
        Require(loaded.ThemeProperties == nullptr && loaded.Markers.empty());

        options = {};
        options.ReferenceTheme = false;
        loaded = Runtime::LoadCompositionWithMetadata(compositor, Buffer(options));
        Require(loaded.ThemeProperties != nullptr);
        Require(loaded.ThemeProperties.TryGetScalar(L"Opacity", value) ==
            CompositionGetValueStatus::Succeeded && value == 0.75f);

        options = {};
        options.ReferenceOwnerProperties = true;
        loaded = Runtime::LoadCompositionWithMetadata(compositor, Buffer(options));
        Require(loaded.Root.as<ContainerVisual>().Children().Count() == 1);
        Require(Runtime::ProgressPropertySet(loaded.Root).TryGetScalar(L"Progress", value) ==
            CompositionGetValueStatus::Succeeded && value == 0);

        options = {};
        options.Progress = -0.5f;
        options.DurationProgress = 2;
        loaded = Runtime::LoadCompositionWithMetadata(compositor, Buffer(options));
        Require(loaded.Markers[0].Progress == -0.5f && loaded.Markers[0].DurationProgress == 2);

        options = {};
        options.Width = options.Height = options.Fps = 0;
        options.Ticks = 0;
        loaded = Runtime::LoadCompositionWithMetadata(compositor, Buffer(options));
        Require(loaded.Duration.count() == 0 && loaded.Size.x == 0 && loaded.FramesPerSecond == 0);

        options = {};
        options.Metadata = false;
        bytes = Buffer(options);
        Require(Runtime::LoadComposition(compositor, bytes) != nullptr);
        Invalid(compositor, bytes);

        auto const nan = std::numeric_limits<float>::quiet_NaN();
        auto const infinity = std::numeric_limits<float>::infinity();
        for (auto bad : { -1.0f, nan, infinity })
        {
            options = {}; options.Width = bad; Invalid(compositor, Buffer(options));
            options = {}; options.Height = bad; Invalid(compositor, Buffer(options));
            options = {}; options.Fps = bad; Invalid(compositor, Buffer(options));
            options = {}; options.DurationProgress = bad; Invalid(compositor, Buffer(options));
        }
        for (auto bad : { nan, infinity })
        {
            options = {}; options.Progress = bad; Invalid(compositor, Buffer(options));
            options = {}; options.DefaultScalar = bad; Invalid(compositor, Buffer(options));
        }
        options = {}; options.Ticks = -1; Invalid(compositor, Buffer(options));
        options = {}; options.Theme = 99; Invalid(compositor, Buffer(options));
        options = {}; options.Theme = 1; Invalid(compositor, Buffer(options));
        options = {}; options.ThemeOwner = 0; Invalid(compositor, Buffer(options));
        options = {}; options.Name = 99; Invalid(compositor, Buffer(options));
        for (auto bad : { 99u, NullIndex })
        {
            options = {}; options.MarkerName = bad; Invalid(compositor, Buffer(options));
            options = {}; options.BindingName = bad; Invalid(compositor, Buffer(options));
            options = {}; options.BindingDisplayName = bad; Invalid(compositor, Buffer(options));
        }
        options = {};
        options.BindingType = static_cast<Fb::PropertyValueType>(255);
        Invalid(compositor, Buffer(options));

        bytes = Buffer();
        for (size_t length = 0; length < bytes.size(); length += 8)
        {
            Invalid(compositor, { bytes.begin(), bytes.begin() + length });
        }
        bytes[4] = std::byte{ 'X' };
        Invalid(compositor, bytes);
        Invalid(compositor, {});

        options = {}; options.SchemaVersion = 2;
        try
        {
            Runtime::LoadCompositionWithMetadata(compositor, Buffer(options));
            throw std::runtime_error("Unsupported schema was not rejected.");
        }
        catch (winrt::hresult_not_implemented const&)
        {
        }
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
            bool const legacy = argc > 1 && std::wstring_view(argv[1]) == L"--legacy";
            if (!legacy)
            {
                Test(compositor);
            }
            for (int i = legacy ? 2 : 1; i < argc; ++i)
            {
                std::ifstream file(argv[i], std::ios::binary | std::ios::ate);
                Require(file.good());
                std::vector<std::byte> bytes(static_cast<size_t>(file.tellg()));
                file.seekg(0);
                file.read(reinterpret_cast<char*>(bytes.data()), static_cast<std::streamsize>(bytes.size()));
                Require(file.good());
                if (legacy)
                {
                    auto root = Runtime::LoadComposition(compositor, bytes);
                    Require(root != nullptr);
                    std::wcout << argv[i] << L": legacy composition loaded\n";
                    continue;
                }
                auto loaded = Runtime::LoadCompositionWithMetadata(compositor, bytes);
                Require(loaded.Root != nullptr);
                auto const* metadata = VerifyComposition(bytes).metadata();
                Require((loaded.ThemeProperties != nullptr) == (metadata->theming_property_set() != NullIndex));
                if (!loaded.ImageSurfaces.empty())
                {
                    auto reused = Runtime::LoadCompositionWithMetadata(compositor, bytes, loaded.ImageSurfaces);
                    Require(reused.Root != loaded.Root && reused.ImageSurfaces.size() == loaded.ImageSurfaces.size());
                    for (size_t image = 0; image < loaded.ImageSurfaces.size(); ++image)
                    {
                        Require(loaded.ImageSurfaces[image].Surface.try_as<XamlMedia::LoadedImageSurface>() != nullptr);
                        Require(reused.ImageSurfaces[image].Index == loaded.ImageSurfaces[image].Index);
                        Require(reused.ImageSurfaces[image].Surface == loaded.ImageSurfaces[image].Surface);
                    }
                }
                std::wcout << argv[i] << L": " << loaded.Size.x << L"x" << loaded.Size.y
                    << L", ticks=" << loaded.Duration.count() << L", fps=" << loaded.FramesPerSecond
                    << L", markers=" << loaded.Markers.size()
                    << L", theme=" << (loaded.ThemeProperties != nullptr ? L"present" : L"absent")
                    << L", images=" << loaded.ImageSurfaces.size()
                    << L", restartable animations=" << TestRestart(loaded) << L"\n";
            }
        }
        queue.ShutdownQueueAsync();
        std::cout << "Metadata tests passed.\n";
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
