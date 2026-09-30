// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Loads a serialized Lottie composition through the registration-free COM surface.

#include <unknwn.h>

#include <Windows.h>
#include <DispatcherQueue.h>
#include <shlwapi.h>
#include <windows.ui.composition.interop.h>

#include <LottieRuntimeCom.h>

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Composition.Desktop.h>

#include <chrono>
#include <fstream>
#include <iostream>
#include <string>
#include <vector>

using namespace winrt;
using namespace winrt::Windows::UI::Composition;
using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::Foundation::Collections;

namespace
{
    winrt::Windows::System::DispatcherQueueController CreateDispatcherQueue()
    {
        DispatcherQueueOptions const options{
            sizeof(DispatcherQueueOptions),
            DQTYPE_THREAD_CURRENT,
            DQTAT_COM_NONE,
        };

        winrt::Windows::System::DispatcherQueueController controller{ nullptr };
        winrt::check_hresult(CreateDispatcherQueueController(
            options,
            reinterpret_cast<PDISPATCHERQUEUECONTROLLER*>(winrt::put_abi(controller))));
        return controller;
    }

    winrt::com_ptr<ILottieCompositionLoader> CreateLoader()
    {
        wchar_t moduleDirectory[MAX_PATH]{};
        winrt::check_bool(GetModuleFileNameW(nullptr, moduleDirectory, MAX_PATH));
        winrt::check_bool(PathRemoveFileSpecW(moduleDirectory));

        auto const manifestPath =
            std::wstring(moduleDirectory) + L"\\LottieRuntime.manifest";

        ACTCTXW context{ sizeof(ACTCTXW) };
        context.lpSource = manifestPath.c_str();
        context.lpAssemblyDirectory = moduleDirectory;
        context.dwFlags = ACTCTX_FLAG_ASSEMBLY_DIRECTORY_VALID;

        auto const activationContext = CreateActCtxW(&context);
        winrt::check_bool(activationContext != INVALID_HANDLE_VALUE);

        ULONG_PTR cookie{};
        winrt::check_bool(ActivateActCtx(activationContext, &cookie));

        winrt::com_ptr<ILottieCompositionLoader> loader;
        auto const result = CoCreateInstance(
            CLSID_LottieCompositionLoader,
            nullptr,
            CLSCTX_INPROC_SERVER,
            __uuidof(ILottieCompositionLoader),
            loader.put_void());

        DeactivateActCtx(0, cookie);
        ReleaseActCtx(activationContext);
        winrt::check_hresult(result);
        return loader;
    }

    std::vector<std::byte> ReadFileBytes(wchar_t const* path)
    {
        std::ifstream file(path, std::ios::binary | std::ios::ate);
        if (!file)
        {
            throw std::runtime_error("Could not open input file.");
        }

        auto const size = static_cast<size_t>(file.tellg());
        std::vector<std::byte> result(size);
        file.seekg(0);
        file.read(reinterpret_cast<char*>(result.data()), static_cast<std::streamsize>(size));
        return result;
    }

    void PrintLine(int depth, std::wstring_view text)
    {
        std::wstring indent(static_cast<size_t>(depth) * 2, L' ');
        std::wcout << indent << text << L"\n";
    }

    hstring RuntimeClassName(winrt::Windows::Foundation::IInspectable const& node)
    {
        if (node == nullptr)
        {
            return L"(null)";
        }

        return winrt::get_class_name(node);
    }

    hstring CommentOf(CompositionObject const& node)
    {
        auto comment = node.Comment();
        return comment.empty() ? L"" : (L" \"" + comment + L"\"");
    }

    void DumpBrush(CompositionBrush const& brush, int depth);
    void DumpVisual(Visual const& visual, int depth);
    void DumpShape(CompositionShape const& shape, int depth);

    void DumpShape(CompositionShape const& shape, int depth)
    {
        if (shape == nullptr)
        {
            return;
        }

        PrintLine(depth, RuntimeClassName(shape) + CommentOf(shape));

        if (auto containerShape = shape.try_as<CompositionContainerShape>())
        {
            for (auto const& child : containerShape.Shapes())
            {
                DumpShape(child, depth + 1);
            }
        }

        if (auto spriteShape = shape.try_as<CompositionSpriteShape>())
        {
            if (auto fill = spriteShape.FillBrush())
            {
                PrintLine(depth + 1, L"FillBrush:");
                DumpBrush(fill, depth + 2);
            }

            if (auto stroke = spriteShape.StrokeBrush())
            {
                PrintLine(depth + 1, L"StrokeBrush:");
                DumpBrush(stroke, depth + 2);
            }
        }
    }

    void DumpBrush(CompositionBrush const& brush, int depth)
    {
        if (brush == nullptr)
        {
            return;
        }

        PrintLine(depth, RuntimeClassName(brush) + CommentOf(brush));

        if (auto linearGradient = brush.try_as<CompositionLinearGradientBrush>())
        {
            for (auto const& stop : linearGradient.ColorStops())
            {
                PrintLine(depth + 1, L"ColorStop " + RuntimeClassName(stop));
            }
        }
    }

    void DumpVisual(Visual const& visual, int depth)
    {
        if (visual == nullptr)
        {
            return;
        }

        PrintLine(depth, RuntimeClassName(visual) + CommentOf(visual));

        if (auto spriteVisual = visual.try_as<SpriteVisual>())
        {
            if (auto brush = spriteVisual.Brush())
            {
                PrintLine(depth + 1, L"Brush:");
                DumpBrush(brush, depth + 2);
            }

            if (auto shapeVisual = visual.try_as<ShapeVisual>())
            {
                for (auto const& shape : shapeVisual.Shapes())
                {
                    DumpShape(shape, depth + 1);
                }
            }
        }
        else if (auto shapeVisual = visual.try_as<ShapeVisual>())
        {
            for (auto const& shape : shapeVisual.Shapes())
            {
                DumpShape(shape, depth + 1);
            }
        }

        if (auto containerVisual = visual.try_as<ContainerVisual>())
        {
            for (auto const& child : containerVisual.Children())
            {
                DumpVisual(child, depth + 1);
            }
        }
    }

    LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
    {
        if (message == WM_DESTROY)
        {
            PostQuitMessage(0);
            return 0;
        }

        return DefWindowProcW(window, message, wParam, lParam);
    }

    void ShowVisual(Visual const& root)
    {
        auto const instance = GetModuleHandleW(nullptr);
        wchar_t const className[] = L"LottieRuntimeWindow";

        WNDCLASSW windowClass{};
        windowClass.lpfnWndProc = WindowProc;
        windowClass.hInstance = instance;
        windowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        windowClass.hbrBackground = static_cast<HBRUSH>(GetStockObject(WHITE_BRUSH));
        windowClass.lpszClassName = className;
        winrt::check_bool(RegisterClassW(&windowClass) != 0);

        auto const window = CreateWindowExW(
            0,
            className,
            L"LottieRuntime",
            WS_OVERLAPPEDWINDOW,
            CW_USEDEFAULT,
            CW_USEDEFAULT,
            900,
            700,
            nullptr,
            nullptr,
            instance,
            nullptr);
        winrt::check_bool(window != nullptr);

        auto compositor = root.Compositor();
        auto compositorInterop =
            compositor.as<ABI::Windows::UI::Composition::Desktop::ICompositorDesktopInterop>();
        winrt::Windows::UI::Composition::Desktop::DesktopWindowTarget target{ nullptr };
        winrt::check_hresult(compositorInterop->CreateDesktopWindowTarget(
            window,
            false,
            reinterpret_cast<ABI::Windows::UI::Composition::Desktop::IDesktopWindowTarget**>(
                winrt::put_abi(target))));
        target.Root(root);

        auto progress = compositor.CreateScalarKeyFrameAnimation();
        progress.InsertKeyFrame(0.0f, 0.0f);
        progress.InsertKeyFrame(1.0f, 1.0f);
        progress.Duration(std::chrono::seconds(5));
        progress.IterationBehavior(AnimationIterationBehavior::Forever);
        root.Properties().StartAnimation(L"Progress", progress);

        ShowWindow(window, SW_SHOW);
        UpdateWindow(window);

        MSG message{};
        while (GetMessageW(&message, nullptr, 0, 0) > 0)
        {
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }
    }
}

int wmain(int argc, wchar_t* argv[])
{
    if (argc < 2 || argc > 3)
    {
        std::wcerr << L"Usage: LottieRuntime.exe <path-to-flatbuffer> [--dump|--show]\n";
        return 1;
    }

    try
    {
        enum class Mode
        {
            Validate,
            Dump,
            Show,
        };

        auto mode = Mode::Validate;
        if (argc == 3)
        {
            if (std::wstring_view(argv[2]) == L"--dump")
            {
                mode = Mode::Dump;
            }
            else if (std::wstring_view(argv[2]) == L"--show")
            {
                mode = Mode::Show;
            }
            else
            {
                throw std::invalid_argument("Mode must be --dump or --show.");
            }
        }

        winrt::check_hresult(CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED));
        auto dispatcherQueueController = CreateDispatcherQueue();
        auto bytes = ReadFileBytes(argv[1]);

        auto loader = CreateLoader();
        Visual root{ nullptr };
        winrt::check_hresult(loader->LoadComposition(
            static_cast<UINT32>(bytes.size()),
            reinterpret_cast<BYTE const*>(bytes.data()),
            winrt::guid_of<Visual>(),
            winrt::put_abi(root)));

        switch (mode)
        {
        case Mode::Validate:
            std::wcout << L"Composition loaded successfully.\n";
            break;
        case Mode::Dump:
            std::wcout << L"Composition object hierarchy:\n";
            DumpVisual(root, 0);
            break;
        case Mode::Show:
            ShowVisual(root);
            break;
        }

        root = nullptr;
        loader = nullptr;
        dispatcherQueueController.ShutdownQueueAsync();
    }
    catch (winrt::hresult_error const& e)
    {
        std::wcerr << L"Failed to load composition: " << e.message().c_str() << L"\n";
        return 1;
    }
    catch (std::exception const& e)
    {
        std::cerr << "Failed: " << e.what() << "\n";
        return 1;
    }

    return 0;
}
